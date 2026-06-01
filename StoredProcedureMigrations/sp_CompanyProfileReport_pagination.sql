/* =============================================
   sp_CompanyProfileReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_CompanyProfileReport.
   Pagination-aware copy used by CompanyProfileController.
   Source procedure: dbo.sp_CompanyProfileReport (left untouched).
   Preserves dbo.fn_GetPermitBusiness and the Extension-count subquery.

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - Every row carries TotalCount (computed once via COUNT_BIG).
   - @SortColumn whitelisted; default IssuedDate (source order).

   PERFORMANCE: two-phase pagination.
     Phase 1 pages just the keys (PaThaKa.Id + Director Id) and computes the
       total with a cheap COUNT_BIG over the same join/filter.
     Phase 2 joins that single page back and only THEN evaluates the expensive
       per-row work (scalar UDF dbo.fn_GetPermitBusiness + the Extension-count
       correlated subquery). For a 10-row page those run 10 times instead of
       once per matched row across the whole date range.
     The @CompanyRegistrationNo predicate is added only when supplied, so an
       index seek stays available instead of the non-sargable CASE wrapper.

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

    -- Shared join + filter. Add the registration-no predicate only when supplied
    -- so the optimizer can seek instead of being forced through a CASE wrapper.
    DECLARE @CoreFrom nvarchar(max) = N'
        FROM PaThaKa
        INNER JOIN PaThaKaDirectors ON PaThaKa.Id = PaThaKaDirectors.PaThaKaId
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        WHERE (PaThaKa.IssuedDate >= @FromDate AND PaThaKa.IssuedDate <= @ToDate)'
        + CASE WHEN @CompanyRegistrationNo = '' THEN N''
               ELSE N'
          AND PaThaKa.CompanyRegistrationNo = @CompanyRegistrationNo' END;

    -- Phase 1: total count + the page of keys only (no UDF / subquery here).
    DECLARE @Sql nvarchar(max) = N'
        DECLARE @Total int;
        SELECT @Total = COUNT_BIG(*)' + @CoreFrom + N';

        SELECT PaThaKa.Id AS PaThaKaId, PaThaKaDirectors.Id AS DirectorId, @Total AS TotalCount
        INTO #page' + @CoreFrom + N'
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKaDirectors.Id ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    -- Phase 2: enrich ONLY the paged rows with the expensive per-row lookups.
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
        INNER JOIN PaThaKaDirectors ON PaThaKaDirectors.Id = #page.DirectorId
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKaDirectors.Id ' + @Direction + N';

        DROP TABLE #page;';

    EXEC sp_executesql @Sql,
        N'@FromDate datetime, @ToDate datetime, @CompanyRegistrationNo nvarchar(20), @PageIndex int, @PageSize int',
        @FromDate = @FromDate, @ToDate = @ToDate, @CompanyRegistrationNo = @CompanyRegistrationNo,
        @PageIndex = @PageIndex, @PageSize = @PageSize;
END
GO
