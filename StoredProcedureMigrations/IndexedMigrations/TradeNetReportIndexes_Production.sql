USE [TradeNetDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/*
    TradeNet report index rollout
    -----------------------------
    Run manually in SSMS or Azure Data Studio while connected to TradeNetDB.
    This script is safe to rerun. It creates each index separately with online,
    low-priority waiting and does not drop or rebuild existing indexes.
*/

PRINT N'============================================================';
PRINT N'TradeNet report index migration: preflight';
PRINT N'============================================================';

IF DB_NAME() <> N'TradeNetDB'
BEGIN
    THROW 50001, N'Abort: run this script only against TradeNetDB.', 1;
END;

DECLARE @EngineEdition int = CONVERT(int, SERVERPROPERTY(N'EngineEdition'));
DECLARE @Edition nvarchar(256) = CONVERT(nvarchar(256), SERVERPROPERTY(N'Edition'));

PRINT N'Database: ' + QUOTENAME(DB_NAME());
PRINT N'Edition: ' + COALESCE(@Edition, N'<unknown>');

-- Enterprise SQL Server, Azure SQL Database, and Azure SQL Managed Instance
-- support the required ONLINE index option.
IF @EngineEdition NOT IN (3, 5, 8)
BEGIN
    THROW 50002, N'Abort: this rollout requires ONLINE index creation. The connected SQL Server edition is not supported.', 1;
END;

DROP TABLE IF EXISTS #RequiredObjects;
CREATE TABLE #RequiredObjects
(
    ObjectName nvarchar(256) NOT NULL,
    ObjectType varchar(2) NOT NULL
);

INSERT INTO #RequiredObjects (ObjectName, ObjectType)
VALUES
    (N'dbo.PaThaKaPermitBusiness', N'U'),
    (N'dbo.ExportPermitItem', N'U'),
    (N'dbo.BorderImportPermitItem', N'U'),
    (N'dbo.BorderExportPermitItem', N'U'),
    (N'dbo.ImportLicence', N'U'),
    (N'dbo.ImportLicenceItem', N'U'),
    (N'dbo.BorderImportLicence', N'U'),
    (N'dbo.AccountTransaction', N'U'),
    (N'dbo.MPUPaymentTransaction', N'U'),
    (N'dbo.PaThaKaRegistration', N'U'),
    (N'dbo.sp_AccountSummaryReport_pagination', N'P'),
    (N'dbo.sp_ActualAmendReport_pagination', N'P'),
    (N'dbo.sp_AmendReport_pagination', N'P'),
    (N'dbo.sp_CancelReport_pagination', N'P'),
    (N'dbo.sp_CardListsByPaThaKaReport_pagination', N'P'),
    (N'dbo.sp_CompanyProfileReport_pagination', N'P'),
    (N'dbo.sp_DirectorListReport_pagination', N'P'),
    (N'dbo.sp_ExtensionReport_pagination', N'P'),
    (N'dbo.sp_HSCodeReport_pagination', N'P'),
    (N'dbo.sp_ImportLicenceDetailReport_pagination', N'P'),
    (N'dbo.sp_ImportLicencePendingDetailReport_pagination', N'P'),
    (N'dbo.sp_MPUReport_pagination', N'P'),
    (N'dbo.sp_MPUReport_V3_pagination', N'P'),
    (N'dbo.sp_NewReport_pagination', N'P'),
    (N'dbo.sp_PaThaKaAllReport_pagination', N'P'),
    (N'dbo.sp_PathakaBindReport_pagination', N'P'),
    (N'dbo.sp_PaThaKaByBusinessTypeReport_pagination', N'P'),
    (N'dbo.sp_PaThaKaRegistrationReport_pagination', N'P'),
    (N'dbo.sp_PaThaKaReport_pagination', N'P'),
    (N'dbo.sp_PaThaKaValidInvalidReport_pagination', N'P'),
    (N'dbo.sp_PendingReport_pagination', N'P'),
    (N'dbo.sp_VoucherReport_pagination', N'P'),
    (N'dbo.vw_ExportPermitItemTotalByCurrency', N'V'),
    (N'dbo.vw_ImportLicenceItemTotalByCurrency', N'V'),
    (N'dbo.vw_ImportPermitItemTotalByCurrency', N'V');

IF EXISTS
(
    SELECT 1
    FROM #RequiredObjects
    WHERE OBJECT_ID(ObjectName, ObjectType) IS NULL
)
BEGIN
    SELECT ObjectName, ObjectType
    FROM #RequiredObjects
    WHERE OBJECT_ID(ObjectName, ObjectType) IS NULL
    ORDER BY ObjectType, ObjectName;

    THROW 50003, N'Abort: required TradeNet report objects are missing. Review the result set.', 1;
END;

DROP TABLE IF EXISTS #RequiredColumns;
CREATE TABLE #RequiredColumns
(
    ObjectName nvarchar(256) NOT NULL,
    ColumnName sysname NOT NULL
);

INSERT INTO #RequiredColumns (ObjectName, ColumnName)
VALUES
    (N'dbo.PaThaKaPermitBusiness', N'PaThaKaId'),
    (N'dbo.PaThaKaPermitBusiness', N'PermitBUsinessId'),
    (N'dbo.ExportPermitItem', N'ExportPermitId'),
    (N'dbo.ExportPermitItem', N'HSCodeId'),
    (N'dbo.ExportPermitItem', N'ItemNo'),
    (N'dbo.ExportPermitItem', N'Id'),
    (N'dbo.ExportPermitItem', N'UniqueId'),
    (N'dbo.ExportPermitItem', N'HSCode'),
    (N'dbo.ExportPermitItem', N'HSYear'),
    (N'dbo.ExportPermitItem', N'Description'),
    (N'dbo.ExportPermitItem', N'UnitId'),
    (N'dbo.ExportPermitItem', N'Price'),
    (N'dbo.ExportPermitItem', N'Quantity'),
    (N'dbo.ExportPermitItem', N'Amount'),
    (N'dbo.ExportPermitItem', N'CurrencyId'),
    (N'dbo.ExportPermitItem', N'ParentId'),
    (N'dbo.ExportPermitItem', N'CheckId'),
    (N'dbo.ExportPermitItem', N'CreatedDate'),
    (N'dbo.BorderImportPermitItem', N'BorderImportPermitId'),
    (N'dbo.BorderImportPermitItem', N'HSCodeId'),
    (N'dbo.BorderImportPermitItem', N'ItemNo'),
    (N'dbo.BorderImportPermitItem', N'Id'),
    (N'dbo.BorderImportPermitItem', N'UniqueId'),
    (N'dbo.BorderImportPermitItem', N'HSCode'),
    (N'dbo.BorderImportPermitItem', N'HSYear'),
    (N'dbo.BorderImportPermitItem', N'Description'),
    (N'dbo.BorderImportPermitItem', N'UnitId'),
    (N'dbo.BorderImportPermitItem', N'Price'),
    (N'dbo.BorderImportPermitItem', N'Quantity'),
    (N'dbo.BorderImportPermitItem', N'Amount'),
    (N'dbo.BorderImportPermitItem', N'CurrencyId'),
    (N'dbo.BorderImportPermitItem', N'ParentId'),
    (N'dbo.BorderImportPermitItem', N'CheckId'),
    (N'dbo.BorderImportPermitItem', N'CreatedDate'),
    (N'dbo.BorderExportPermitItem', N'BorderExportPermitId'),
    (N'dbo.BorderExportPermitItem', N'HSCodeId'),
    (N'dbo.BorderExportPermitItem', N'ItemNo'),
    (N'dbo.BorderExportPermitItem', N'Id'),
    (N'dbo.BorderExportPermitItem', N'UniqueId'),
    (N'dbo.BorderExportPermitItem', N'HSCode'),
    (N'dbo.BorderExportPermitItem', N'HSYear'),
    (N'dbo.BorderExportPermitItem', N'Description'),
    (N'dbo.BorderExportPermitItem', N'UnitId'),
    (N'dbo.BorderExportPermitItem', N'Price'),
    (N'dbo.BorderExportPermitItem', N'Quantity'),
    (N'dbo.BorderExportPermitItem', N'Amount'),
    (N'dbo.BorderExportPermitItem', N'CurrencyId'),
    (N'dbo.BorderExportPermitItem', N'ParentId'),
    (N'dbo.BorderExportPermitItem', N'CheckId'),
    (N'dbo.BorderExportPermitItem', N'CreatedDate'),
    (N'dbo.ImportLicence', N'Status'),
    (N'dbo.ImportLicence', N'ApplicationDate'),
    (N'dbo.ImportLicence', N'ApplicationNo'),
    (N'dbo.ImportLicence', N'ApplyType'),
    (N'dbo.ImportLicence', N'CommodityType'),
    (N'dbo.ImportLicence', N'CreatedDate'),
    (N'dbo.ImportLicence', N'ImportLicenceNo'),
    (N'dbo.ImportLicence', N'Id'),
    (N'dbo.ImportLicence', N'OldImportLicenceNo'),
    (N'dbo.ImportLicence', N'LastDate'),
    (N'dbo.ImportLicence', N'PaThaKaId'),
    (N'dbo.ImportLicence', N'ExportImportSectionId'),
    (N'dbo.ImportLicence', N'auto'),
    (N'dbo.ImportLicence', N'quota'),
    (N'dbo.ImportLicenceItem', N'ImportLicenceId'),
    (N'dbo.ImportLicenceItem', N'CurrencyId'),
    (N'dbo.ImportLicenceItem', N'HSCodeId'),
    (N'dbo.ImportLicenceItem', N'Amount'),
    (N'dbo.BorderImportLicence', N'Status'),
    (N'dbo.BorderImportLicence', N'ApplicationDate'),
    (N'dbo.BorderImportLicence', N'ApplicationNo'),
    (N'dbo.BorderImportLicence', N'ApplyType'),
    (N'dbo.BorderImportLicence', N'CardType'),
    (N'dbo.BorderImportLicence', N'PaThaKaId'),
    (N'dbo.BorderImportLicence', N'ExportImportSectionId'),
    (N'dbo.BorderImportLicence', N'SakhanId'),
    (N'dbo.AccountTransaction', N'IsPayment'),
    (N'dbo.AccountTransaction', N'PaymentDate'),
    (N'dbo.AccountTransaction', N'VoucherDate'),
    (N'dbo.AccountTransaction', N'TransactionId'),
    (N'dbo.AccountTransaction', N'TransactionFormType'),
    (N'dbo.AccountTransaction', N'PaymentType'),
    (N'dbo.AccountTransaction', N'VoucherNo'),
    (N'dbo.MPUPaymentTransaction', N'TransactionRefNo'),
    (N'dbo.MPUPaymentTransaction', N'TransactionId'),
    (N'dbo.MPUPaymentTransaction', N'ApplicationNo'),
    (N'dbo.MPUPaymentTransaction', N'ApprovalCode'),
    (N'dbo.MPUPaymentTransaction', N'ResponseCode'),
    (N'dbo.MPUPaymentTransaction', N'TransactionDateTime'),
    (N'dbo.MPUPaymentTransaction', N'PaymentType'),
    (N'dbo.PaThaKaRegistration', N'CompanyRegistrationNo'),
    (N'dbo.PaThaKaRegistration', N'ApplyType'),
    (N'dbo.PaThaKaRegistration', N'Status'),
    (N'dbo.PaThaKaRegistration', N'ApplicationNo'),
    (N'dbo.PaThaKaRegistration', N'CompanyName'),
    (N'dbo.PaThaKaRegistration', N'ApproveDate');

IF EXISTS
(
    SELECT 1
    FROM #RequiredColumns
    WHERE COL_LENGTH(ObjectName, ColumnName) IS NULL
)
BEGIN
    SELECT ObjectName, ColumnName
    FROM #RequiredColumns
    WHERE COL_LENGTH(ObjectName, ColumnName) IS NULL
    ORDER BY ObjectName, ColumnName;

    THROW 50004, N'Abort: required TradeNet report columns are missing. Review the result set.', 1;
END;

IF EXISTS
(
    SELECT Expected.ViewName, Expected.IndexName
    FROM
    (
        VALUES
            (N'dbo.vw_ExportPermitItemTotalByCurrency', N'IX_vw_ExportPermitItemTotalByCurrency'),
            (N'dbo.vw_ImportLicenceItemTotalByCurrency', N'IX_vw_ImportLicenceItemTotalByCurrency'),
            (N'dbo.vw_ImportPermitItemTotalByCurrency', N'IX_vw_ImportPermitItemTotalByCurrency')
    ) AS Expected(ViewName, IndexName)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes AS Existing
        WHERE Existing.object_id = OBJECT_ID(Expected.ViewName, N'V')
          AND Existing.name = Expected.IndexName
          AND Existing.type = 1
          AND Existing.is_unique = 1
          AND Existing.is_disabled = 0
    )
)
BEGIN
    THROW 50005, N'Abort: a required report indexed view index is missing or disabled.', 1;
