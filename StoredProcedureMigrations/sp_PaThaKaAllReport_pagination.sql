/* =============================================
   sp_PaThaKaAllReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_PaThaKaAllReport.
   Pagination-aware copy used by ListOfCompanyController.
   Source procedure: dbo.sp_PaThaKaAllReport (left untouched).
   Preserves dbo.fn_GetNRCNo for OwnerNRC and all source joins.

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - Every row carries TotalCount (COUNT(*) OVER()).
   - @SortColumn whitelisted; default IssuedDate (source order).

   Idempotent: CREATE OR ALTER.
   ============================================= */
CREATE OR ALTER PROCEDURE [dbo].[sp_PaThaKaAllReport_pagination]
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

    DECLARE @OrderBy nvarchar(200) =
        CASE @SortColumn
            WHEN 'CompanyRegistrationNo'   THEN 'PaThaKa.CompanyRegistrationNo'
            WHEN 'CompanyName'             THEN 'PaThaKa.CompanyName'
            WHEN 'OwnerName'               THEN 'PaThaKa.OwnerName'
            WHEN 'CompanyRegistrationDate' THEN 'PaThaKa.CompanyRegistrationDate'
            WHEN 'EndDate'                 THEN 'PaThaKa.EndDate'
            WHEN 'BusinessType'            THEN 'businessType.Name'
            WHEN 'LineofBusiness'          THEN 'lineofBusiness.Name'
            WHEN 'State'                   THEN 'PaThaKa.State'
            WHEN 'Status'                  THEN 'PaThaKa.Status'
            ELSE 'PaThaKa.IssuedDate'
        END;

    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @Sql nvarchar(max) = N'
        SELECT CompanyRegistrationNo, CompanyName, OwnerName,
               dbo.fn_GetNRCNo(OwnerNRCType, OwnerNRCPrefixId, OwnerNRCPrefixCodeId, OwnerNRCNo) AS OwnerNRC,
               CompanyRegistrationDate, EndDate,
               businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
               UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country, PostalCode,
               Mobile1, Mobile2, Mobile3, Fax, Email, Capital, currency.Code AS Currency,
               cardFees.Terms, DecisionDate, decisionCode.Name AS DecisionName,
               decisionCode.Position AS DecisionPosition, Status, MICPermitNo,
               COUNT(*) OVER() AS TotalCount
        FROM PaThaKa
        INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
        INNER JOIN Currency currency ON PaThaKa.CurrencyId = currency.Id
        INNER JOIN CardRegistrationFees cardFees ON PaThaKa.CardRegistrationFeesId = cardFees.Id
        INNER JOIN DecisionCode decisionCode ON PaThaKa.DecisionCodeId = decisionCode.Id
        WHERE BusinessTypeId = (CASE WHEN @BusinessTypeId = 0 THEN BusinessTypeId ELSE @BusinessTypeId END)
          AND LineofBusinessId = (CASE WHEN @LineofBusinessId = 0 THEN LineofBusinessId ELSE @LineofBusinessId END)
          AND Status = (CASE WHEN @Status = '''' THEN Status ELSE @Status END)
          AND State = (CASE WHEN @State = '''' THEN State ELSE @State END)
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKa.Id ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    SET @Sql = @Sql + N';';

    EXEC sp_executesql @Sql,
        N'@BusinessTypeId int, @LineofBusinessId int, @State nvarchar(200), @Status nvarchar(50), @PageIndex int, @PageSize int',
        @BusinessTypeId = @BusinessTypeId, @LineofBusinessId = @LineofBusinessId,
        @State = @State, @Status = @Status, @PageIndex = @PageIndex, @PageSize = @PageSize;
END
GO
