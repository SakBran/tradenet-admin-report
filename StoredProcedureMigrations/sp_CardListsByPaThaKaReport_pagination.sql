/* =============================================
   sp_CardListsByPaThaKaReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_CardListsByPaThaKaReport.
   Pagination-aware copy used by CardListsByCompanyRegistrationNumberController.
   Source procedure: dbo.sp_CardListsByPaThaKaReport (left untouched).

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - Every row carries TotalCount (COUNT(*) OVER()).
   - @SortColumn whitelisted; default CompanyRegistrationNo.

   Idempotent: CREATE OR ALTER.
   ============================================= */
CREATE OR ALTER PROCEDURE [dbo].[sp_CardListsByPaThaKaReport_pagination]
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
            ELSE 'PaThaKa.CompanyRegistrationNo'
        END;

    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @Sql nvarchar(max) = N'
        SELECT PaThaKa.MICPermitNo AS MICPermitNo, PaThaKa.CompanyRegistrationNo, PaThaKa.CompanyName,
               PaThaKa.CompanyRegistrationDate, PaThaKa.EndDate,
               businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
               PaThaKa.UnitLevel, PaThaKa.StreetNumberStreetName, PaThaKa.QuarterCityTownship,
               PaThaKa.State, PaThaKa.Country, PaThaKa.PostalCode, PaThaKa.Capital,
               COUNT(*) OVER() AS TotalCount
        FROM PaThaKa
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        WHERE PaThaKa.CompanyRegistrationNo = @CompanyRegistrationNo
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKa.Id ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    SET @Sql = @Sql + N';';

    EXEC sp_executesql @Sql,
        N'@CompanyRegistrationNo nvarchar(20), @PageIndex int, @PageSize int',
        @CompanyRegistrationNo = @CompanyRegistrationNo,
        @PageIndex = @PageIndex,
        @PageSize = @PageSize;
END
GO