END;

PRINT N'Preflight passed.';
GO

DROP TABLE IF EXISTS #IndexCandidates;
CREATE TABLE #IndexCandidates
(
    CandidateId int NOT NULL PRIMARY KEY,
    BatchNo int NOT NULL,
    SchemaName sysname NOT NULL,
    TableName sysname NOT NULL,
    IndexName sysname NOT NULL,
    KeyColumnsSql nvarchar(max) NOT NULL,
    IncludeColumnsSql nvarchar(max) NOT NULL
);

DROP TABLE IF EXISTS #IndexCandidateKeys;
CREATE TABLE #IndexCandidateKeys
(
    CandidateId int NOT NULL,
    KeyOrdinal int NOT NULL,
    ColumnName sysname NOT NULL,
    PRIMARY KEY (CandidateId, KeyOrdinal)
);

DROP TABLE IF EXISTS #IndexCandidateCoverage;
CREATE TABLE #IndexCandidateCoverage
(
    CandidateId int NOT NULL,
    ColumnName sysname NOT NULL,
    PRIMARY KEY (CandidateId, ColumnName)
);

DROP TABLE IF EXISTS #MigrationResults;
CREATE TABLE #MigrationResults
(
    CandidateId int NOT NULL,
    BatchNo int NOT NULL,
    TableName sysname NOT NULL,
    RequestedIndexName sysname NOT NULL,
    InstalledIndexName sysname NOT NULL,
    MigrationAction nvarchar(80) NOT NULL
);

