# TaskForLLM

This document tracks the completed work for supporting two EF Core databases in the backend.

## Completion Status

1. [x] Add named connection strings
   - `Backend/appsettings.json` now includes:
     - `DefaultConnection`
     - `TemplateDB`
     - `TradeNetDBTest`
   - `DefaultConnection` remains pointed at `TemplateDB` for compatibility.
   - Local development connection strings use trusted SQL Server authentication.

2. [x] Confirm EF tooling
   - `dotnet-ef` is installed.
   - Tooling was updated to match the EF Core package version:
     - `dotnet ef --version`
     - Verified version: `9.0.6`

3. [x] Scaffold `TradeNetDBTest` into a second context
   - Scaffold was run from `Backend`.
   - Generated context:
     - `Backend/DBContext/TradeNetDbContext.cs`
   - Generated TradeNet models:
     - `Backend/Model/TradeNet/*.cs`
   - Generated model count: `227`
   - Scaffold output was isolated in the `API.Model.TradeNet` namespace to avoid mixing with TemplateDB models.
   - Scaffold used `--no-onconfiguring` so the connection string stays in application configuration.

   Final scaffold command shape:

   ```powershell
   dotnet ef dbcontext scaffold "Server=localhost;Database=TradeNetDBTest;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;" Microsoft.EntityFrameworkCore.SqlServer --output-dir Model/TradeNet --context-dir DBContext --context TradeNetDbContext --namespace API.Model.TradeNet --context-namespace API.DBContext --data-annotations --no-onconfiguring --force
   ```

4. [x] Register both DbContexts in `Program.cs`
   - `ApplicationDbContext` uses `TemplateDB`.
   - `TradeNetDbContext` uses `TradeNetDBTest`.

5. [x] Keep database contexts isolated
   - Existing TemplateDB controllers and services continue to inject `ApplicationDbContext`.
   - `TradeNetDbContext` is registered and ready for TradeNetDBTest-specific controllers/services.
   - No existing controller currently needs `TradeNetDbContext`; future TradeNet features should inject it directly instead of reusing `ApplicationDbContext`.

6. [x] Build and verify
   - `dotnet build` was run in `Backend`.
   - Build result: succeeded with `0` warnings and `0` errors after the final verification.

7. [x] Test database access
   - Verified both local databases are reachable through SQL Server:
     - `TemplateDB`
     - `TradeNetDBTest`
   - Applied existing `ApplicationDbContext` migrations to `TemplateDB`.
   - Verified `TemplateDB.dbo.Users` is queryable.
   - Verified `TradeNetDBTest` contains `227` base tables.

8. [x] Optional cleanup
   - Kept `DefaultConnection` for compatibility.
   - Removed hardcoded SQL password usage from `Backend/appsettings.json` connection strings for local development.
   - Added root `.gitignore` coverage for .NET backend and React frontend generated files.

## Notes For Future AI Instructions

- Keep AI-facing task instructions in the `docs` folder from now on.
- Keep `ApplicationDbContext` and `TradeNetDbContext` isolated.
- Use explicit connection names so the database target is clear.
- Do not put database passwords or production secrets in task files or committed appsettings files.
- For future TradeNetDBTest features, inject `TradeNetDbContext` in the relevant controller or service.
