# Database Connection Notes

Updated: 2026-09-30

Use `Backend/appsettings.json` -> `ConnectionStrings:TradeNetDBTest` for report database checks, stored procedure comparisons, and LINQ verification against the TradeNet database.

## TradeNetDBTest

- Server: `tn2db.myanmartradenet.com,14133`
- Database: `TradeNetDB`
- User: `tn2db`
- Password: stored in `Backend/appsettings.json`; do not copy it into docs, logs, commits, or summaries.
- Required options currently used by the app: `MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True`

## Usage Note

For local investigation scripts, read the connection string from `Backend/appsettings.json` instead of hardcoding credentials again.

```powershell
$connectionString = (Get-Content 'Backend/appsettings.json' | ConvertFrom-Json).ConnectionStrings.TradeNetDBTest
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionString)

# Example: use $connectionString with SqlConnection in PowerShell/C# scripts.
```

## Stored Procedure Deployment

Use **Windows Authentication** when deploying or replacing stored procedures on `TradeNetDB`. Connect to `tn2db.myanmartradenet.com,14133`, select `TradeNetDB`, and verify that SQL Server reports the target instance as `tn2db\PRODUCTION` before making changes.

The `tn2db` SQL login stored in `Backend/appsettings.json` is for application access and read-only verification. It does not have `VIEW DEFINITION` or `ALTER` permission on stored procedures and must not be used for deployments. The Windows account performing a deployment must have those permissions on the target procedure.