INSERT INTO #IndexCandidates
(
    CandidateId,
    BatchNo,
    SchemaName,
    TableName,
    IndexName,
    KeyColumnsSql,
    IncludeColumnsSql
)
VALUES
    (1, 1, N'dbo', N'PaThaKaPermitBusiness', N'IX_PaThaKaPermitBusiness_PaThaKaId',
        N'[PaThaKaId]',
        N'[PermitBUsinessId]'),
    (2, 1, N'dbo', N'ExportPermitItem', N'IX_ExportPermitItem_ReportCover',
        N'[ExportPermitId], [HSCodeId], [ItemNo]',
        N'[Id], [UniqueId], [HSCode], [HSYear], [Description], [UnitId], [Price], [Quantity], [Amount], [CurrencyId], [ParentId], [CheckId], [CreatedDate]'),
    (3, 1, N'dbo', N'BorderImportPermitItem', N'IX_BorderImportPermitItem_ReportCover',
        N'[BorderImportPermitId], [HSCodeId], [ItemNo]',
        N'[Id], [UniqueId], [HSCode], [HSYear], [Description], [UnitId], [Price], [Quantity], [Amount], [CurrencyId], [ParentId], [CheckId], [CreatedDate]'),
    (4, 1, N'dbo', N'BorderExportPermitItem', N'IX_BorderExportPermitItem_ReportCover',
        N'[BorderExportPermitId], [HSCodeId], [ItemNo]',
        N'[Id], [UniqueId], [HSCode], [HSYear], [Description], [UnitId], [Price], [Quantity], [Amount], [CurrencyId], [ParentId], [CheckId], [CreatedDate]'),
    (5, 2, N'dbo', N'ImportLicence', N'IX_ImportLicence_PendingReport',
        N'[Status], [ApplicationDate], [ApplicationNo]',
        N'[ApplyType], [CommodityType], [PaThaKaId], [ExportImportSectionId]'),
    (6, 3, N'dbo', N'BorderImportLicence', N'IX_BorderImportLicence_PendingReport',
        N'[Status], [ApplicationDate], [ApplicationNo]',
        N'[ApplyType], [CardType], [PaThaKaId], [ExportImportSectionId], [SakhanId]'),
    (7, 3, N'dbo', N'AccountTransaction', N'IX_AccountTransaction_PaymentReport',
        N'[IsPayment], [PaymentDate], [TransactionId]',
        N'[PaymentType]'),
    (8, 3, N'dbo', N'MPUPaymentTransaction', N'IX_MPUPaymentTransaction_TransactionRefNo',
        N'[TransactionRefNo]',
        N'[TransactionId], [ApplicationNo], [ApprovalCode], [ResponseCode], [TransactionDateTime], [PaymentType]'),
    (9, 3, N'dbo', N'PaThaKaRegistration', N'IX_PaThaKaRegistration_CompanyProfile',
        N'[CompanyRegistrationNo], [ApplyType], [Status]',
        N'[ApplicationNo], [CompanyName], [ApproveDate]'),
    (10, 4, N'dbo', N'AccountTransaction', N'IX_AccountTransaction_OnlineFees',
        N'[IsPayment], [VoucherDate]',
        N'[TransactionId], [TransactionFormType], [PaymentDate], [PaymentType], [VoucherNo]'),
    (11, 3, N'dbo', N'ImportLicence', N'IX_ImportLicence_NewReport',
        N'[Status], [ApplyType], [CreatedDate], [ImportLicenceNo]',
        N'[Id], [ExportImportSectionId], [PaThaKaId], [OldImportLicenceNo], [LastDate], [auto], [quota], [CommodityType]'),
    (12, 3, N'dbo', N'ImportLicenceItem', N'IX_ImportLicenceItem_NewReport_Licence',
        N'[ImportLicenceId]',
        N'[CurrencyId], [HSCodeId], [Amount]');

