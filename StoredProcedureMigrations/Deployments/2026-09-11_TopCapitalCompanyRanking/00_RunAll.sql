/* =====================================================================================
   List of Top Capital Company ranking deployment - 2026-09-11
   Run this ONE file to apply the procedure, or run 01 individually.
   Either way: PROCEDURE FIRST, APPLICATION SECOND.

   Target database: TradeNetDB  (NOT ReportTemplateDB - that one only holds the Excel
   export job queue; deploying report procedures into it is a known trap.)

   What changes: one entry in the @SortColumn whitelist of sp_PaThaKaReport_pagination,

       WHEN 'Capital'                 THEN 'PaThaKa.Capital'

   Why it matters: the old admin's List of Top Capital Company ranked the whole result
   set in C# (.OrderByDescending(x => x.Capital).Take(model.TotalRecords), the "No of
   List" box) before showing it. The rewritten report has no such filter and could not
   rank by capital at all: 'Capital' was not whitelisted, so the procedure fell through
   to ELSE 'PaThaKa.IssuedDate'. This release adds the entry; the controller then asks
   for ORDER BY Capital DESC + FETCH NEXT @TotalRecords and the report is a top-capital
   report again.

   RUN THIS BEFORE SHIPPING THE APPLICATION. Against a stale procedure the new
   controller still returns N rows, but they are the N most recently issued companies
   rather than the N largest by capital - wrong in a way nothing on screen reveals.

   Deliberately untouched: the WHERE clause, the projection, the OFFSET/FETCH paging and
   every other whitelist entry, so the report that shares this procedure
   (PaThaKaRegisteredBusinessOrganizationReport) is unaffected.

   Generated from the repository files of the same name; see README.md in this folder.
   ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [TradeNetDB];
GO

-- ============================================================================
-- sp_PaThaKaReport_pagination   (file 01_sp_PaThaKaReport_pagination.sql)
-- ============================================================================
PRINT N'Applying sp_PaThaKaReport_pagination ...';
GO

/* =============================================
   sp_PaThaKaReport_pagination

   NEW stored procedure. Does NOT modify the existing dbo.sp_PaThaKaReport.
   It is a pagination-aware copy used by the converted report APIs:
     - PaThaKaRegisteredBusinessOrganizationReportController
     - ListOfTopCapitalCompanyController

   Source procedure: dbo.sp_PaThaKaReport (left untouched).

   Behaviour:
   - @PageSize NULL or <= 0  -> returns ALL matching rows (used by Excel export).
   - @PageSize > 0           -> returns one page using OFFSET/FETCH.
   - Every row carries TotalCount (COUNT(*) OVER()) = total matching rows
     across all pages, so the caller builds paging metadata in one trip.
   - @SortColumn is validated against a whitelist; anything unknown falls back
     to the source procedure's IssuedDate ordering. CompanyRegistrationNo is the
     deterministic tie-breaker so paging is stable.

   Deployment:
   - Production: run this script through the normal release process.
   - Development: this is also applied directly to the dev database and tested
     (the connection in Backend/appsettings.json -> "TradeNetDBTest").

   Idempotent: CREATE OR ALTER so re-running is safe.
   ============================================= */
--exec sp_PaThaKaReport_pagination '2020-07-01','2020-07-28',0,0,'','',NULL,NULL,0,10
CREATE OR ALTER PROCEDURE [dbo].[sp_PaThaKaReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @BusinessTypeId int,
    @LineofBusinessId int,
    @State nvarchar(200),
    @Status nvarchar(50),
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Whitelist the sort column to a known output column; default to IssuedDate.
    DECLARE @OrderBy nvarchar(200) =
        CASE @SortColumn
            WHEN 'CompanyRegistrationNo'   THEN 'PaThaKa.CompanyRegistrationNo'
            WHEN 'CompanyName'             THEN 'PaThaKa.CompanyName'
            WHEN 'CompanyRegistrationDate' THEN 'PaThaKa.CompanyRegistrationDate'
            WHEN 'EndDate'                 THEN 'PaThaKa.EndDate'
            WHEN 'BusinessType'            THEN 'businessType.Name'
            WHEN 'LineofBusiness'          THEN 'lineofBusiness.Name'
            WHEN 'State'                   THEN 'PaThaKa.State'
            WHEN 'MICPermitNo'             THEN 'PaThaKa.MICPermitNo'
            -- ListOfTopCapitalCompany ranks by capital to pick its top N ("No of List").
            -- Without this entry the report silently fell back to IssuedDate ordering and
            -- was not a top-capital report at all.
            WHEN 'Capital'                 THEN 'PaThaKa.Capital'
            ELSE 'PaThaKa.IssuedDate'
        END;

    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @Sql nvarchar(max) = N'
        SELECT CompanyRegistrationNo, CompanyName, CompanyRegistrationDate, EndDate,
               businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
               UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country,
               PostalCode, Capital, MICPermitNo,
               COUNT(*) OVER() AS TotalCount
        FROM PaThaKa
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        WHERE (IssuedDate >= @FromDate AND IssuedDate <= @ToDate)
          AND BusinessTypeId = (CASE WHEN @BusinessTypeId = 0 THEN BusinessTypeId ELSE @BusinessTypeId END)
          AND LineofBusinessId = (CASE WHEN @LineofBusinessId = 0 THEN LineofBusinessId ELSE @LineofBusinessId END)
          AND Status = (CASE WHEN @Status = '''' THEN Status ELSE @Status END)
          AND State = (CASE WHEN @State = '''' THEN State ELSE @State END)
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKa.CompanyRegistrationNo ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
    BEGIN
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS
        FETCH NEXT @PageSize ROWS ONLY';
    END

    SET @Sql = @Sql + N';';

    EXEC sp_executesql @Sql,
        N'@FromDate datetime, @ToDate datetime, @BusinessTypeId int, @LineofBusinessId int,
          @State nvarchar(200), @Status nvarchar(50), @PageIndex int, @PageSize int',
        @FromDate = @FromDate,
        @ToDate = @ToDate,
        @BusinessTypeId = @BusinessTypeId,
        @LineofBusinessId = @LineofBusinessId,
        @State = @State,
        @Status = @Status,
        @PageIndex = @PageIndex,
        @PageSize = @PageSize;
END
GO

GO

PRINT N'Done. Now run VerifyDeployment.sql.';
GO
