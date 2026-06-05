/* =============================================
   sp_CompanyProfileReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_CompanyProfileReport.
   Pagination-aware copy used by CompanyProfileController.
   Source procedure: dbo.sp_CompanyProfileReport (left untouched).
   Preserves dbo.fn_GetPermitBusiness and the Extension-count subquery.

   GRAIN: pages at the COMPANY grain (one page = N companies), then expands each
     paged company to one row per director. The legacy Company Profile report
     renders directors as a nested "ဒါရိုက်တာအဖွဲ့၀င်များ" sub-grid grouped under
     each company, so the UI groups the flat rows back into one block per company.
     Paging by company guarantees a company's full set of directors lands on a
     single page (paging by director could split a company across page boundaries
     and break the grouping). @PageSize therefore counts COMPANIES, not director
     rows, and TotalCount is the distinct company count.

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page of companies.
   - Every row carries TotalCount (distinct company count, computed once).
   - @SortColumn whitelisted; default IssuedDate (source order).

   PERFORMANCE: two-phase pagination.
     Phase 1 pages just the company keys (PaThaKa.Id) and computes the total with a
       cheap COUNT_BIG over the same company-grain filter. EXISTS keeps it one row
       per company (mirrors the legacy INNER JOIN to PaThaKaDirectors without
       fanning out).
     Phase 2 joins that page of companies back to their directors and only THEN
       evaluates the expensive per-company work (scalar UDF dbo.fn_GetPermitBusiness
       + the Extension-count correlated subquery).
     The @CompanyRegistrationNo predicate is added only when supplied, so an index
       seek stays available instead of the non-sargable CASE wrapper.

   Idempotent: CREATE OR ALTER.
   ============================================= */
CREATE OR ALTER PROCEDURE [dbo].[sp_CompanyProfileReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @CompanyRegistrationNo nvarchar(20),
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OrderBy nvarchar(200) =
        CASE @SortColumn
            WHEN 'CompanyRegistrationNo'   THEN 'PaThaKa.CompanyRegistrationNo'
            WHEN 'CompanyName'             THEN 'PaThaKa.CompanyName'
            WHEN 'CompanyRegistrationDate' THEN 'PaThaKa.CompanyRegistrationDate'
            WHEN 'EndDate'                 THEN 'PaThaKa.EndDate'
            WHEN 'BusinessType'            THEN 'businessType.Name'
            WHEN 'LineofBusiness'          THEN 'lineofBusiness.Name'
            WHEN 'State'                   THEN 'PaThaKa.State'
            ELSE 'PaThaKa.IssuedDate'
        END;

    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    -- Company-grain core: one row per company that has at least one director.
    -- EXISTS mirrors the legacy report's INNER JOIN to PaThaKaDirectors without
    -- multiplying rows. Registration-no predicate added only when supplied so the
    -- optimizer can seek instead of being forced through a CASE wrapper.
    DECLARE @CompanyFrom nvarchar(max) = N'
        FROM PaThaKa
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        WHERE (PaThaKa.IssuedDate >= @FromDate AND PaThaKa.IssuedDate <= @ToDate)
          AND EXISTS (SELECT 1 FROM PaThaKaDirectors WHERE PaThaKaDirectors.PaThaKaId = PaThaKa.Id)'
        + CASE WHEN @CompanyRegistrationNo = '' THEN N''
               ELSE N'
          AND PaThaKa.CompanyRegistrationNo = @CompanyRegistrationNo' END;

    -- Phase 1: distinct company count + the page of company keys only (no UDF /
    -- subquery / director fan-out here).
    DECLARE @Sql nvarchar(max) = N'
        DECLARE @Total int;
        SELECT @Total = COUNT_BIG(*)' + @CompanyFrom + N';

        SELECT PaThaKa.Id AS PaThaKaId, @Total AS TotalCount
        INTO #page' + @CompanyFrom + N'
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKa.Id ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    -- Phase 2: expand the paged companies to one row per director and enrich ONLY
    -- those rows with the expensive per-company lookups. Ordering keeps each
    -- company's directors contiguous (sort key, then company id, then director id)
    -- so the UI can group them into one nested block per company.
    SET @Sql = @Sql + N';

        SELECT PaThaKa.Id, PaThaKa.CompanyRegistrationNo, PaThaKa.EndDate, PaThaKa.CompanyName,
               PaThaKa.CompanyRegistrationDate,
               businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
               PaThaKa.UnitLevel, PaThaKa.StreetNumberStreetName, PaThaKa.QuarterCityTownship,
               PaThaKa.State, PaThaKa.Country, PaThaKa.PostalCode, PaThaKa.Capital,
               PaThaKaDirectors.Name AS DirectorName, PaThaKaDirectors.NRC AS DirectorNRC,
               PaThaKaDirectors.Position AS DirectorPosition,
               ISNULL(dbo.fn_GetPermitBusiness(PaThaKa.Id), '''') AS PermitBusiness,
               (SELECT COUNT(Id) FROM PaThaKaRegistration
                WHERE PaThaKaRegistration.CompanyRegistrationNo = PaThaKa.CompanyRegistrationNo
                AND PaThaKaRegistration.ApplyType = ''Extension'' AND PaThaKaRegistration.Status = ''Approved'') AS ExtensionCount,
               #page.TotalCount
        FROM #page
        INNER JOIN PaThaKa ON PaThaKa.Id = #page.PaThaKaId
        INNER JOIN PaThaKaDirectors ON PaThaKaDirectors.PaThaKaId = PaThaKa.Id
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKa.Id ' + @Direction + N', PaThaKaDirectors.Id ' + @Direction + N';

        DROP TABLE #page;';

    EXEC sp_executesql @Sql,
        N'@FromDate datetime, @ToDate datetime, @CompanyRegistrationNo nvarchar(20), @PageIndex int, @PageSize int',
        @FromDate = @FromDate, @ToDate = @ToDate, @CompanyRegistrationNo = @CompanyRegistrationNo,
        @PageIndex = @PageIndex, @PageSize = @PageSize;
END
GO