INSERT INTO #IndexCandidateKeys (CandidateId, KeyOrdinal, ColumnName)
VALUES
    (1, 1, N'PaThaKaId'),
    (2, 1, N'ExportPermitId'),
    (2, 2, N'HSCodeId'),
    (2, 3, N'ItemNo'),
    (3, 1, N'BorderImportPermitId'),
    (3, 2, N'HSCodeId'),
    (3, 3, N'ItemNo'),
    (4, 1, N'BorderExportPermitId'),
    (4, 2, N'HSCodeId'),
    (4, 3, N'ItemNo'),
    (5, 1, N'Status'),
    (5, 2, N'ApplicationDate'),
    (5, 3, N'ApplicationNo'),
    (6, 1, N'Status'),
    (6, 2, N'ApplicationDate'),
    (6, 3, N'ApplicationNo'),
    (7, 1, N'IsPayment'),
    (7, 2, N'PaymentDate'),
    (7, 3, N'TransactionId'),
    (8, 1, N'TransactionRefNo'),
    (9, 1, N'CompanyRegistrationNo'),
    (9, 2, N'ApplyType'),
    (9, 3, N'Status'),
    (10, 1, N'IsPayment'),
    (10, 2, N'VoucherDate'),
    (11, 1, N'Status'),
    (11, 2, N'ApplyType'),
    (11, 3, N'CreatedDate'),
    (11, 4, N'ImportLicenceNo'),
    (12, 1, N'ImportLicenceId');

