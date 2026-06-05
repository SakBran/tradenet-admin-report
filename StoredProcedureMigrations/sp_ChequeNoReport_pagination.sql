CREATE OR ALTER PROCEDURE [dbo].[sp_ChequeNoReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @ChequeNoId int,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL,
    @IncludeTotalCount bit = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ps bigint = CASE
        WHEN ISNULL(@PageSize, 0) <= 0 THEN 9223372036854775807
        WHEN @IncludeTotalCount = 0 THEN @PageSize + 1
        ELSE @PageSize END;
    DECLARE @off bigint = CASE WHEN ISNULL(@PageSize, 0) <= 0 THEN 0 ELSE ISNULL(@PageIndex, 0) * CAST(@PageSize AS bigint) END;
    DECLARE @dir nvarchar(4) = CASE WHEN UPPER(ISNULL(@SortOrder, 'ASC')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @ob nvarchar(400);
    IF @SortColumn IS NOT NULL AND @SortColumn IN (N'ChequeId', N'ChequeNo', N'Date', N'SDate', N'Amount')
        SET @ob = QUOTENAME(@SortColumn) + N' ' + @dir
            + CASE WHEN @SortColumn = N'Date' THEN N'' ELSE N', [Date] ASC' END
            + CASE WHEN @SortColumn = N'ChequeId' THEN N'' ELSE N', [ChequeId] ASC' END;
    ELSE
        SET @ob = N'[Date] ASC, [ChequeId] ASC';

    DECLARE @sql nvarchar(max) = N'
    SELECT pg.*
    FROM (
        SELECT grouped.ChequeId,
            grouped.ChequeNo,
            grouped.[Date],
            grouped.SDate,
            grouped.Amount,
            CASE WHEN @IncludeTotalCount = 1 THEN COUNT(*) OVER() ELSE NULL END AS TotalCount
        FROM (
            SELECT tmp.Id AS ChequeId,
                tmp.Code AS ChequeNo,
                tmp.VoucherDate AS [Date],
                tmp.SVoucherDate AS SDate,
                SUM(tmp.Amount) AS Amount
            FROM (
                SELECT ChequeNo.Id,
                    ChequeNo.Code,
                    AccountTransaction.VoucherDate,
                    CONVERT(varchar, AccountTransaction.VoucherDate, 103) AS SVoucherDate,
                    AccountTransactionDetail.Amount
                FROM AccountTransaction
                INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
                INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
                INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
                WHERE AccountTransaction.IsPayment = 1
                    AND AccountTransaction.VoucherDate >= @FromDate
                    AND AccountTransaction.VoucherDate <= @ToDate
                    AND ChequeNo.Id = (CASE WHEN @ChequeNoId = 0 THEN ChequeNo.Id ELSE @ChequeNoId END)
            ) tmp
            GROUP BY tmp.Id, tmp.Code, tmp.VoucherDate, tmp.SVoucherDate
        ) grouped
        ORDER BY ' + @ob + N' OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY
    ) pg
    ORDER BY ' + @ob + N'
    OPTION (RECOMPILE);';

    EXEC sp_executesql @sql,
        N'@FromDate datetime, @ToDate datetime, @ChequeNoId int, @IncludeTotalCount bit, @off bigint, @ps bigint',
        @FromDate = @FromDate,
        @ToDate = @ToDate,
        @ChequeNoId = @ChequeNoId,
        @IncludeTotalCount = @IncludeTotalCount,
        @off = @off,
        @ps = @ps;
END
