using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_DirectorListReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string NRCType { get; set; } = string.Empty;
    public int NRCPrefixId { get; set; }
    public int NRCPrefixCodeId { get; set; }
    public string NRCNo { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_DirectorListReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string? DirectorName { get; set; }
    public string? DirectorNRC { get; set; }
    public string? DirectorPosition { get; set; }
    public string? DirectorNationality { get; set; }
    public string? DirectorUnitLevel { get; set; }
    public string? DirectorStreetNumberStreetName { get; set; }
    public string? DirectorQuarterCityTownship { get; set; }
    public string? DirectorState { get; set; }
    public string? DirectorCountry { get; set; }
    public string? DirectorPostalCode { get; set; }
    public int? DirectorSortOrder { get; set; }
    public string? DirectorBlackList { get; set; }
}

public static class sp_DirectorListReport
{
    private const string ByCompanyRegistrationNo = "By Company Registration No";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_DirectorListReportResult> Query(
        TradeNetDbContext db,
        sp_DirectorListReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var query = BuildDirectorRows(db);

        if (request.Type == ByCompanyRegistrationNo)
        {
            return query
                .Where(row => row.CompanyRegistrationNo == request.CompanyRegistrationNo)
                .OrderBy(row => row.DirectorSortOrder)
                .Select(row => new sp_DirectorListReportResult
                {
                    CompanyRegistrationNo = row.CompanyRegistrationNo,
                    CompanyName = row.CompanyName,
                    CompanyRegistrationDate = row.CompanyRegistrationDate,
                    EndDate = row.EndDate,
                    BusinessType = row.BusinessType,
                    LineofBusiness = row.LineofBusiness,
                    UnitLevel = row.UnitLevel,
                    StreetNumberStreetName = row.StreetNumberStreetName,
                    QuarterCityTownship = row.QuarterCityTownship,
                    State = row.State,
                    Country = row.Country,
                    PostalCode = row.PostalCode,
                    DirectorName = row.DirectorName,
                    DirectorNRC = row.DirectorNRC,
                    DirectorPosition = row.DirectorPosition,
                    DirectorUnitLevel = row.DirectorUnitLevel,
                    DirectorStreetNumberStreetName = row.DirectorStreetNumberStreetName,
                    DirectorQuarterCityTownship = row.DirectorQuarterCityTownship,
                    DirectorState = row.DirectorState,
                    DirectorCountry = row.DirectorCountry,
                    DirectorPostalCode = row.DirectorPostalCode
                });
        }

        query = query.Where(row => row.IssuedDate >= request.FromDate && row.IssuedDate <= request.ToDate);
        query = ApplyDirectorListFilters(db, query, request);

        return query
            .OrderBy(row => row.IssuedDate)
            .ThenBy(row => row.DirectorSortOrder)
            .Select(row => new sp_DirectorListReportResult
            {
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                CompanyName = row.CompanyName,
                CompanyRegistrationDate = row.CompanyRegistrationDate,
                IssuedDate = row.IssuedDate,
                EndDate = row.EndDate,
                BusinessType = row.BusinessType,
                LineofBusiness = row.LineofBusiness,
                UnitLevel = row.UnitLevel,
                StreetNumberStreetName = row.StreetNumberStreetName,
                QuarterCityTownship = row.QuarterCityTownship,
                State = row.State,
                Country = row.Country,
                PostalCode = row.PostalCode,
                DirectorName = row.DirectorName,
                DirectorNRC = row.DirectorNRC,
                DirectorPosition = row.DirectorPosition,
                DirectorNationality = row.DirectorNationality,
                DirectorUnitLevel = row.DirectorUnitLevel,
                DirectorStreetNumberStreetName = row.DirectorStreetNumberStreetName,
                DirectorQuarterCityTownship = row.DirectorQuarterCityTownship,
                DirectorState = row.DirectorState,
                DirectorCountry = row.DirectorCountry,
                DirectorPostalCode = row.DirectorPostalCode,
                DirectorSortOrder = row.DirectorSortOrder,
                DirectorBlackList = row.DirectorBlackList
            });
    }