INSERT INTO #IndexCandidateCoverage (CandidateId, ColumnName)
VALUES
    (1, N'PermitBUsinessId'),
    (2, N'Id'),
    (2, N'UniqueId'),
    (2, N'HSCode'),
    (2, N'HSYear'),
    (2, N'Description'),
    (2, N'UnitId'),
    (2, N'Price'),
    (2, N'Quantity'),
    (2, N'Amount'),
    (2, N'CurrencyId'),
    (2, N'ParentId'),
    (2, N'CheckId'),
    (2, N'CreatedDate'),
    (3, N'Id'),
    (3, N'UniqueId'),
    (3, N'HSCode'),
    (3, N'HSYear'),
    (3, N'Description'),
    (3, N'UnitId'),
    (3, N'Price'),
    (3, N'Quantity'),
    (3, N'Amount'),
    (3, N'CurrencyId'),
    (3, N'ParentId'),
    (3, N'CheckId'),
    (3, N'CreatedDate'),
    (4, N'Id'),
    (4, N'UniqueId'),
    (4, N'HSCode'),
    (4, N'HSYear'),
    (4, N'Description'),
    (4, N'UnitId'),
    (4, N'Price'),
    (4, N'Quantity'),
    (4, N'Amount'),
    (4, N'CurrencyId'),
    (4, N'ParentId'),
    (4, N'CheckId'),
    (4, N'CreatedDate'),
    (5, N'ApplyType'),
    (5, N'CommodityType'),
    (5, N'PaThaKaId'),
    (5, N'ExportImportSectionId'),
    (6, N'ApplyType'),
    (6, N'CardType'),
    (6, N'PaThaKaId'),
    (6, N'ExportImportSectionId'),
    (6, N'SakhanId'),
    (7, N'PaymentType'),
    (8, N'TransactionId'),
    (8, N'ApplicationNo'),
    (8, N'ApprovalCode'),
    (8, N'ResponseCode'),
    (8, N'TransactionDateTime'),
    (8, N'PaymentType'),
    (9, N'ApplicationNo'),
    (9, N'CompanyName'),
    (9, N'ApproveDate'),
    (10, N'TransactionId'),
    (10, N'TransactionFormType'),
    (10, N'PaymentDate'),
    (10, N'PaymentType'),
    (10, N'VoucherNo'),
    (11, N'Id'),
    (11, N'ExportImportSectionId'),
    (11, N'PaThaKaId'),
    (11, N'OldImportLicenceNo'),
    (11, N'LastDate'),
    (11, N'auto'),
    (11, N'quota'),
    (11, N'CommodityType'),
    (12, N'CurrencyId'),
    (12, N'HSCodeId'),
    (12, N'Amount');
