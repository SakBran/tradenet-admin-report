# Report API Build Guide For LLMs

Use this guide to create report APIs from the report mappings in `docs/ReportAndLinqMappingList.md`.

## Goal

Create one API controller for one report row.

- One row in `ReportAndLinqMappingList.md` equals one API controller.
- The report name comes only from `ReportAndLinqMappingList.md`.
- The stored procedure class comes from the mapped stored procedure in that same row.
- The query must use the existing LINQ converter in `Backend/StoredProcedureToLinq`.
- The paginated API response must use `ApiResult<T>` through `ReportQueryService`.
- The Excel API response must use `ExcelGenerator`.
- The controller must expose two POST endpoints:
  - `POST /api/{ControllerName}` for paginated JSON.
  - `POST /api/{ControllerName}/Excel` for `.xlsx` export.
- The controller file must be created under `Backend/Controllers/Report`.

Do not combine multiple report rows into one controller unless the user explicitly asks for that.

## Batch Execution Mode

When the user asks to build report APIs from this guide, do not stop after one report unless the user explicitly asks for only one.

Work continuously in the same session:

1. Pick the next `To Do` row from `docs/ReportAndLinqMappingList.md`.
2. Build that report controller with both endpoints.
3. Run `dotnet build Backend\API.csproj`.
4. If the build succeeds, mark the row `Completed` and update the completed/remaining counts.
5. Move immediately to the next `To Do` row.
6. Continue until there are no `To Do` rows left, a real blocker is found, or context/tool limits make it unsafe to continue.

Do not ask the user to repeat the command for each report. If a row is blocked, mark only that row `Blocked` with the reason, then continue with the next `To Do` row when possible.

## Required Files To Read First

Before creating an API, read these files:

1. `docs/ReportAndLinqMappingList.md`
   - Pick the next row whose `API Status` is `To Do`.
   - Copy the report name and stored procedure name exactly.

2. `Backend/StoredProcedureToLinq/{StoredProcedureName}.cs`
   - Confirm the LINQ converter file exists.
   - Read the request class, result class, and `Query(...)` method signature.
   - Do not guess request fields from the report name.

3. `Backend/Controllers/Report/MemberRegistrationReportController.cs`
   - Use this as the reference controller pattern.

4. `Backend/Model/ReportQueryRequest.cs`
   - Use this reusable base class for pagination, sorting, and filtering request fields.

5. `Backend/Service/Reports/ReportQueryService.cs`
   - Use this reusable helper to create paged `ApiResult<T>` from any `IQueryable<T>`.

6. `Backend/Service/Reports/ExcelGenerator.cs`
   - Use this reusable helper to generate `.xlsx` bytes from any `IQueryable<T>`.

7. `Backend/Model/APIResult.cs`
   - Confirm pagination, sorting, and filtering fields supported by `ApiResult<T>`.

8. `Backend/DBContext/TradeNetDbContext.cs`
   - Confirm the controller must inject `TradeNetDbContext`, not `ApplicationDbContext`.

## Anti-Hallucination Checks

Run these checks before writing a controller:

```powershell
$mapping = Select-String -Path docs\ReportAndLinqMappingList.md -Pattern 'dbo\.([A-Za-z0-9_]+)' |
    ForEach-Object { $_.Matches.Value -replace 'dbo\.', '' } |
    Sort-Object -Unique
$files = Get-ChildItem Backend\StoredProcedureToLinq -Filter *.cs |
    ForEach-Object { $_.BaseName }
$mapping | Where-Object { $_ -notin $files }
```

Expected output: no missing stored procedure names.

If a stored procedure name is missing:

- Do not invent a class.
- Check for a typo in `ReportAndLinqMappingList.md`.
- Check `docs/StoredProcedureToLinqTasks.md` for conversion status.
- If the LINQ converter truly does not exist, mark the row `Blocked` and write the reason.

Also verify the selected converter by searching for its actual types:

```powershell
rg "class|record|Query" Backend\StoredProcedureToLinq\{StoredProcedureName}.cs
```

Use the real class names and method signature found in that file.

## Controller Naming

Use the report name to create the controller name.

Rules:

- Remove spaces and punctuation.
- Keep business spelling from the report name, such as `Licence`.
- Append `Controller`.
- If the same report name appears in different modules, prefix the module name.

Example:

- Report name: `Member Registration Report`
- Controller: `MemberRegistrationReportController`
- File: `Backend/Controllers/Report/MemberRegistrationReportController.cs`
- Route: `POST /api/MemberRegistrationReport`
- Excel route: `POST /api/MemberRegistrationReport/Excel`

## Request Body Rules

Create a controller request DTO in the controller file and inherit from `ReportQueryRequest`.

