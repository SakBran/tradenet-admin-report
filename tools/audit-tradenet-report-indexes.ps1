[CmdletBinding()]
param(
    [string]$ConfigPath = "Backend/appsettings.json",
    [string]$ConnectionName = "TradeNetDBTest",
    [string]$OutputDirectory = "artifacts/report-index-audit",
    [int]$CommandTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Convert-DataTableToRows {
    param([System.Data.DataTable]$Table)

    return @(
        foreach ($row in $Table.Rows) {
            $item = [ordered]@{}
            foreach ($column in $Table.Columns) {
                $value = $row[$column.ColumnName]
                $item[$column.ColumnName] = if ($value -is [System.DBNull]) { $null } else { $value }
            }

            [pscustomobject]$item
        }
    )
}

function Invoke-AuditQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Name,
        [string]$Sql,
        [string]$OutputPath,
        [int]$TimeoutSeconds
    )

    Write-Host "Collecting $Name..."

    $command = $Connection.CreateCommand()
    $command.CommandTimeout = $TimeoutSeconds
    $command.CommandText = $Sql

    try {
        $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
        $table = [System.Data.DataTable]::new($Name)
        [void]$adapter.Fill($table)
        $rows = Convert-DataTableToRows -Table $table

        $rows | Export-Csv -LiteralPath (Join-Path $OutputPath "$Name.csv") -NoTypeInformation -Encoding utf8
        $rows | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputPath "$Name.json") -Encoding utf8

        return $rows
    }
    finally {
        $command.Dispose()
    }
}

function Get-ControllerBranch {
    param([string]$ControllerName)

    $name = $ControllerName -replace "Controller$", ""

    foreach ($candidate in @(
        @{ Prefix = "BorderExportPermit"; Branch = "Border Export Permit" },
        @{ Prefix = "BorderExportLicence"; Branch = "Border Export Licence" },
        @{ Prefix = "BorderImportPermit"; Branch = "Border Import Permit" },
        @{ Prefix = "BorderImportLicence"; Branch = "Border Import Licence" },
        @{ Prefix = "ExportPermit"; Branch = "Export Permit" },
        @{ Prefix = "ExportLicence"; Branch = "Export Licence" },
        @{ Prefix = "ImportPermit"; Branch = "Import Permit" },
        @{ Prefix = "ImportLicence"; Branch = "Import Licence" }
    )) {
        if ($name.StartsWith($candidate.Prefix, [System.StringComparison]::Ordinal)) {
            return $candidate.Branch
        }
    }

    return $name
}

function Get-ControllerQueryMap {
    param([string]$RepositoryRoot)

    $controllerPath = Join-Path $RepositoryRoot "Backend/Controllers/Report"
    $rows = foreach ($file in Get-ChildItem -LiteralPath $controllerPath -Filter "*Controller.cs" | Sort-Object Name) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $matches = [regex]::Matches(
            $text,
            "\b(sp_[A-Za-z0-9_]+)\.(?:Query|ExecuteAsync|CreatePagedResultAsync|CreateAggregateResultAsync|CreateAggregateExcelWorkbookAsync|CreateExcelWorkbookAsync)\s*\(")
        $queryClasses = @($matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)

        if ($queryClasses.Count -eq 0) {
            [pscustomobject]@{
                Controller = $file.BaseName
                Branch = Get-ControllerBranch -ControllerName $file.BaseName
                QueryClass = "<custom direct query>"
                SourceFile = "Backend/Controllers/Report/$($file.Name)"
            }
            continue
        }

        foreach ($queryClass in $queryClasses) {
            [pscustomobject]@{
                Controller = $file.BaseName
                Branch = Get-ControllerBranch -ControllerName $file.BaseName
                QueryClass = $queryClass
                SourceFile = "Backend/Controllers/Report/$($file.Name)"
            }
        }
    }

    return @($rows)
}