GO

/*
    Batch 1: clear join gaps
    Batch 2: pending report
    Batch 3: proactive future-capacity indexes
    Batch 4: online-fees hot-path cover

    The semantic guard accepts an existing index when it has the requested key
    columns as a left prefix and physically covers every requested projection
    column, even if the installed index has a different name or extra columns.
*/
PRINT N'============================================================';
PRINT N'TradeNet report index migration: batch 1 - clear join gaps';
PRINT N'============================================================';

DECLARE
    @CandidateId int,
    @BatchNo int,
    @PreviousBatchNo int = 1,
    @SchemaName sysname,
    @TableName sysname,
    @IndexName sysname,
    @KeyColumnsSql nvarchar(max),
    @IncludeColumnsSql nvarchar(max),
    @EquivalentIndexName sysname,
    @ObjectId int,
    @CreateSql nvarchar(max),
    @ErrorMessage nvarchar(2048);

DECLARE CandidateCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT
        CandidateId,
        BatchNo,
        SchemaName,
        TableName,
        IndexName,
        KeyColumnsSql,
        IncludeColumnsSql
    FROM #IndexCandidates
    ORDER BY BatchNo, CandidateId;

OPEN CandidateCursor;

FETCH NEXT FROM CandidateCursor
INTO @CandidateId, @BatchNo, @SchemaName, @TableName, @IndexName, @KeyColumnsSql, @IncludeColumnsSql;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF @BatchNo <> @PreviousBatchNo
    BEGIN
        PRINT N'============================================================';
        PRINT N'TradeNet report index migration: batch '
            + CONVERT(nvarchar(10), @BatchNo)
            + N' - '
            + CASE @BatchNo
                WHEN 2 THEN N'pending report'
                WHEN 3 THEN N'proactive future capacity'
                WHEN 4 THEN N'online-fees hot-path cover'
                ELSE N'additional indexes'
              END;
        PRINT N'============================================================';
        SET @PreviousBatchNo = @BatchNo;
    END;

    SET @ObjectId = OBJECT_ID(QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName), N'U');
    SET @EquivalentIndexName = NULL;

    SELECT TOP (1)
        @EquivalentIndexName = Existing.name
    FROM sys.indexes AS Existing
    WHERE Existing.object_id = @ObjectId
      AND Existing.type IN (1, 2)
      AND Existing.is_disabled = 0
      AND Existing.is_hypothetical = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM #IndexCandidateKeys AS RequestedKey
          WHERE RequestedKey.CandidateId = @CandidateId
            AND NOT EXISTS
            (
                SELECT 1
                FROM sys.index_columns AS ExistingColumn
                JOIN sys.columns AS ColumnDefinition
                  ON ColumnDefinition.object_id = ExistingColumn.object_id
                 AND ColumnDefinition.column_id = ExistingColumn.column_id
                WHERE ExistingColumn.object_id = Existing.object_id
                  AND ExistingColumn.index_id = Existing.index_id
                  AND ExistingColumn.key_ordinal = RequestedKey.KeyOrdinal
                  AND ColumnDefinition.name = RequestedKey.ColumnName
            )
      )
      AND
      (
          Existing.type = 1
          OR NOT EXISTS
          (
              SELECT 1
              FROM #IndexCandidateCoverage AS RequestedCoverage
              WHERE RequestedCoverage.CandidateId = @CandidateId
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM sys.index_columns AS ExistingColumn
                    JOIN sys.columns AS ColumnDefinition
                      ON ColumnDefinition.object_id = ExistingColumn.object_id
                     AND ColumnDefinition.column_id = ExistingColumn.column_id
                    WHERE ExistingColumn.object_id = Existing.object_id
                      AND ExistingColumn.index_id = Existing.index_id
                      AND ColumnDefinition.name = RequestedCoverage.ColumnName
                )
          )
      )
    ORDER BY
        CASE WHEN Existing.name = @IndexName THEN 0 ELSE 1 END,
        Existing.index_id;

    IF @EquivalentIndexName IS NOT NULL
    BEGIN
        PRINT N'SKIP: ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName)
            + N' already has equivalent index ' + QUOTENAME(@EquivalentIndexName) + N'.';

        INSERT INTO #MigrationResults
        (
            CandidateId,
            BatchNo,
            TableName,
            RequestedIndexName,
            InstalledIndexName,
            MigrationAction
        )
        VALUES
        (
            @CandidateId,
            @BatchNo,
            @TableName,
            @IndexName,
            @EquivalentIndexName,
            N'SKIPPED - equivalent index exists'
        );
    END
    ELSE
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = @ObjectId
              AND name = @IndexName
        )
        BEGIN
            SET @ErrorMessage = N'Abort: index ' + QUOTENAME(@IndexName)
                + N' exists on ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName)
                + N' but does not satisfy the required left-prefix covering definition.';

            THROW 50006, @ErrorMessage, 1;
        END;

        SET @CreateSql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@IndexName)
            + N' ON ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName)
            + N' (' + @KeyColumnsSql + N')'
            + N' INCLUDE (' + @IncludeColumnsSql + N')'
            + N' WITH (ONLINE = ON (WAIT_AT_LOW_PRIORITY (MAX_DURATION = 5 MINUTES, ABORT_AFTER_WAIT = SELF)));';

        PRINT N'CREATE: ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName)
            + N'.' + QUOTENAME(@IndexName) + N'.';

        EXEC sys.sp_executesql @CreateSql;

        INSERT INTO #MigrationResults
        (
            CandidateId,
            BatchNo,
            TableName,
            RequestedIndexName,
            InstalledIndexName,
            MigrationAction
        )
        VALUES
        (
            @CandidateId,
            @BatchNo,
            @TableName,
            @IndexName,
            @IndexName,
            N'CREATED'
        );
    END;

    FETCH NEXT FROM CandidateCursor
    INTO @CandidateId, @BatchNo, @SchemaName, @TableName, @IndexName, @KeyColumnsSql, @IncludeColumnsSql;
