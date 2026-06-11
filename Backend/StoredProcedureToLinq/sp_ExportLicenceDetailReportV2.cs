using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public static class sp_ExportLicenceDetailReportV2
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_ExportLicenceDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = NormalizePageSize(pagingRequest.PageSize);

        var rows = await ExecuteAsync(
            db,
            request,
            pageIndex,
            pageSize,
            pagingRequest.IncludeTotalCount);

        var results = rows.Select(row => row.ToResult()).ToList();

        if (pagingRequest.IncludeTotalCount)
        {
            var totalCount = rows.Count == 0 ? 0 : rows[0].TotalCount;
            return ApiResult<sp_ExportLicenceDetailReportResult>.CreatePageFromRows(
                results,
                totalCount,
                pageIndex,
                pageSize,
                pagingRequest.SortColumn,
                pagingRequest.SortOrder,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_ExportLicenceDetailReportResult>.CreateFastPageFromRows(
            results,
            pageIndex,
            pageSize,
            pagingRequest.SortColumn,
            pagingRequest.SortOrder,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery);
    }

    private static async Task<List<sp_ExportLicenceDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        int pageIndex,
        int pageSize,
        bool includeTotalCount,
        CancellationToken cancellationToken = default)
    {
        var parameters = new[]
        {
            new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = request.FromDate },
            new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = request.ToDate },
            new SqlParameter("@PaThaKaTypeId", SqlDbType.Int) { Value = request.PaThaKaTypeId },
            new SqlParameter("@ExportImportSectionId", SqlDbType.Int) { Value = request.ExportImportSectionId },
            new SqlParameter("@ExportImportMethodId", SqlDbType.Int) { Value = request.ExportImportMethodId },
            new SqlParameter("@ExportImportIncotermId", SqlDbType.Int) { Value = request.ExportImportIncotermId },
            new SqlParameter("@BuyerCountryId", SqlDbType.Int) { Value = request.BuyerCountryId },
            new SqlParameter("@CompanyRegistrationNo", SqlDbType.NVarChar, 50)
            {
                Value = request.CompanyRegistrationNo ?? string.Empty
            },
            new SqlParameter("@PageIndex", SqlDbType.Int) { Value = pageIndex },
            new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize },
            new SqlParameter("@IncludeTotalCount", SqlDbType.Bit) { Value = includeTotalCount },
        };

        return await ExecuteSeekedAsync(
            db,
            parameters,
            pageIndex,
            pageSize,
            includeTotalCount,
            cancellationToken);
    }

    private static async Task<List<sp_ExportLicenceDetailReportRow>> ExecuteSeekedAsync(
        TradeNetDbContext db,
        SqlParameter[] filterParameters,
        int pageIndex,
        int pageSize,
        bool includeTotalCount,
        CancellationToken cancellationToken)
    {
        const string licenceKeySql = """
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            SELECT
                CONVERT(nvarchar(36), licence.Id) AS LicenceId,
                licence.CreatedDate,
                licence.IssuedDate AS LicenceDate,
                licence.ExportLicenceNo AS LicenceNo
            FROM dbo.ExportLicence AS licence WITH (INDEX(IX_ExportLicence_Report_NewDetail_Page))
            INNER JOIN dbo.PaThaKa AS paThaKa ON paThaKa.Id = licence.PaThaKaId
            INNER JOIN dbo.PaThaKaType AS paThaKaType ON paThaKa.PaThaKaTypeId = paThaKaType.Id
            WHERE licence.ApplyType = N'New'
              AND licence.Status = N'Approved'
              AND licence.CreatedDate >= @FromDate
              AND licence.CreatedDate <= @ToDate
              AND (@CompanyRegistrationNo = N'' OR paThaKa.CompanyRegistrationNo = @CompanyRegistrationNo)
              AND (@PaThaKaTypeId = 0 OR paThaKaType.Id = @PaThaKaTypeId)
              AND (@ExportImportSectionId = 0 OR licence.ExportImportSectionId = @ExportImportSectionId)
              AND (@ExportImportMethodId = 0 OR licence.ExportImportMethodId = @ExportImportMethodId)
              AND (@ExportImportIncotermId = 0 OR licence.ExportImportIncotermId = @ExportImportIncotermId)
              AND (@BuyerCountryId = 0 OR licence.BuyerCountryId = @BuyerCountryId)
            ORDER BY licence.CreatedDate, licence.Id
            OPTION (RECOMPILE, MAXDOP 1);
            """;

        const string coveredItemKeySql = """
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            SELECT
                CAST(N'' AS nvarchar(36)) AS LicenceId,
                CONVERT(nvarchar(36), item.Id) AS ItemId,
                item.UniqueId AS ItemUniqueId,
                item.HSCode,
                item.ItemNo,
                item.HSCodeId,
                item.UnitId,
                item.CurrencyId,
                item.Description,
                item.Price,
                item.Quantity,
                item.Amount
            FROM dbo.ExportLicenceItem AS item WITH (INDEX(IX_ExportLicenceItem_Report_Licence_Page))
            WHERE item.ExportLicenceId = @LicenceId
            ORDER BY item.HSCode, item.ItemNo, item.Id
            OPTION (RECOMPILE, MAXDOP 1);
            """;

        const string safeItemKeySql = """
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            SELECT
                CAST(N'' AS nvarchar(36)) AS LicenceId,
                CONVERT(nvarchar(36), item.Id) AS ItemId,
                item.UniqueId AS ItemUniqueId,
                item.HSCode,
                item.ItemNo,
                CAST(0 AS int) AS HSCodeId,
                CAST(0 AS int) AS UnitId,
                CAST(0 AS int) AS CurrencyId,
                CAST(NULL AS nvarchar(max)) AS Description,
                CAST(0 AS decimal(18, 2)) AS Price,
                CAST(0 AS decimal(18, 2)) AS Quantity,
                CAST(0 AS decimal(18, 2)) AS Amount
            FROM dbo.ExportLicenceItem AS item WITH (INDEX(IX_ExportLicenceItem_Report_Licence_Page))
            WHERE item.ExportLicenceId = @LicenceId
            ORDER BY item.HSCode, item.ItemNo, item.Id
            OPTION (RECOMPILE, MAXDOP 1);
            """;

        var licences = await db.Database
            .SqlQueryRaw<ExportLicenceDetailLicenceKey>(licenceKeySql, CloneFilterParameters(filterParameters))
            .ToListAsync(cancellationToken);
        var itemDetailsCovered = await IsItemPageIndexCoveredAsync(db, cancellationToken);
        var itemKeySql = itemDetailsCovered ? coveredItemKeySql : safeItemKeySql;

        var offset = pageIndex * pageSize;
        var take = pageSize + (includeTotalCount ? 0 : 1);
        var selectedKeys = new List<ExportLicenceDetailItemKey>(take);
        var totalCount = 0;

        foreach (var licence in licences)
        {
            var itemKeys = await db.Database
                .SqlQueryRaw<ExportLicenceDetailItemKey>(
                    itemKeySql,
                    new SqlParameter("@LicenceId", SqlDbType.Char, 36) { Value = licence.LicenceId })
                .ToListAsync(cancellationToken);

            foreach (var itemKey in itemKeys)
            {
                if (includeTotalCount)
                {
                    totalCount++;
                }

                if (totalCount > offset && selectedKeys.Count < take)
                {
                    itemKey.LicenceId = licence.LicenceId;
                    selectedKeys.Add(itemKey);
                }
                else if (!includeTotalCount && selectedKeys.Count < take)
                {
                    if (totalCount >= offset)
                    {
                        itemKey.LicenceId = licence.LicenceId;
                        selectedKeys.Add(itemKey);
                    }
                }

                if (!includeTotalCount)
                {
                    totalCount++;
                    if (selectedKeys.Count >= take)
                    {
                        break;
                    }
                }
            }

            if (!includeTotalCount && selectedKeys.Count >= take)
            {
                break;
            }
        }

        if (!includeTotalCount)
        {
            totalCount = 0;
        }

        var rows = new List<sp_ExportLicenceDetailReportRow>(selectedKeys.Count);
        foreach (var key in selectedKeys)
        {
            var row = await FetchDetailRowAsync(db, key, totalCount, itemDetailsCovered, cancellationToken);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static async Task<sp_ExportLicenceDetailReportRow?> FetchDetailRowAsync(
        TradeNetDbContext db,
        ExportLicenceDetailItemKey key,
        int totalCount,
        bool itemDetailsCovered,
        CancellationToken cancellationToken)
    {
        var licenceRows = await db.Database
            .SqlQueryRaw<ExportLicenceDetailLicencePart>(
                LicenceDetailSql,
                new SqlParameter("@LicenceId", SqlDbType.Char, 36) { Value = key.LicenceId })
            .ToListAsync(cancellationToken);
        var licence = licenceRows.FirstOrDefault();
        if (licence is null)
        {
            return null;
        }

        var item = itemDetailsCovered
            ? new ExportLicenceDetailItemPart
            {
                HSCodeId = key.HSCodeId,
                UnitId = key.UnitId,
                CurrencyId = key.CurrencyId,
                Description = key.Description,
                Price = key.Price,
                Quantity = key.Quantity,
                Amount = key.Amount,
            }
            : await FetchItemDetailAsync(db, key, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var unitCode = await FetchUnitCodeAsync(db, item.UnitId, cancellationToken);
        var currencyCode = await FetchCurrencyCodeAsync(db, item.CurrencyId, cancellationToken);
        var hsCode = await FetchHsCodeAsync(db, item.HSCodeId, cancellationToken);
        var portofExport = await FetchDelimitedLookupNamesAsync(
            db,
            DelimitedLookupTable.PortOfDischarge,
            licence.PortofExportId,
            cancellationToken);
        var destinationCountry = await FetchDelimitedLookupNamesAsync(
            db,
            DelimitedLookupTable.Countries,
            licence.DestinationCountryId,
            cancellationToken);

        return new sp_ExportLicenceDetailReportRow
        {
            PaThaKaTypeId = licence.PaThaKaTypeId,
            PaThaKaTypeCode = licence.PaThaKaTypeCode,
            PaThaKaTypeName = licence.PaThaKaTypeName,
            SakhanId = null,
            SakhanCode = null,
            SakhanName = null,
            ExportImportSectionId = licence.ExportImportSectionId,
            ExportImportMethodId = licence.ExportImportMethodId,
            ExportImportIncotermId = licence.ExportImportIncotermId,
            BuyerCountryId = licence.BuyerCountryId,
            SectionCode = licence.SectionCode,
            SectionName = licence.SectionName,
            LicenceNo = licence.LicenceNo,
            LicenceDate = licence.LicenceDate,
            CompanyRegistrationNo = licence.CompanyRegistrationNo,
            CompanyName = licence.CompanyName,
            UnitLevel = licence.UnitLevel,
            StreetNumberStreetName = licence.StreetNumberStreetName,
            QuarterCityTownship = licence.QuarterCityTownship,
            State = licence.State,
            Country = licence.Country,
            PostalCode = licence.PostalCode,
            BuyerName = licence.BuyerName,
            BuyerAddress = licence.BuyerAddress,
            BuyerCountry = licence.BuyerCountry,
            PortofExport = portofExport,
            PortofDischarge = licence.PortofDischarge,
            LastDate = licence.LastDate,
            MethodName = licence.MethodName,
            DestinationCountry = destinationCountry,
            ConsignedCountry = licence.ConsignedCountry,
            CountryofOrigin = licence.CountryofOrigin,
            HSCode = hsCode.Code,
            HSDescription = $"{hsCode.Description} {item.Description}".Trim(),
            Unit = unitCode,
            Price = item.Price,
            Quantity = item.Quantity,
            Amount = item.Amount,
            Currency = currencyCode,
            Conditions = licence.Conditions,
            ApplicationNo = licence.ApplicationNo,
            ApplicationDate = licence.ApplicationDate,
            CommodityType = licence.CommodityType,
            ApproveDate = licence.ApproveDate,
            TotalCount = totalCount,
        };
    }

    private static SqlParameter[] CloneFilterParameters(SqlParameter[] parameters) =>
        parameters
            .Where(parameter => parameter.ParameterName is not "@PageIndex"
                and not "@PageSize"
                and not "@IncludeTotalCount")
            .Select(parameter => new SqlParameter(parameter.ParameterName, parameter.SqlDbType, parameter.Size)
            {
                Value = parameter.Value
            })
            .ToArray();

    private static async Task<bool> IsItemPageIndexCoveredAsync(
        TradeNetDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database
            .SqlQueryRaw<LookupValue>(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.indexes AS i
                    INNER JOIN sys.index_columns AS ic
                        ON ic.object_id = i.object_id
                        AND ic.index_id = i.index_id
                    INNER JOIN sys.columns AS c
                        ON c.object_id = ic.object_id
                        AND c.column_id = ic.column_id
                    WHERE i.name = N'IX_ExportLicenceItem_Report_Licence_Page'
                      AND i.object_id = OBJECT_ID(N'[dbo].[ExportLicenceItem]')
                      AND c.name = N'Amount'
                      AND ic.is_included_column = 1
                ) THEN N'1' ELSE N'0' END AS Value;
                """)
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault()?.Value == "1";
    }

    private static async Task<string?> FetchUnitCodeAsync(
        TradeNetDbContext db,
        int id,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database
            .SqlQueryRaw<LookupValue>(
                """
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                SELECT CAST(Code AS nvarchar(4000)) AS Value
                FROM dbo.Unit
                WHERE Id = @Id
                OPTION (RECOMPILE, MAXDOP 1);
                """,
                new SqlParameter("@Id", SqlDbType.Int) { Value = id })
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault()?.Value;
    }

    private static async Task<string?> FetchCurrencyCodeAsync(
        TradeNetDbContext db,
        int id,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database
            .SqlQueryRaw<LookupValue>(
                """
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                SELECT CAST(Code AS nvarchar(4000)) AS Value
                FROM dbo.Currency
                WHERE Id = @Id
                OPTION (RECOMPILE, MAXDOP 1);
                """,
                new SqlParameter("@Id", SqlDbType.Int) { Value = id })
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault()?.Value;
    }

    private static async Task<ExportLicenceDetailItemPart?> FetchItemDetailAsync(
        TradeNetDbContext db,
        ExportLicenceDetailItemKey key,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database
            .SqlQueryRaw<ExportLicenceDetailItemPart>(
                ItemDetailSql,
                new SqlParameter("@ItemId", SqlDbType.Char, 36) { Value = key.ItemId },
                new SqlParameter("@ItemUniqueId", SqlDbType.Int) { Value = key.ItemUniqueId })
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    private enum DelimitedLookupTable
    {
        Countries,
        PortOfDischarge,
    }

    private static async Task<string> FetchDelimitedLookupNamesAsync(
        TradeNetDbContext db,
        DelimitedLookupTable table,
        string? delimitedIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(delimitedIds))
        {
            return string.Empty;
        }

        var sql = table switch
        {
            DelimitedLookupTable.Countries => """
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                SELECT STUFF((
                    SELECT N',' + lookupTable.Name
                    FROM dbo.Countries AS lookupTable
                    WHERE N',' + @Ids + N',' LIKE N'%,' + CONVERT(nvarchar(20), lookupTable.Id) + N',%'
                    ORDER BY lookupTable.Id
                    FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
                , 1, 1, N'') AS Value
                OPTION (RECOMPILE, MAXDOP 1);
                """,
            DelimitedLookupTable.PortOfDischarge => """
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                SELECT STUFF((
                    SELECT N',' + lookupTable.Name
                    FROM dbo.PortOfDischarge AS lookupTable
                    WHERE N',' + @Ids + N',' LIKE N'%,' + CONVERT(nvarchar(20), lookupTable.Id) + N',%'
                    ORDER BY lookupTable.Id
                    FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
                , 1, 1, N'') AS Value
                OPTION (RECOMPILE, MAXDOP 1);
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, null),
        };

        var rows = await db.Database
            .SqlQueryRaw<LookupValue>(
                sql,
                new SqlParameter("@Ids", SqlDbType.NVarChar, -1) { Value = delimitedIds })
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault()?.Value ?? string.Empty;
    }

    private static async Task<HsCodeValue> FetchHsCodeAsync(
        TradeNetDbContext db,
        int id,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database
            .SqlQueryRaw<HsCodeValue>(
                """
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                SELECT
                    Code,
                    Description
                FROM dbo.HSCode
                WHERE Id = @Id
                OPTION (RECOMPILE, MAXDOP 1);
                """,
                new SqlParameter("@Id", SqlDbType.Int) { Value = id })
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault() ?? new HsCodeValue();
    }

    private const string LicenceDetailSql = """
        SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

        SELECT
            paThaKaType.Id AS PaThaKaTypeId,
            paThaKaType.Code AS PaThaKaTypeCode,
            paThaKaType.Description AS PaThaKaTypeName,
            licence.ExportImportSectionId,
            licence.ExportImportMethodId,
            licence.ExportImportIncotermId,
            licence.BuyerCountryId,
            section.Code AS SectionCode,
            section.Name AS SectionName,
            licence.ExportLicenceNo AS LicenceNo,
            licence.IssuedDate AS LicenceDate,
            paThaKa.CompanyRegistrationNo,
            paThaKa.CompanyName,
            paThaKa.UnitLevel,
            paThaKa.StreetNumberStreetName,
            paThaKa.QuarterCityTownship,
            paThaKa.State,
            paThaKa.Country,
            paThaKa.PostalCode,
            licence.BuyerName,
            licence.BuyerAddress,
            buyerCountry.Name AS BuyerCountry,
            licence.PortofExportId,
            CAST(N'' AS nvarchar(max)) AS PortofExport,
            licence.PortofDischarge,
            licence.LastDate,
            method.Name AS MethodName,
            licence.DestinationCountryId,
            CAST(N'' AS nvarchar(max)) AS DestinationCountry,
            consignedCountry.Name AS ConsignedCountry,
            countryofOrigin.Name AS CountryofOrigin,
            licence.Remark AS Conditions,
            licence.ApplicationNo,
            licence.ApplicationDate,
            licence.CommodityType,
            licence.ApproveDate
        FROM dbo.ExportLicence AS licence
        INNER JOIN dbo.PaThaKa AS paThaKa ON paThaKa.Id = licence.PaThaKaId
        INNER JOIN dbo.PaThaKaType AS paThaKaType ON paThaKa.PaThaKaTypeId = paThaKaType.Id
        INNER JOIN dbo.ExportImportSection AS section ON section.Id = licence.ExportImportSectionId
        INNER JOIN dbo.Countries AS buyerCountry ON buyerCountry.Id = licence.BuyerCountryId
        INNER JOIN dbo.ExportImportMethod AS method ON method.Id = licence.ExportImportMethodId
        INNER JOIN dbo.Countries AS consignedCountry ON consignedCountry.Id = licence.ConsignedCountryId
        INNER JOIN dbo.Countries AS countryofOrigin ON countryofOrigin.Id = licence.CountryofOriginId
        WHERE licence.Id = @LicenceId
        OPTION (RECOMPILE, MAXDOP 1);
        """;

    private const string ItemDetailSql = """
        SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

        SELECT
            HSCodeId,
            UnitId,
            CurrencyId,
            Description,
            Price,
            Quantity,
            Amount
        FROM dbo.ExportLicenceItem
        WHERE Id = @ItemId
          AND UniqueId = @ItemUniqueId
        OPTION (RECOMPILE, MAXDOP 1);
        """;

    private sealed class ExportLicenceDetailLicenceKey
    {
        public string LicenceId { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? LicenceDate { get; set; }
        public string? LicenceNo { get; set; }
    }

    private sealed class ExportLicenceDetailItemKey
    {
        public string LicenceId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public int ItemUniqueId { get; set; }
        public string? HSCode { get; set; }
        public int? ItemNo { get; set; }
        public int HSCodeId { get; set; }
        public int UnitId { get; set; }
        public int CurrencyId { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class ExportLicenceDetailLicencePart
    {
        public int PaThaKaTypeId { get; set; }
        public string PaThaKaTypeCode { get; set; } = string.Empty;
        public string PaThaKaTypeName { get; set; } = string.Empty;
        public int ExportImportSectionId { get; set; }
        public int ExportImportMethodId { get; set; }
        public int ExportImportIncotermId { get; set; }
        public int BuyerCountryId { get; set; }
        public string SectionCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string LicenceNo { get; set; } = string.Empty;
        public DateTime? LicenceDate { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? UnitLevel { get; set; }
        public string? StreetNumberStreetName { get; set; }
        public string? QuarterCityTownship { get; set; }
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
        public string BuyerName { get; set; } = string.Empty;
        public string BuyerAddress { get; set; } = string.Empty;
        public string? BuyerCountry { get; set; }
        public string? PortofExportId { get; set; }
        public string? PortofExport { get; set; }
        public string PortofDischarge { get; set; } = string.Empty;
        public DateTime? LastDate { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string? DestinationCountryId { get; set; }
        public string? DestinationCountry { get; set; }
        public string? ConsignedCountry { get; set; }
        public string? CountryofOrigin { get; set; }
        public string? Conditions { get; set; }
        public string? ApplicationNo { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public string? CommodityType { get; set; }
        public DateTime? ApproveDate { get; set; }
    }

    private sealed class ExportLicenceDetailItemPart
    {
        public int HSCodeId { get; set; }
        public int UnitId { get; set; }
        public int CurrencyId { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class LookupValue
    {
        public string? Value { get; set; }
    }

    private sealed class HsCodeValue
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);
    }
}