function Get-QueryClassInventory {
    param(
        [string]$RepositoryRoot,
        [object[]]$ControllerMap
    )

    $helperPath = Join-Path $RepositoryRoot "Backend/StoredProcedureToLinq"
    $queryClasses = @(
        $ControllerMap.QueryClass |
            Where-Object { $_ -ne "<custom direct query>" } |
            Sort-Object -Unique
    )

    $rows = foreach ($queryClass in $queryClasses) {
        $files = @(Get-ChildItem -LiteralPath $helperPath -Filter "$queryClass*.cs" | Sort-Object Name)
        $text = ($files | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"

        $tables = @(
            [regex]::Matches($text, "\b(?:db|_context)\.([A-Za-z0-9_]+)") |
                ForEach-Object { $_.Groups[1].Value } |
                Where-Object { $_ -notin @("Database", "Set") } |
                Sort-Object -Unique
        )
        $predicates = @(
            [regex]::Matches($text, "\brequest\.([A-Za-z0-9_]+)") |
                ForEach-Object { $_.Groups[1].Value } |
                Sort-Object -Unique
        )
        $orderings = @(
            [regex]::Matches($text, "\.(?:OrderBy|OrderByDescending|ThenBy|ThenByDescending)\s*\(\s*[A-Za-z0-9_]+\s*=>\s*[A-Za-z0-9_]+\.([A-Za-z0-9_]+)") |
                ForEach-Object { $_.Groups[1].Value } |
                Sort-Object -Unique
        )
        $joins = @(
            [regex]::Matches($text, "(?:\bjoin\s+[A-Za-z0-9_]+\s+in\s+(?:db|_context)\.|\.Join\(\s*(?:db|_context)\.)([A-Za-z0-9_]+)") |
                ForEach-Object { $_.Groups[1].Value } |
                Sort-Object -Unique
        )
        $branches = @(
            $ControllerMap |
                Where-Object { $_.QueryClass -eq $queryClass } |
                Select-Object -ExpandProperty Branch |
                Sort-Object -Unique
        )
        $controllers = @(
            $ControllerMap |
                Where-Object { $_.QueryClass -eq $queryClass } |
                Select-Object -ExpandProperty Controller |
                Sort-Object -Unique
        )

        [pscustomobject]@{
            QueryClass = $queryClass
            Branches = $branches -join "; "
            Controllers = $controllers -join "; "
            Tables = $tables -join "; "
            RequestPredicates = $predicates -join "; "
            Joins = $joins -join "; "
            Ordering = $orderings -join "; "
            SourceFiles = @($files | ForEach-Object { "Backend/StoredProcedureToLinq/$($_.Name)" }) -join "; "
        }
    }

    $customRows = @(
        $ControllerMap |
            Where-Object { $_.QueryClass -eq "<custom direct query>" } |
            ForEach-Object {
                [pscustomobject]@{
                    QueryClass = $_.QueryClass
                    Branches = $_.Branch
                    Controllers = $_.Controller
                    Tables = "See controller source"
                    RequestPredicates = "See controller source"
                    Joins = "See controller source"
                    Ordering = "See controller source"
                    SourceFiles = $_.SourceFile
                }
            }
    )

    return @($rows) + $customRows
}

function ConvertTo-MarkdownCell {
    param([object]$Value)

    return ([string]$Value) -replace "\|", "\|"
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedConfigPath = if ([System.IO.Path]::IsPathRooted($ConfigPath)) {
    $ConfigPath
}
else {
    Join-Path $repositoryRoot $ConfigPath
}
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $repositoryRoot $OutputDirectory
}

if (-not (Test-Path -LiteralPath $resolvedConfigPath)) {
    throw "Configuration file not found: $resolvedConfigPath"
}

$configuration = Get-Content -LiteralPath $resolvedConfigPath -Raw | ConvertFrom-Json
$connectionString = [string]$configuration.ConnectionStrings.$ConnectionName
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "ConnectionStrings.$ConnectionName was not found in $resolvedConfigPath"
}

$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionString)
New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null