END;

CLOSE CandidateCursor;
DEALLOCATE CandidateCursor;
GO

PRINT N'============================================================';
PRINT N'TradeNet report index migration: postflight';
PRINT N'============================================================';

SELECT
    Result.BatchNo,
    Result.TableName,
    Result.RequestedIndexName,
    Result.InstalledIndexName,
    Result.MigrationAction
FROM #MigrationResults AS Result
ORDER BY Result.BatchNo, Result.CandidateId;

SELECT
    SchemaDefinition.name AS SchemaName,
    TableDefinition.name AS TableName,
    IndexDefinition.name AS InstalledIndexName,
    IndexDefinition.type_desc AS IndexType,
    STUFF
    (
        (
            SELECT N', ' + QUOTENAME(ColumnDefinition.name)
            FROM sys.index_columns AS IndexColumn
            JOIN sys.columns AS ColumnDefinition
              ON ColumnDefinition.object_id = IndexColumn.object_id
             AND ColumnDefinition.column_id = IndexColumn.column_id
            WHERE IndexColumn.object_id = IndexDefinition.object_id
              AND IndexColumn.index_id = IndexDefinition.index_id
              AND IndexColumn.key_ordinal > 0
            ORDER BY IndexColumn.key_ordinal
            FOR XML PATH(N''), TYPE
        ).value(N'.', N'nvarchar(max)'),
        1,
        2,
        N''
    ) AS KeyColumns,
    STUFF
    (
        (
            SELECT N', ' + QUOTENAME(ColumnDefinition.name)
            FROM sys.index_columns AS IndexColumn
            JOIN sys.columns AS ColumnDefinition
              ON ColumnDefinition.object_id = IndexColumn.object_id
             AND ColumnDefinition.column_id = IndexColumn.column_id
            WHERE IndexColumn.object_id = IndexDefinition.object_id
              AND IndexColumn.index_id = IndexDefinition.index_id
              AND IndexColumn.is_included_column = 1
            ORDER BY IndexColumn.index_column_id
            FOR XML PATH(N''), TYPE
        ).value(N'.', N'nvarchar(max)'),
        1,
        2,
        N''
    ) AS IncludedColumns
