/* =============================================
   sp_PathakaBindReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_PathakaBindReport.
   Pagination-aware copy used by EIRCardBindReportController.
   Source procedure: dbo.sp_PathakaBindReport (left untouched).

   Note: the source aliases a column as 'Bind Application No' (with spaces);
   this copy aliases it BindApplicationNo so it maps to the C# result property.

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - Every row carries TotalCount (COUNT(*) OVER()).
   - @SortColumn whitelisted; default ApplicationDate.

   Idempotent: CREATE OR ALTER.
   ============================================= */
CREATE OR ALTER PROCEDURE [dbo].[sp_PathakaBindReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OrderBy nvarchar(200) =
        CASE @SortColumn
            WHEN 'ApplicationDate'    THEN 'a.ApplicationDate'
            WHEN 'ApproveDate'        THEN 'b.ApproveDate'
            WHEN 'ApplicationNo'      THEN 'a.ApplicationNo'
            WHEN 'BindApplicationNo'  THEN 'b.ApplicationNo'
            WHEN 'Status'             THEN 'b.Status'
            WHEN 'PaThaKaNo'          THEN 'c.PaThaKaNo'
            WHEN 'CompanyName'        THEN 'b.CompanyName'
            ELSE 'a.ApplicationDate'
        END;

    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    DECLARE @Sql nvarchar(max) = N'
        SELECT a.ApplicationDate,
               b.ApproveDate,
               a.ApplicationNo,
               b.ApplicationNo AS BindApplicationNo,
               b.Status,
               c.PaThaKaNo,
               d.MemberCode,
               d.Email,
               b.CompanyName,
               COUNT(*) OVER() AS TotalCount
        FROM PaThaKaBind AS a
        LEFT JOIN PaThaKaRegistration AS b ON b.Id = a.PaThaKaId
        LEFT JOIN PaThaKa AS c ON c.Id = a.PaThaKaId
        LEFT JOIN Member AS d ON d.Id = a.MemberId
        WHERE b.ApproveDate BETWEEN @FromDate AND @ToDate
          AND c.MemberId IS NOT NULL
        ORDER BY ' + @OrderBy + N' ' + @Direction + N', a.Id ' + @Direction;

    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Sql = @Sql + N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    SET @Sql = @Sql + N';';

    EXEC sp_executesql @Sql,
        N'@FromDate datetime, @ToDate datetime, @PageIndex int, @PageSize int',
        @FromDate = @FromDate, @ToDate = @ToDate, @PageIndex = @PageIndex, @PageSize = @PageSize;
END
GO
