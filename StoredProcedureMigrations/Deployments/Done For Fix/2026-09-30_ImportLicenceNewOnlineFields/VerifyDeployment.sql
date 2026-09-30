USE [TradeNetDB];
GO

DECLARE @Definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_NewReport_pagination'));

SELECT
    IIF(@Definition LIKE N'%ImportLicence.ApplicationNo%', 1, 0) AS HasOnlineNo,
    IIF(@Definition LIKE N'%ImportLicence.ApplicationDate%', 1, 0) AS HasOnlineDate,
    IIF(@Definition LIKE N'%ImportLicence.IssuedDate LicenceDate%', 1, 0) AS UsesIssuedDateForLicenceDate,
    IIF(@Definition LIKE N'%ImportLicence.Remark%', 1, 0) AS HasRemark,
    IIF(@Definition NOT LIKE N'%ImportLicence.LicenceDate LicenceDate%', 1, 0) AS DoesNotUseDatabaseLicenceDate;
GO

DECLARE @SampleDate date =
(
    SELECT CAST(MAX(CreatedDate) AS date)
    FROM dbo.ImportLicence
    WHERE ApplyType = N'New'
      AND Status = N'Approved'
);
DECLARE @SampleToDate datetime = DATEADD(millisecond, -3, DATEADD(day, 1, CAST(@SampleDate AS datetime)));

SELECT TOP (20)
    ApplicationNo AS OnlineNo,
    ApplicationDate AS OnlineDate,
    IssuedDate AS LicenceDate,
    Remark
FROM dbo.ImportLicence
WHERE ApplyType = N'New'
  AND Status = N'Approved'
  AND CreatedDate >= @SampleDate
  AND CreatedDate < DATEADD(day, 1, @SampleDate)
ORDER BY ApplicationDate DESC;

EXEC dbo.sp_NewReport_pagination
    @FormType = N'Import Licence',
    @FromDate = @SampleDate,
    @ToDate = @SampleToDate,
    @PageIndex = 0,
    @PageSize = 20,
    @SortColumn = N'ApplicationDate',
    @SortOrder = N'DESC';
GO
