Run the updated stored-procedure files on your SQL Server to apply Border Export Permit fixes.

Files to apply (already edited in the repository):
- StoredProcedureMigrations/sp_VoucherReport_pagination.sql
- StoredProcedureMigrations/sp_ExtensionReport_pagination.sql
- StoredProcedureMigrations/sp_CancelReport_pagination.sql
- StoredProcedureMigrations/sp_NewReport_pagination.sql

Example using `sqlcmd` (Windows):

```powershell
$sqlServer = "<SERVER_NAME>\<INSTANCE>"
$database = "<DATABASE_NAME>"
$sqlFiles = @(
  "StoredProcedureMigrations\sp_VoucherReport_pagination.sql",
  "StoredProcedureMigrations\sp_ExtensionReport_pagination.sql",
  "StoredProcedureMigrations\sp_CancelReport_pagination.sql",
  "StoredProcedureMigrations\sp_NewReport_pagination.sql"
)

foreach ($f in $sqlFiles) {
  Write-Host "Applying: $f"
  sqlcmd -S $sqlServer -d $database -i $f -b
}
```

Notes:
- Replace `<SERVER_NAME>\<INSTANCE>` and `<DATABASE_NAME>` with your DB connection.
- `-b` makes `sqlcmd` exit on error; remove if you prefer to inspect errors interactively.
- After applying, test the API endpoint that calls `sp_VoucherReport_pagination` for `FormType='Border Export Permit'`.

If you want, I can:
- Produce a single combined SQL script to run once, or
- Attempt to run these commands here if you provide a connection string (not recommended in chat).