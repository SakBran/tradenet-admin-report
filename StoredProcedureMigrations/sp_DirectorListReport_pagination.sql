/* =============================================
   sp_DirectorListReport_pagination

   NEW stored procedure. Does NOT modify dbo.sp_DirectorListReport.
   Pagination-aware copy used by:
     - ListOfDirectorsController                          (@Type = 'Director List' / other)
     - ListOfDirectorsByCompanyRegistrationNoController   (@Type = 'By Company Registration No')
   Source procedure: dbo.sp_DirectorListReport (left untouched).
   Preserves both @Type branches and dbo.fn_GetNRCNo expansion.

   Because the two source branches return different column sets and the caller
   maps a single superset row type, BOTH branches here project the SAME superset
   of columns (NULL for columns a branch does not produce), plus TotalCount.

   - @PageSize NULL/<=0 -> all rows (Excel); >0 -> one OFFSET/FETCH page.
   - Every row carries TotalCount (COUNT(*) OVER()).
   - @SortColumn whitelisted; defaults match each source branch's ORDER BY.

   Idempotent: CREATE OR ALTER.
   ============================================= */
CREATE OR ALTER PROCEDURE [dbo].[sp_DirectorListReport_pagination]
    @FromDate datetime,
    @ToDate datetime,
    @CompanyRegistrationNo nvarchar(20),
    @Name nvarchar(200),
    @Nationality nvarchar(200),
    @NRCType nvarchar(50),
    @NRCPrefixId int,
    @NRCPrefixCodeId int,
    @NRCNo nvarchar(20),
    @Type nvarchar(50), -- By Company Registration No, Director List
    @SortColumn nvarchar(128) = NULL,
    @SortOrder nvarchar(4) = NULL,
    @PageIndex int = NULL,
    @PageSize int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FilterNRCNo nvarchar(20) = dbo.fn_GetNRCNo(@NRCType, @NRCPrefixId, @NRCPrefixCodeId, @NRCNo);
    DECLARE @Direction nvarchar(4) =
        CASE WHEN UPPER(ISNULL(@SortOrder, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;
    DECLARE @Paging nvarchar(max) = N'';
    IF (@PageSize IS NOT NULL AND @PageSize > 0)
        SET @Paging = N'
        OFFSET (ISNULL(@PageIndex, 0) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY';

    DECLARE @Sql nvarchar(max);

    IF (@Type = 'By Company Registration No')
    BEGIN
        DECLARE @OrderBy1 nvarchar(200) =
            CASE @SortColumn
                WHEN 'CompanyRegistrationNo' THEN 'PaThaKa.CompanyRegistrationNo'
                WHEN 'CompanyName'           THEN 'PaThaKa.CompanyName'
                WHEN 'DirectorName'          THEN 'directors.Name'
                WHEN 'DirectorPosition'      THEN 'directors.Position'
                ELSE 'directors.SortOrder'
            END;

        SET @Sql = N'
            SELECT CompanyRegistrationNo, CompanyName, CompanyRegistrationDate,
                   CAST(NULL AS datetime) AS IssuedDate, EndDate,
                   businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
                   PaThaKa.UnitLevel, PaThaKa.StreetNumberStreetName, PaThaKa.QuarterCityTownship,
                   PaThaKa.State, PaThaKa.Country, PaThaKa.PostalCode,
                   directors.Name AS DirectorName,
                   dbo.fn_GetNRCNo(directors.NRCType, directors.NRCPrefixId, directors.NRCPrefixCodeId, directors.NRCNo) AS DirectorNRC,
                   directors.Position AS DirectorPosition,
                   CAST(NULL AS nvarchar(200)) AS DirectorNationality,
                   directors.UnitLevel AS DirectorUnitLevel, directors.StreetNumberStreetName AS DirectorStreetNumberStreetName,
                   directors.QuarterCityTownship AS DirectorQuarterCityTownship, directors.State AS DirectorState,
                   directors.Country AS DirectorCountry, directors.PostalCode AS DirectorPostalCode,
                   CAST(NULL AS int) AS DirectorSortOrder,
                   CAST(NULL AS nvarchar(20)) AS DirectorBlackList,
                   COUNT(*) OVER() AS TotalCount
            FROM PaThaKa
            INNER JOIN PaThaKaDirectors directors ON PaThaKa.Id = directors.PaThaKaId
            INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
            INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
            WHERE CompanyRegistrationNo = @CompanyRegistrationNo
            ORDER BY ' + @OrderBy1 + N' ' + @Direction + N', directors.Id ' + @Direction + @Paging + N';';

        EXEC sp_executesql @Sql,
            N'@CompanyRegistrationNo nvarchar(20), @PageIndex int, @PageSize int',
            @CompanyRegistrationNo = @CompanyRegistrationNo, @PageIndex = @PageIndex, @PageSize = @PageSize;
    END
    ELSE
    BEGIN
        DECLARE @OrderBy2 nvarchar(200) =
            CASE @SortColumn
                WHEN 'CompanyRegistrationNo' THEN 'tmp.CompanyRegistrationNo'
                WHEN 'CompanyName'           THEN 'tmp.CompanyName'
                WHEN 'EndDate'               THEN 'tmp.EndDate'
                WHEN 'BusinessType'          THEN 'tmp.BusinessType'
                WHEN 'DirectorName'          THEN 'tmp.DirectorName'
                WHEN 'DirectorNationality'   THEN 'tmp.DirectorNationality'
                ELSE 'tmp.IssuedDate'
            END;

        SET @Sql = N'
            SELECT tmp.CompanyRegistrationNo, tmp.CompanyName, tmp.CompanyRegistrationDate, tmp.IssuedDate, tmp.EndDate,
                   tmp.BusinessType, tmp.LineofBusiness, tmp.UnitLevel, tmp.StreetNumberStreetName, tmp.QuarterCityTownship,
                   tmp.State, tmp.Country, tmp.PostalCode, tmp.DirectorName, tmp.DirectorNRC, tmp.DirectorPosition,
                   tmp.DirectorNationality, tmp.DirectorUnitLevel, tmp.DirectorStreetNumberStreetName,
                   tmp.DirectorQuarterCityTownship, tmp.DirectorState, tmp.DirectorCountry, tmp.DirectorPostalCode,
                   tmp.DirectorSortOrder, tmp.DirectorBlackList,
                   COUNT(*) OVER() AS TotalCount
            FROM
            (SELECT CompanyRegistrationNo, CompanyName, CompanyRegistrationDate, IssuedDate, EndDate,
                businessType.Name AS BusinessType, lineofBusiness.Name AS LineofBusiness,
                PaThaKa.UnitLevel, PaThaKa.StreetNumberStreetName, PaThaKa.QuarterCityTownship, PaThaKa.State, PaThaKa.Country, PaThaKa.PostalCode,
                directors.Name AS DirectorName,
                dbo.fn_GetNRCNo(directors.NRCType, directors.NRCPrefixId, directors.NRCPrefixCodeId, directors.NRCNo) AS DirectorNRC,
                directors.Position AS DirectorPosition, directors.Nationality AS DirectorNationality,
                directors.UnitLevel AS DirectorUnitLevel, directors.StreetNumberStreetName AS DirectorStreetNumberStreetName,
                directors.QuarterCityTownship AS DirectorQuarterCityTownship, directors.State AS DirectorState,
                directors.Country AS DirectorCountry, directors.PostalCode AS DirectorPostalCode,
                directors.SortOrder AS DirectorSortOrder,
                CASE WHEN directors.IsBlackList = 1 THEN ''Black List'' ELSE '''' END AS DirectorBlackList
             FROM PaThaKa
             INNER JOIN PaThaKaDirectors directors ON PaThaKa.Id = directors.PaThaKaId
             INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
             INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
             WHERE (IssuedDate >= @FromDate AND IssuedDate <= @ToDate)) tmp
            WHERE tmp.CompanyRegistrationNo = (CASE WHEN @CompanyRegistrationNo = '''' THEN CompanyRegistrationNo END)
              AND tmp.DirectorName = (CASE WHEN @Name = '''' THEN tmp.DirectorName ELSE @Name END)
              AND tmp.DirectorNationality = (CASE WHEN @Nationality = '''' THEN tmp.DirectorNationality ELSE @Nationality END)
              AND tmp.DirectorNRC = (CASE WHEN @FilterNRCNo = '''' THEN tmp.DirectorNRC ELSE @FilterNRCNo END)
            ORDER BY ' + @OrderBy2 + N' ' + @Direction + N', tmp.DirectorSortOrder ' + @Direction + @Paging + N';';

        EXEC sp_executesql @Sql,
            N'@FromDate datetime, @ToDate datetime, @CompanyRegistrationNo nvarchar(20), @Name nvarchar(200),
              @Nationality nvarchar(200), @FilterNRCNo nvarchar(20), @PageIndex int, @PageSize int',
            @FromDate = @FromDate, @ToDate = @ToDate, @CompanyRegistrationNo = @CompanyRegistrationNo,
            @Name = @Name, @Nationality = @Nationality, @FilterNRCNo = @FilterNRCNo,
            @PageIndex = @PageIndex, @PageSize = @PageSize;
    END
END
GO
