/* =============================================
   sp_PaThaKaValidInvalidReport_pagination

   NEW stored procedure. Does NOT modify the existing
   dbo.sp_PaThaKaValidInvalidReport. Pagination-aware copy used by the
   converted report API:
     - ListOfValidAndInvalidCompanyController

   Source procedure: dbo.sp_PaThaKaValidInvalidReport (left untouched).

   Source semantics preserved exactly:
   - @Type = 'valid'  -> rows where EndDate > @Date.
   - @Type otherwise  -> rows where EndDate < @Date.
   - Same optional BusinessTypeId / LineofBusinessId / Status / State filters.
   - Same output columns (no Capital / MICPermitNo; includes IssuedDate).

   Pagination behaviour:
   - @PageSize NULL or <= 0  -> returns ALL matching rows (used by Excel export).
   - @PageSize > 0           -> returns one page using OFFSET/FETCH.
   - Every row carries TotalCount (COUNT(*) OVER()) = total matching rows.
   - @SortColumn is whitelisted; unknown values fall back to IssuedDate ordering.
     CompanyRegistrationNo is the deterministic tie-breaker for stable paging.

   Deployment:
   - Production: run this script through the normal release process.
   - Development: also applied directly to the dev database and tested
     (Backend/appsettings.json -> "TradeNetDBTest").

   Idempotent: CREATE OR ALTER so re-running is safe.
   ============================================= */
--exec sp_PaThaKaValidInvalidReport_pagination '2020-07-28',0,0,'','','valid',NULL,NULL,0,10
CREATE OR ALTER PROCEDURE [dbo].[sp_PaThaKaValidInvalidReport_pagination]
    @Date datetime,
    @BusinessTypeId int,
    @LineofBusinessId int,
    @State nvarchar(200),
    @Status nvarchar(50),
    @Type nvarchar(20), --valid,invalid
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
            WHEN 'IssuedDate'              THEN 'PaThaKa.IssuedDate'
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

    -- Preserve the source procedure's @Type branch: 'valid' -> EndDate > @Date,
    -- anything else -> EndDate < @Date.
    DECLARE @EndDatePredicate nvarchar(50) =
        CASE WHEN @Type = 'valid' THEN N'EndDate > @Date' ELSE N'EndDate < @Date' END;

    DECLARE @Sql nvarchar(max) = N'
        SELECT CompanyRegistrationNo, IssuedDate, CompanyName, CompanyRegistrationDate, EndDate,
               businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
               UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country, PostalCode,
               COUNT(*) OVER() AS TotalCount
        FROM PaThaKa
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        WHERE (' + @EndDatePredicate + N')
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
        N'@Date datetime, @BusinessTypeId int, @LineofBusinessId int,
          @State nvarchar(200), @Status nvarchar(50), @PageIndex int, @PageSize int',
        @Date = @Date,
        @BusinessTypeId = @BusinessTypeId,
        @LineofBusinessId = @LineofBusinessId,
        @State = @State,
        @Status = @Status,
        @PageIndex = @PageIndex,
        @PageSize = @PageSize;
END
GO
