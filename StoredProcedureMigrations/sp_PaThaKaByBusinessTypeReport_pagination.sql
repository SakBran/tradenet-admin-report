/* =============================================
   sp_PaThaKaByBusinessTypeReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_PaThaKaByBusinessTypeReport.
   Pagination-aware copy used by RegistrationByBusinessTypeController.
   Source procedure: dbo.sp_PaThaKaByBusinessTypeReport (left untouched).

   This is a grouped/aggregate report (one row per business type). The window
   COUNT(*) OVER() counts the grouped rows = total business-type rows, and
   SUM(Count(PaThaKa.Id)) OVER() carries the grand total of company counts across
   all business types (the report's "Total" row) on every paged row.

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - @SortColumn whitelisted; default business-type SortOrder (source order).

   Idempotent: CREATE OR ALTER.
   ============================================= */
CREATE OR ALTER PROCEDURE [dbo].[sp_PaThaKaByBusinessTypeReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @BusinessTypeId int,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OrderBy nvarchar(200) =
        CASE @SortColumn
            WHEN 'BusinessType' THEN 'businessType.Name'
            WHEN 'CompanyCount' THEN 'Count(PaThaKa.Id)'
            ELSE 'businessType.SortOrder'
        END;

    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    -- Append businessType.Name as a deterministic tie-breaker unless it is already
    -- the sort column (a column may appear only once in ORDER BY).
    DECLARE @OrderClause nvarchar(400) = @OrderBy + N' ' + @Direction;
    IF @OrderBy <> N'businessType.Name'
        SET @OrderClause = @OrderClause + N', businessType.Name ' + @Direction;

    DECLARE @Sql nvarchar(max) = N'
        SELECT businessType.Name AS BusinessType, Count(PaThaKa.Id) AS CompanyCount,
               COUNT(*) OVER() AS TotalCount,
               SUM(Count(PaThaKa.Id)) OVER() AS GrandTotal
        FROM BusinessType businessType
        LEFT JOIN PaThaKa ON businessType.Id = PaThaKa.BusinessTypeId
        WHERE (PaThaKa.IssuedDate >= @FromDate AND PaThaKa.IssuedDate <= @ToDate)
          AND BusinessTypeId = (CASE WHEN @BusinessTypeId = 0 THEN BusinessTypeId ELSE @BusinessTypeId END)
          AND Status = ''Registered''
        GROUP BY businessType.Name, businessType.SortOrder
        ORDER BY ' + @OrderClause;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    SET @Sql = @Sql + N';';

    EXEC sp_executesql @Sql,
        N'@FromDate datetime, @ToDate datetime, @BusinessTypeId int, @PageIndex int, @PageSize int',
        @FromDate = @FromDate, @ToDate = @ToDate, @BusinessTypeId = @BusinessTypeId,
        @PageIndex = @PageIndex, @PageSize = @PageSize;
END
GO