$controllerMap = Get-ControllerQueryMap -RepositoryRoot $repositoryRoot
$queryClassInventory = Get-QueryClassInventory -RepositoryRoot $repositoryRoot -ControllerMap $controllerMap

$controllerMap | Export-Csv -LiteralPath (Join-Path $resolvedOutputPath "controller-query-map.csv") -NoTypeInformation -Encoding utf8
$controllerMap | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedOutputPath "controller-query-map.json") -Encoding utf8
$queryClassInventory | Export-Csv -LiteralPath (Join-Path $resolvedOutputPath "query-class-inventory.csv") -NoTypeInformation -Encoding utf8
$queryClassInventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedOutputPath "query-class-inventory.json") -Encoding utf8

$queryInventoryMarkdown = @(
    "# TradeNet Report Query-Class Inventory"
    ""
    "Generated by ``tools/audit-tradenet-report-indexes.ps1``. This is an ignored local evidence snapshot."
    ""
    "| Query class | Branches | Controllers | Tables | Request predicates | Static join targets | Ordering |"
    "| --- | --- | --- | --- | --- | --- | --- |"
)
$queryInventoryMarkdown += $queryClassInventory | ForEach-Object {
    "| $(ConvertTo-MarkdownCell $_.QueryClass) | $(ConvertTo-MarkdownCell $_.Branches) | $(ConvertTo-MarkdownCell $_.Controllers) | $(ConvertTo-MarkdownCell $_.Tables) | $(ConvertTo-MarkdownCell $_.RequestPredicates) | $(ConvertTo-MarkdownCell $_.Joins) | $(ConvertTo-MarkdownCell $_.Ordering) |"
}
$queryInventoryMarkdown | Set-Content -LiteralPath (Join-Path $resolvedOutputPath "query-class-inventory.md") -Encoding utf8