FROM #MigrationResults AS Result
JOIN sys.tables AS TableDefinition
  ON TableDefinition.name = Result.TableName
JOIN sys.schemas AS SchemaDefinition
  ON SchemaDefinition.schema_id = TableDefinition.schema_id
 AND SchemaDefinition.name = N'dbo'
JOIN sys.indexes AS IndexDefinition
  ON IndexDefinition.object_id = TableDefinition.object_id
 AND IndexDefinition.name = Result.InstalledIndexName
ORDER BY Result.BatchNo, Result.CandidateId;

PRINT N'Postflight complete. Review the result sets before closing this window.';
GO

/*
    Manual rollback reference only.
    Never run rollback statements without DBA review and an approved change.

    DROP INDEX IF EXISTS [IX_ImportLicenceItem_NewReport_Licence] ON [dbo].[ImportLicenceItem];
    DROP INDEX IF EXISTS [IX_ImportLicence_NewReport] ON [dbo].[ImportLicence];
    DROP INDEX IF EXISTS [IX_AccountTransaction_OnlineFees] ON [dbo].[AccountTransaction];
    DROP INDEX IF EXISTS [IX_PaThaKaRegistration_CompanyProfile] ON [dbo].[PaThaKaRegistration];
    DROP INDEX IF EXISTS [IX_MPUPaymentTransaction_TransactionRefNo] ON [dbo].[MPUPaymentTransaction];
    DROP INDEX IF EXISTS [IX_AccountTransaction_PaymentReport] ON [dbo].[AccountTransaction];
    DROP INDEX IF EXISTS [IX_BorderImportLicence_PendingReport] ON [dbo].[BorderImportLicence];
    DROP INDEX IF EXISTS [IX_ImportLicence_PendingReport] ON [dbo].[ImportLicence];
    DROP INDEX IF EXISTS [IX_BorderExportPermitItem_ReportCover] ON [dbo].[BorderExportPermitItem];
    DROP INDEX IF EXISTS [IX_BorderImportPermitItem_ReportCover] ON [dbo].[BorderImportPermitItem];
    DROP INDEX IF EXISTS [IX_ExportPermitItem_ReportCover] ON [dbo].[ExportPermitItem];
    DROP INDEX IF EXISTS [IX_PaThaKaPermitBusiness_PaThaKaId] ON [dbo].[PaThaKaPermitBusiness];
*/
