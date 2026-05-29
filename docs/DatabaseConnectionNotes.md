# Database Connection Notes

Updated: 2026-05-29

Use `Backend/appsettings.json` -> `ConnectionStrings:TradeNetDBTest` for report database checks, stored procedure comparisons, and LINQ verification against the TradeNet database.

## TradeNetDBTest

- Server: `203.81.66.111,14330`
- Database: `TradeNetDB`
- User: `sa`
- Password: stored in `Backend/appsettings.json`; do not copy it into docs, logs, commits, or summaries.
- Required options currently used by the app: `MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True`

## Usage Note

For local investigation scripts, read the connection string from `Backend/appsettings.json` instead of hardcoding credentials again.

```powershell
$connectionString = (Get-Content 'Backend/appsettings.json' | ConvertFrom-Json).ConnectionStrings.TradeNetDBTest
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionString)

# Example: use $connectionString with SqlConnection in PowerShell/C# scripts.
```

