/* =============================================
   sp_PaThaKaRegistrationReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_PaThaKaRegistrationReport.
   Pagination-aware copy used by RegistrationByVoucherController.
   Source procedure: dbo.sp_PaThaKaRegistrationReport (left untouched).

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - Every row carries TotalCount (COUNT(*) OVER()).
   - @SortColumn whitelisted; default registration CreatedDate.

   Idempotent: CREATE OR ALTER.
   ============================================= */
CREATE OR ALTER PROCEDURE [dbo].[sp_PaThaKaRegistrationReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @PaymentType nvarchar(50),
    @ApplyType nvarchar(50),
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OrderBy nvarchar(200) =
        CASE @SortColumn
            WHEN 'Date'                  THEN 'PaThaKaRegistration.CreatedDate'
            WHEN 'CompanyRegistrationNo' THEN 'CompanyRegistrationNo'
            WHEN 'CompanyName'           THEN 'CompanyName'
            WHEN 'BusinessType'          THEN 'businessType.Name'
            WHEN 'LineofBusiness'        THEN 'lineofBusiness.Name'
            WHEN 'State'                 THEN 'State'
            WHEN 'VoucherNo'             THEN 'VoucherNo'
            WHEN 'VoucherDate'           THEN 'VoucherDate'
            WHEN 'TotalAmount'           THEN 'AccountTransaction.TotalAmount'
            ELSE 'PaThaKaRegistration.CreatedDate'
        END;

    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @Sql nvarchar(max) = N'
        SELECT PaThaKaRegistration.CreatedDate AS Date, CompanyRegistrationNo, CompanyName,
               businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
               UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country, PostalCode,
               PaymentType, VoucherNo, VoucherDate, AccountTransaction.TotalAmount AS TotalAmount,
               COUNT(*) OVER() AS TotalCount
        FROM PaThaKaRegistration
        INNER JOIN AccountTransaction ON PaThaKaRegistration.Id = AccountTransaction.TransactionId
        INNER JOIN BusinessType businessType ON PaThaKaRegistration.BusinessTypeId = businessType.Id
        INNER JOIN LineofBusiness lineofBusiness ON PaThaKaRegistration.LineofBusinessId = lineofBusiness.Id
        WHERE PaThaKaRegistration.ApplyType = @ApplyType AND Status = ''Approved'' AND IsPayment = 1
          AND AccountTransaction.PaymentType = (CASE WHEN @PaymentType = '''' THEN AccountTransaction.PaymentType ELSE @PaymentType END)
          AND (PaThaKaRegistration.CreatedDate >= @FromDate AND PaThaKaRegistration.CreatedDate <= @ToDate)
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', PaThaKaRegistration.Id ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    SET @Sql = @Sql + N';';

    EXEC sp_executesql @Sql,
        N'@FromDate datetime, @ToDate datetime, @PaymentType nvarchar(50), @ApplyType nvarchar(50), @PageIndex int, @PageSize int',
        @FromDate = @FromDate, @ToDate = @ToDate, @PaymentType = @PaymentType, @ApplyType = @ApplyType,
        @PageIndex = @PageIndex, @PageSize = @PageSize;
END
GO