    private static IQueryable<DirectorRow> ApplyDirectorListFilters(
        TradeNetDbContext db,
        IQueryable<DirectorRow> query,
        sp_DirectorListReportRequest request)
    {
        // Preserve the stored procedure's CASE-without-ELSE behavior: a non-empty
        // company registration number in this branch returns no rows.
        query = request.CompanyRegistrationNo == string.Empty
            ? query.Where(row => row.CompanyRegistrationNo == row.CompanyRegistrationNo)
            : query.Where(_ => false);

        query = request.Name == string.Empty
            ? query.Where(row => row.DirectorName == row.DirectorName)
            : query.Where(row => row.DirectorName == request.Name);

        query = request.Nationality == string.Empty
            ? query.Where(row => row.DirectorNationality == row.DirectorNationality)
            : query.Where(row => row.DirectorNationality == request.Nationality);

        if (request.NRCType == CurrentNrcType && request.NRCNo != string.Empty)
        {
            query = from row in query
                    from requestPrefix in db.Nrcprefixes
                        .Where(prefix => prefix.Id == request.NRCPrefixId)
                        .DefaultIfEmpty()
                    from requestPrefixCode in db.NrcprefixCodes
                        .Where(prefixCode => prefixCode.Id == request.NRCPrefixCodeId)
                        .DefaultIfEmpty()
                    where row.DirectorNRC == requestPrefix.StatePrefix.ToString()
                        + "/"
                        + requestPrefix.TownshipPrefix
                        + requestPrefixCode.Code
                        + request.NRCNo
                    select row;
        }
        else if (request.NRCType == OldNrcType && request.NRCNo != string.Empty)
        {
            query = query.Where(row => row.DirectorNRC == request.NRCNo);
        }
        else
        {
            query = query.Where(row => row.DirectorNRC == row.DirectorNRC);
        }

        return query;
    }

    private static IQueryable<DirectorRow> BuildDirectorRows(TradeNetDbContext db)
    {
        return from paThaKa in db.PaThaKas
               join director in db.PaThaKaDirectors on paThaKa.Id equals director.PaThaKaId
               join businessType in db.BusinessTypes on paThaKa.BusinessTypeId equals businessType.Id
               join lineofBusiness in db.LineofBusinesses on paThaKa.LineofBusinessId equals lineofBusiness.Id
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => director.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => director.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               select new DirectorRow
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   CompanyName = paThaKa.CompanyName,
                   CompanyRegistrationDate = paThaKa.CompanyRegistrationDate,
                   IssuedDate = paThaKa.IssuedDate,
                   EndDate = paThaKa.EndDate,
                   BusinessType = businessType.Name,
                   LineofBusiness = lineofBusiness.Name,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   DirectorName = director.Name,
                   DirectorNRC = director.Nrctype == CurrentNrcType && director.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + director.Nrcno
                       : director.Nrctype == OldNrcType && director.Nrcno != string.Empty
                           ? director.Nrcno
                           : string.Empty,
                   DirectorPosition = director.Position,
                   DirectorNationality = director.Nationality,
                   DirectorUnitLevel = director.UnitLevel,
                   DirectorStreetNumberStreetName = director.StreetNumberStreetName,
                   DirectorQuarterCityTownship = director.QuarterCityTownship,
                   DirectorState = director.State,
                   DirectorCountry = director.Country,
                   DirectorPostalCode = director.PostalCode,
                   DirectorSortOrder = director.SortOrder,
                   DirectorBlackList = director.IsBlackList ? "Black List" : string.Empty
               };
    }

    private sealed class DirectorRow
    {
        public string CompanyRegistrationNo { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public DateTime CompanyRegistrationDate { get; set; }
        public DateTime IssuedDate { get; set; }
        public DateTime EndDate { get; set; }
        public string BusinessType { get; set; } = null!;
        public string? LineofBusiness { get; set; }
        public string? UnitLevel { get; set; }
        public string StreetNumberStreetName { get; set; } = null!;
        public string QuarterCityTownship { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string? PostalCode { get; set; }
        public string? DirectorName { get; set; }
        public string? DirectorNRC { get; set; }
        public string? DirectorPosition { get; set; }
        public string? DirectorNationality { get; set; }
        public string? DirectorUnitLevel { get; set; }
        public string? DirectorStreetNumberStreetName { get; set; }
        public string? DirectorQuarterCityTownship { get; set; }
        public string? DirectorState { get; set; }
        public string? DirectorCountry { get; set; }
        public string? DirectorPostalCode { get; set; }
        public int? DirectorSortOrder { get; set; }
        public string? DirectorBlackList { get; set; }
    }
}
