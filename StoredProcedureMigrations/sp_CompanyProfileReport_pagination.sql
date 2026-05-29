/* =============================================
   sp_CompanyProfileReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_CompanyProfileReport.
   Pagination-aware copy used by CompanyProfileController.
   Source procedure: dbo.sp_CompanyProfileReport (left untouched).
   Preserves dbo.fn_GetPermitBusiness and the Extension-count subquery.

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - Every row carries TotalCount (COUNT(*) OVER()).
   - @SortColumn whitelisted; default IssuedDate (source order).

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

    DECLARE @Sql nvarchar(max) = N'
        SELECT PaThaKa.Id, CompanyRegistrationNo, EndDate, CompanyName, CompanyRegistrationDate,
               businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
               PaThaKa.UnitLevel, PaThaKa.StreetNumberStreetName, PaThaKa.QuarterCityTownship,
               PaThaKa.State, PaThaKa.Country, PaThaKa.PostalCode, Capital,
               PaThaKaDirectors.Name AS DirectorName, PaThaKaDirectors.NRC AS DirectorNRC,
               PaThaKaDirectors.Position AS DirectorPosition,
               ISNULL(dbo.fn_GetPermitBusiness(PaThaKa.Id), '''') AS PermitBusiness,
               (SELECT COUNT(Id) FROM PaThaKaRegistration
                WHERE PaThaKaRegistration.CompanyRegistrationNo = PaThaKa.CompanyRegistrationNo
                AND PaThaKaRegistration.ApplyType = ''Extension'' AND PaThaKaRegistration.Status = ''Approved'') AS ExtensionCount,
               COUNT(*) OVER() AS TotalCount
        FROM PaThaKa
        INNER JOIN PaThaKaDirectors ON PaThaKa.Id = PaThaKaDirectors.PaThaKaId
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        WHERE (PaThaKa.IssuedDate >= @FromDate AND PaThaKa.IssuedDate <= @ToDate)
          AND PaThaKa.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '''' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKaDirectors.Id ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    SET @Sql = @Sql + N';';

    EXEC sp_executesql @Sql,
        N'@FromDate datetime, @ToDate datetime, @CompanyRegistrationNo nvarchar(20), @PageIndex int, @PageSize int',
        @FromDate = @FromDate, @ToDate = @ToDate, @CompanyRegistrationNo = @CompanyRegistrationNo,
        @PageIndex = @PageIndex, @PageSize = @PageSize;
END
GO