$queries = [ordered]@{
    "database-summary" = @"
SELECT
    DB_NAME() AS DatabaseName,
    CONVERT(nvarchar(256), SERVERPROPERTY(N'Edition')) AS Edition,
    CONVERT(int, SERVERPROPERTY(N'EngineEdition')) AS EngineEdition,
    CONVERT(nvarchar(128), SERVERPROPERTY(N'ProductVersion')) AS ProductVersion,
    (SELECT COUNT(*) FROM sys.foreign_keys) AS ForeignKeyCount,
    (SELECT COUNT(*) FROM sys.procedures WHERE name LIKE N'%[_]pagination') AS PaginationProcedureCount,
    (SELECT COUNT(*) FROM sys.views WHERE OBJECTPROPERTY(object_id, N'IsSchemaBound') = 1) AS SchemaBoundViewCount;
"@
    "table-sizes" = @"
SELECT
    SchemaDefinition.name AS SchemaName,
    TableDefinition.name AS TableName,
    SUM(PartitionStats.row_count) AS [Rows],
    CONVERT(decimal(18, 2), SUM(PartitionStats.used_page_count) * 8.0 / 1024) AS UsedMB,
    COUNT(DISTINCT CASE WHEN IndexDefinition.index_id > 0 THEN IndexDefinition.index_id END) AS IndexCount
FROM sys.tables AS TableDefinition
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = TableDefinition.schema_id
JOIN sys.dm_db_partition_stats AS PartitionStats
  ON PartitionStats.object_id = TableDefinition.object_id
 AND PartitionStats.index_id IN (0, 1)
LEFT JOIN sys.indexes AS IndexDefinition
  ON IndexDefinition.object_id = TableDefinition.object_id
WHERE SchemaDefinition.name = N'dbo'
GROUP BY SchemaDefinition.name, TableDefinition.name
ORDER BY SUM(PartitionStats.row_count) DESC, TableDefinition.name;
"@
    "indexes" = @"
SELECT
    SchemaDefinition.name AS SchemaName,
    ObjectDefinition.name AS ObjectName,
    ObjectDefinition.type_desc AS ObjectType,
    IndexDefinition.name AS IndexName,
    IndexDefinition.type_desc AS IndexType,
    IndexDefinition.is_unique AS IsUnique,
    IndexDefinition.is_primary_key AS IsPrimaryKey,
    IndexDefinition.is_disabled AS IsDisabled,
    STUFF((
        SELECT N', ' + QUOTENAME(ColumnDefinition.name)
        FROM sys.index_columns AS IndexColumn
        JOIN sys.columns AS ColumnDefinition
          ON ColumnDefinition.object_id = IndexColumn.object_id
         AND ColumnDefinition.column_id = IndexColumn.column_id
        WHERE IndexColumn.object_id = IndexDefinition.object_id
          AND IndexColumn.index_id = IndexDefinition.index_id
          AND IndexColumn.key_ordinal > 0
        ORDER BY IndexColumn.key_ordinal
        FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 2, N'') AS KeyColumns,
    STUFF((
        SELECT N', ' + QUOTENAME(ColumnDefinition.name)
        FROM sys.index_columns AS IndexColumn
        JOIN sys.columns AS ColumnDefinition
          ON ColumnDefinition.object_id = IndexColumn.object_id
         AND ColumnDefinition.column_id = IndexColumn.column_id
        WHERE IndexColumn.object_id = IndexDefinition.object_id
          AND IndexColumn.index_id = IndexDefinition.index_id
          AND IndexColumn.is_included_column = 1
        ORDER BY IndexColumn.index_column_id
        FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 2, N'') AS IncludedColumns,
    IndexDefinition.filter_definition AS FilterDefinition
FROM sys.objects AS ObjectDefinition
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = ObjectDefinition.schema_id
JOIN sys.indexes AS IndexDefinition
  ON IndexDefinition.object_id = ObjectDefinition.object_id
WHERE SchemaDefinition.name = N'dbo'
  AND ObjectDefinition.type IN (N'U', N'V')
  AND IndexDefinition.index_id > 0
  AND IndexDefinition.is_hypothetical = 0
ORDER BY ObjectDefinition.name, IndexDefinition.index_id;
"@
    "index-usage" = @"
SELECT
    SchemaDefinition.name AS SchemaName,
    ObjectDefinition.name AS ObjectName,
    IndexDefinition.name AS IndexName,
    COALESCE(IndexUsage.user_seeks, 0) AS UserSeeks,
    COALESCE(IndexUsage.user_scans, 0) AS UserScans,
    COALESCE(IndexUsage.user_lookups, 0) AS UserLookups,
    COALESCE(IndexUsage.user_updates, 0) AS UserUpdates,
    IndexUsage.last_user_seek AS LastUserSeek,
    IndexUsage.last_user_scan AS LastUserScan,
    IndexUsage.last_user_update AS LastUserUpdate
FROM sys.objects AS ObjectDefinition
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = ObjectDefinition.schema_id
JOIN sys.indexes AS IndexDefinition
  ON IndexDefinition.object_id = ObjectDefinition.object_id
LEFT JOIN sys.dm_db_index_usage_stats AS IndexUsage
  ON IndexUsage.database_id = DB_ID()
 AND IndexUsage.object_id = IndexDefinition.object_id
 AND IndexUsage.index_id = IndexDefinition.index_id
WHERE SchemaDefinition.name = N'dbo'
  AND ObjectDefinition.type IN (N'U', N'V')
  AND IndexDefinition.index_id > 0
  AND IndexDefinition.is_hypothetical = 0
ORDER BY ObjectDefinition.name, IndexDefinition.index_id;
"@
    "duplicate-key-candidates" = @"
WITH IndexKeys AS
(
    SELECT
        SchemaDefinition.name AS SchemaName,
        TableDefinition.name AS TableName,
        IndexDefinition.object_id,
        IndexDefinition.index_id,
        IndexDefinition.name AS IndexName,
        STUFF((
            SELECT N', ' + QUOTENAME(ColumnDefinition.name)
            FROM sys.index_columns AS IndexColumn
            JOIN sys.columns AS ColumnDefinition
              ON ColumnDefinition.object_id = IndexColumn.object_id
             AND ColumnDefinition.column_id = IndexColumn.column_id
            WHERE IndexColumn.object_id = IndexDefinition.object_id
              AND IndexColumn.index_id = IndexDefinition.index_id
              AND IndexColumn.key_ordinal > 0
            ORDER BY IndexColumn.key_ordinal
            FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 2, N'') AS KeyColumns
    FROM sys.tables AS TableDefinition
    JOIN sys.schemas AS SchemaDefinition
      ON SchemaDefinition.schema_id = TableDefinition.schema_id
    JOIN sys.indexes AS IndexDefinition
      ON IndexDefinition.object_id = TableDefinition.object_id
    WHERE SchemaDefinition.name = N'dbo'
      AND IndexDefinition.index_id > 0
      AND IndexDefinition.is_hypothetical = 0
      AND IndexDefinition.is_disabled = 0
)
SELECT
    FirstIndex.SchemaName,
    FirstIndex.TableName,
    FirstIndex.KeyColumns,
    FirstIndex.IndexName AS FirstIndexName,
    SecondIndex.IndexName AS SecondIndexName
FROM IndexKeys AS FirstIndex
JOIN IndexKeys AS SecondIndex
  ON SecondIndex.object_id = FirstIndex.object_id
 AND SecondIndex.index_id > FirstIndex.index_id
 AND SecondIndex.KeyColumns = FirstIndex.KeyColumns
ORDER BY FirstIndex.TableName, FirstIndex.KeyColumns, FirstIndex.IndexName;
"@
    "missing-index-dmvs" = @"
SELECT TOP (250)
    SchemaDefinition.name AS SchemaName,
    TableDefinition.name AS TableName,
    MissingGroupStats.user_seeks AS UserSeeks,
    MissingGroupStats.user_scans AS UserScans,
    CONVERT(decimal(18, 2), MissingGroupStats.avg_total_user_cost) AS AvgTotalUserCost,
    CONVERT(decimal(18, 2), MissingGroupStats.avg_user_impact) AS AvgUserImpactPercent,
    CONVERT(decimal(18, 2),
        (MissingGroupStats.user_seeks + MissingGroupStats.user_scans)
        * MissingGroupStats.avg_total_user_cost
        * MissingGroupStats.avg_user_impact / 100.0) AS ImprovementScore,
    MissingDetails.equality_columns AS EqualityColumns,
    MissingDetails.inequality_columns AS InequalityColumns,
    MissingDetails.included_columns AS IncludedColumns,
    MissingGroupStats.last_user_seek AS LastUserSeek,
    MissingGroupStats.last_user_scan AS LastUserScan
FROM sys.dm_db_missing_index_group_stats AS MissingGroupStats
JOIN sys.dm_db_missing_index_groups AS MissingGroups
  ON MissingGroups.index_group_handle = MissingGroupStats.group_handle
JOIN sys.dm_db_missing_index_details AS MissingDetails
  ON MissingDetails.index_handle = MissingGroups.index_handle
JOIN sys.tables AS TableDefinition
  ON TableDefinition.object_id = MissingDetails.object_id
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = TableDefinition.schema_id
WHERE MissingDetails.database_id = DB_ID()
ORDER BY ImprovementScore DESC, TableDefinition.name;
"@
    "indexed-views" = @"
SELECT
    SchemaDefinition.name AS SchemaName,
    ViewDefinition.name AS ViewName,
    IndexDefinition.name AS IndexName,
    IndexDefinition.type_desc AS IndexType,
    IndexDefinition.is_unique AS IsUnique,
    IndexDefinition.is_disabled AS IsDisabled
FROM sys.views AS ViewDefinition
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = ViewDefinition.schema_id
JOIN sys.indexes AS IndexDefinition
  ON IndexDefinition.object_id = ViewDefinition.object_id
WHERE SchemaDefinition.name = N'dbo'
  AND IndexDefinition.index_id > 0
ORDER BY ViewDefinition.name, IndexDefinition.index_id;
"@
    "pagination-procedures" = @"
SELECT
    SchemaDefinition.name AS SchemaName,
    ProcedureDefinition.name AS ProcedureName,
    ProcedureDefinition.create_date AS CreatedDate,
    ProcedureDefinition.modify_date AS ModifiedDate
FROM sys.procedures AS ProcedureDefinition
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = ProcedureDefinition.schema_id
WHERE SchemaDefinition.name = N'dbo'
  AND ProcedureDefinition.name LIKE N'%[_]pagination'
ORDER BY ProcedureDefinition.name;
"@
    "procedure-runtime-stats" = @"
SELECT TOP (250)
    SchemaDefinition.name AS SchemaName,
    ObjectDefinition.name AS ProcedureName,
    ProcedureStats.cached_time AS CachedTime,
    ProcedureStats.last_execution_time AS LastExecutionTime,
    ProcedureStats.execution_count AS ExecutionCount,
    CONVERT(decimal(18, 2), ProcedureStats.total_elapsed_time / NULLIF(ProcedureStats.execution_count, 0) / 1000.0) AS AvgElapsedMs,
    CONVERT(decimal(18, 2), ProcedureStats.max_elapsed_time / 1000.0) AS MaxElapsedMs,
    CONVERT(decimal(18, 2), ProcedureStats.total_logical_reads * 1.0 / NULLIF(ProcedureStats.execution_count, 0)) AS AvgLogicalReads,
    ProcedureStats.max_logical_reads AS MaxLogicalReads
FROM sys.dm_exec_procedure_stats AS ProcedureStats
JOIN sys.objects AS ObjectDefinition
  ON ObjectDefinition.object_id = ProcedureStats.object_id
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = ObjectDefinition.schema_id
WHERE ProcedureStats.database_id = DB_ID()
ORDER BY AvgElapsedMs DESC, ObjectDefinition.name;
"@
}

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()

try {
    $datasets = [ordered]@{}
    foreach ($query in $queries.GetEnumerator()) {
        $datasets[$query.Key] = Invoke-AuditQuery `
            -Connection $connection `
            -Name $query.Key `
            -Sql $query.Value `
            -OutputPath $resolvedOutputPath `
            -TimeoutSeconds $CommandTimeoutSeconds
    }

    $manifest = [ordered]@{
        GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
        ConnectionName = $ConnectionName
        DataSource = $builder.DataSource
        InitialCatalog = $builder.InitialCatalog
        ControllerCount = @($controllerMap | Select-Object -ExpandProperty Controller -Unique).Count
        SharedQueryClassCount = @($queryClassInventory | Where-Object { $_.QueryClass -ne "<custom direct query>" }).Count
        CustomDirectQueryControllerCount = @($queryClassInventory | Where-Object { $_.QueryClass -eq "<custom direct query>" }).Count
        DatasetCounts = [ordered]@{}
    }
    foreach ($dataset in $datasets.GetEnumerator()) {
        $manifest.DatasetCounts[$dataset.Key] = @($dataset.Value).Count
    }

    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resolvedOutputPath "manifest.json") -Encoding utf8
}
finally {
    $connection.Close()
    $connection.Dispose()
}

Write-Host ""
Write-Host "TradeNet report index audit complete."
Write-Host "Connection name: $ConnectionName"
Write-Host "Server: $($builder.DataSource)"
Write-Host "Database: $($builder.InitialCatalog)"
Write-Host "Controllers: $(@($controllerMap | Select-Object -ExpandProperty Controller -Unique).Count)"
Write-Host "Shared query classes: $(@($queryClassInventory | Where-Object { $_.QueryClass -ne '<custom direct query>' }).Count)"
Write-Host "Output: $resolvedOutputPath"
