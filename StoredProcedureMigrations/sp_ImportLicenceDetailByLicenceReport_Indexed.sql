USE [TradeNetDB];
GO

/*
    sp_ImportLicenceDetailByLicenceReport_Indexed
    ---------------------------------------------
    Paginated licence-level drill list (one row per licence + currency) reached from
    the By Section / Method / Seller Country / Company summaries. Its record count
    equals the "No of Licences" of the clicked cell.

    Reads the indexed view dbo.vw_ImportLicenceItemTotalByCurrency WITH (NOEXPAND)
    (one pre-summed row per licence+currency) instead of fanning out over the 6.4M-row
    ImportLicenceItem table, which timed out under EF.

    Optional @Currency restricts to the clicked cell's currency so the drilled record
    count reconciles with the summary cell.
*/
CREATE OR ALTER PROCEDURE dbo.sp_ImportLicenceDetailByLicenceReport_Indexed
    @FromDate datetime,
    @ToDate datetime,
    @PaThaKaTypeId int = 0,
    @ExportImportSectionId int = 0,
    @ExportImportMethodId int = 0,
    @ExportImportIncotermId int = 0,
    @SellerCountryId int = 0,
    @CompanyRegistrationNo nvarchar(50) = N'',
    @Currency nvarchar(20) = N'',
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 0 ELSE ISNULL(@PageIndex, 0) * CAST(@PageSize AS bigint) END;
    DECLARE @fetch bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1   -- fast page: one extra row signals "has next"
        ELSE @PageSize END;

    -- Exact total only when requested (the heavy half); RECOMPILE folds in the actual
    -- filter values rather than reusing a sniffed plan.
    DECLARE @total int = NULL;
    IF @IncludeTotalCount = 1
    BEGIN
        SELECT @total = COUNT(*)
        FROM dbo.ImportLicence AS il
        INNER JOIN dbo.PaThaKa AS p ON p.Id = il.PaThaKaId
        INNER JOIN dbo.vw_ImportLicenceItemTotalByCurrency AS v WITH (NOEXPAND) ON v.ImportLicenceId = il.Id
        INNER JOIN dbo.Currency AS c ON c.Id = v.CurrencyId
        WHERE il.ApplyType = N'New' AND il.Status = N'Approved' AND il.ImportLicenceNo <> N''
            AND il.CreatedDate >= @FromDate AND il.CreatedDate <= @ToDate
            AND (@CompanyRegistrationNo = N'' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
            AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
            AND (@ExportImportSectionId = 0 OR il.ExportImportSectionId = @ExportImportSectionId)
            AND (@ExportImportMethodId = 0 OR il.ExportImportMethodId = @ExportImportMethodId)
            AND (@ExportImportIncotermId = 0 OR il.ExportImportIncotermId = @ExportImportIncotermId)
            AND (@SellerCountryId = 0 OR il.SellerCountryId = @SellerCountryId)
            AND (@Currency = N'' OR c.Code = @Currency)
        OPTION (RECOMPILE);
    END

    SELECT
        s.Name AS SectionName,
        il.ImportLicenceNo AS LicenceNo,
        il.IssuedDate AS LicenceDate,
        p.CompanyRegistrationNo AS CompanyRegistrationNo,
        p.CompanyName AS CompanyName,
        m.Name AS MethodName,
        co.Name AS SellerCountry,
        c.Code AS Currency,
        v.TotalAmount AS TotalValue,
        @total AS TotalCount
    FROM dbo.ImportLicence AS il
    INNER JOIN dbo.PaThaKa AS p ON p.Id = il.PaThaKaId
    INNER JOIN dbo.vw_ImportLicenceItemTotalByCurrency AS v WITH (NOEXPAND) ON v.ImportLicenceId = il.Id
    INNER JOIN dbo.Currency AS c ON c.Id = v.CurrencyId
    INNER JOIN dbo.ExportImportSection AS s ON s.Id = il.ExportImportSectionId
    INNER JOIN dbo.ExportImportMethod AS m ON m.Id = il.ExportImportMethodId
    INNER JOIN dbo.Countries AS co ON co.Id = il.SellerCountryId
    WHERE il.ApplyType = N'New' AND il.Status = N'Approved' AND il.ImportLicenceNo <> N''
        AND il.CreatedDate >= @FromDate AND il.CreatedDate <= @ToDate
        AND (@CompanyRegistrationNo = N'' OR p.CompanyRegistrationNo = @CompanyRegistrationNo)
        AND (@PaThaKaTypeId = 0 OR p.PaThaKaTypeId = @PaThaKaTypeId)
        AND (@ExportImportSectionId = 0 OR il.ExportImportSectionId = @ExportImportSectionId)
        AND (@ExportImportMethodId = 0 OR il.ExportImportMethodId = @ExportImportMethodId)
        AND (@ExportImportIncotermId = 0 OR il.ExportImportIncotermId = @ExportImportIncotermId)
        AND (@SellerCountryId = 0 OR il.SellerCountryId = @SellerCountryId)
        AND (@Currency = N'' OR c.Code = @Currency)
    -- Order by the licence Id (clustered/Id-keyed indexes exist, e.g. the
    -- (ApplyType,Status,ExportImportSectionId,...,Id) index) rather than ImportLicenceNo.
    -- A drill into a large section (e.g. Section 4 / USD ~32k licences) ordered by
    -- ImportLicenceNo has NO supporting index, so SQL materialised + sorted the whole
    -- 7-table join against the 768k-row indexed view => the first page took >130s and
    -- the grid showed "no data" (it was still loading). Ordering by il.Id lets the engine
    -- seek the filtered rows and STOP after one page (verified ~0.4s vs >130s). (il.Id, c.Code)
    -- is unique per (licence, currency) row, so paging stays deterministic.
    ORDER BY il.Id, c.Code
    OFFSET @off ROWS FETCH NEXT @fetch ROWS ONLY
    OPTION (RECOMPILE);
END
GO