The request DTO must contain:

- Every property required by the stored procedure LINQ request class.

Do not re-declare these fields in the controller request DTO because `ReportQueryRequest` already provides them:

- `PageIndex`
- `PageSize`
- `SortColumn`
- `SortOrder`
- `FilterColumn`
- `FilterQuery`

Do not add business filters that are not present in the LINQ converter request class.

If the LINQ `Query(...)` method has no request parameter, the API request DTO should contain only pagination/sort/filter fields.

## Controller Implementation Rules

Each controller must:

- Use `[ApiController]`.
- Use `[Route("api/[controller]")]`.
- Inject `TradeNetDbContext`.
- Use `[HttpPost]` for paginated JSON.
- Use `[HttpPost("Excel")]` for Excel export.
- Accept both request bodies with `[FromBody]`.
- Return `Task<ActionResult<ApiResult<TResult>>>` from the paginated endpoint.
- Return `Task<IActionResult>` from the Excel endpoint.
- Build the stored procedure request object from the POST body.
- Call the LINQ converter `Query(...)` method.
- Pass the returned `IQueryable<TResult>` into `ReportQueryService.CreatePagedResultAsync(...)` for paginated JSON.
- Pass the returned `IQueryable<TResult>` into `ExcelGenerator.CreateWorkbookAsync(...)` for Excel export.
- Return `Ok(result)` from the paginated endpoint.
- Return `File(fileBytes, ExcelGenerator.ContentType, "{ReportName}.xlsx")` from the Excel endpoint.

Do not call `ToList`, `AsEnumerable`, `Count`, `First`, or any other materializer in the controller before `ReportQueryService` or `ExcelGenerator`.

Do not use `FromSql`, `ExecuteSql`, raw SQL strings, or direct stored procedure calls.

## Required Validation

Add only validations that are supported by the real request fields:

- If the body is null, return `BadRequest("Request body is required.")`.
- For required `DateTime` fields, reject `default`.
- If both `FromDate` and `ToDate` exist, reject `ToDate < FromDate`.
- For known enum-like string fields, read allowed values from the LINQ converter or SQL definition before validating.
- Clamp `PageIndex` to zero or greater.
- Use default `PageSize = 10`.
- Clamp max `PageSize` to `1000`.

Do not invent allowed values. If allowed values are unclear, skip enum validation and let the LINQ converter handle no-match behavior.

## Template

Use this shape and replace names only after reading the real converter file:

```csharp
using System;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Report
{
    [ApiController]
    [Route("api/[controller]")]
    public class {ReportName}Controller : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public {ReportName}Controller(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<{ResultType}>>> Post([FromBody] {ReportName}Request? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = {StoredProcedureName}.Query(_context, procedureRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] {ReportName}Request? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = {StoredProcedureName}.Query(_context, procedureRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "{Report Name}");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "{ReportName}.xlsx");
        }

        private bool TryCreateReportRequest(
            {ReportName}Request? request,
            out {StoredProcedureName}Request? procedureRequest,
            out ActionResult? errorResult)
        {
            procedureRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            procedureRequest = new {StoredProcedureName}Request
            {
                // Copy real request properties here.
            };

            return true;
        }
    }

    public sealed class {ReportName}Request : ReportQueryRequest
    {
        // Copy real procedure request properties here.
    }
}
```

If the converter query does not use a request object:

- Make the controller request DTO inherit from `ReportQueryRequest`.
- Do not create a `{StoredProcedureName}Request` object.
- In both endpoints, call:

```csharp
var query = {StoredProcedureName}.Query(_context);
```

## Completion Checklist

Before marking a row complete:

1. Confirm the controller file exists under `Backend/Controllers/Report`.
2. Confirm it creates exactly one report API.
3. Confirm it uses `TradeNetDbContext`.
4. Confirm it calls the mapped LINQ converter.
5. Confirm it has both endpoints:
   - `[HttpPost]` paginated JSON endpoint.
   - `[HttpPost("Excel")]` Excel export endpoint.
6. Confirm it uses `ReportQueryService.CreatePagedResultAsync(...)`.
7. Confirm it uses `ExcelGenerator.CreateWorkbookAsync(...)`.
8. Run:

```powershell
dotnet build Backend\API.csproj
```

9. Only if the build succeeds, update `docs/ReportAndLinqMappingList.md`:
   - Change the report row `API Status` from `To Do` to `Completed`.
   - Increase `API completed` by `1`.
   - Decrease `API remaining` by `1`.

If the build fails, do not mark the row `Completed`.

If the API cannot be built because a required fact is missing, mark the row `Blocked` and add a short reason in the status cell.
