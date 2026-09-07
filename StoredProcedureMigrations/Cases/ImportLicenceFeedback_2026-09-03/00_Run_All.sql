:ON ERROR EXIT

PRINT 'Applying Import Licence feedback stored procedures to the current database.';
IF DB_NAME() <> N'TradeNetDB'
    THROW 51000, 'Wrong target database. Connect to TradeNetDB and run again.', 1;

PRINT 'Target database confirmed: TradeNetDB.';
GO

:r .\sp_ActualAmendReport_pagination.sql
:r .\sp_AmendReport_pagination.sql
:r .\sp_ImportLicenceListingCurrencyTotals.sql
:r .\sp_NewReport_pagination.sql
:r .\sp_VoucherReport_pagination.sql
:r .\sp_PendingReport_pagination.sql

PRINT 'Import Licence feedback stored procedures applied successfully.';
GO
