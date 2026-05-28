using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_OGARecommendationReportRequest
{
    public int OGADepartmentId { get; set; }
    public int OGASectionId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
}

public sealed class sp_OGARecommendationReportResult
{
    public string Id { get; set; } = null!;
    public int OGASectionId { get; set; }
    public string? OGASectionName { get; set; }
    public string ReferenceNo { get; set; } = null!;
    public string SarNo { get; set; } = null!;
    public DateTime SarDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string? Allowance { get; set; }
    public string LicenceNo { get; set; } = null!;
    public string FormType { get; set; } = null!;
    public string Remark { get; set; } = null!;
    public string Balance { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Position { get; set; } = null!;
    public string? SDate { get; set; }
}

public static class sp_OGARecommendationReport
{
    public static IQueryable<sp_OGARecommendationReportResult> Query(
        TradeNetDbContext db,
        sp_OGARecommendationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return BranchRows(db, request)
            .OrderBy(row => row.SarDate)
            .Select(row => new sp_OGARecommendationReportResult
            {
                Id = row.Id,
                OGASectionId = row.OGASectionId,
                OGASectionName = row.OGASectionName,
                ReferenceNo = row.ReferenceNo,
                SarNo = row.SarNo,
                SarDate = row.SarDate,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                CompanyName = row.CompanyName,
                UnitLevel = row.UnitLevel,
                StreetNumberStreetName = row.StreetNumberStreetName,
                QuarterCityTownship = row.QuarterCityTownship,
                State = row.State,
                Country = row.Country,
                PostalCode = row.PostalCode,
                Allowance = row.Allowance,
                LicenceNo = row.LicenceNo,
                FormType = row.FormType,
                Remark = row.Remark,
                Balance = row.Balance,
                FullName = row.FullName,
                Position = row.Position,
                SDate = row.HistoryCreatedDate == null
                    ? null
                    : row.HistoryCreatedDate.Value.Day.ToString()
                        + "/"
                        + row.HistoryCreatedDate.Value.Month.ToString()
                        + "/"
                        + row.HistoryCreatedDate.Value.Year.ToString()
            });
    }

    private static IQueryable<OgaRecommendationReportRow> BranchRows(
        TradeNetDbContext db,
        sp_OGARecommendationReportRequest request)
    {
        return BaseRows(db, request)
            .Join(db.ExportLicences,
                row => row.History.LicencePermitId,
                licence => licence.Id,
                (row, licence) => new OgaRecommendationReportRow
                {
                    Id = row.Recommendation.Id,
                    OGASectionId = row.Recommendation.OgasectionId,
                    OGASectionName = row.Section.EnglishName,
                    ReferenceNo = row.Recommendation.ReferenceNo,
                    SarNo = row.Recommendation.SarNo,
                    SarDate = row.Recommendation.SarDate,
                    CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                    CompanyName = row.PaThaKa.CompanyName,
                    UnitLevel = row.PaThaKa.UnitLevel,
                    StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                    QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                    State = row.PaThaKa.State,
                    Country = row.PaThaKa.Country,
                    PostalCode = row.PaThaKa.PostalCode,
                    Allowance = row.Recommendation.Allowance,
                    LicenceNo = licence.ExportLicenceNo,
                    FormType = row.History.Type,
                    Remark = row.History.Remark,
                    Balance = row.History.Balance,
                    FullName = row.User.FullName,
                    Position = row.User.Position,
                    HistoryCreatedDate = row.History.CreatedDate
                })
            .Concat(BaseRows(db, request)
                .Join(db.ImportLicences,
                    row => row.History.LicencePermitId,
                    licence => licence.Id,
                    (row, licence) => new OgaRecommendationReportRow
                    {
                        Id = row.Recommendation.Id,
                        OGASectionId = row.Recommendation.OgasectionId,
                        OGASectionName = row.Section.EnglishName,
                        ReferenceNo = row.Recommendation.ReferenceNo,
                        SarNo = row.Recommendation.SarNo,
                        SarDate = row.Recommendation.SarDate,
                        CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                        CompanyName = row.PaThaKa.CompanyName,
                        UnitLevel = row.PaThaKa.UnitLevel,
                        StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                        QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                        State = row.PaThaKa.State,
                        Country = row.PaThaKa.Country,
                        PostalCode = row.PaThaKa.PostalCode,
                        Allowance = row.Recommendation.Allowance,
                        LicenceNo = licence.ImportLicenceNo,
                        FormType = row.History.Type,
                        Remark = row.History.Remark,
                        Balance = row.History.Balance,
                        FullName = row.User.FullName,
                        Position = row.User.Position,
                        HistoryCreatedDate = row.History.CreatedDate
                    }))
            .Concat(BaseRows(db, request)
                .Join(db.ExportPermits,
                    row => row.History.LicencePermitId,
                    permit => permit.Id,
                    (row, permit) => new OgaRecommendationReportRow
                    {
                        Id = row.Recommendation.Id,
                        OGASectionId = row.Recommendation.OgasectionId,
                        OGASectionName = row.Section.EnglishName,
                        ReferenceNo = row.Recommendation.ReferenceNo,
                        SarNo = row.Recommendation.SarNo,
                        SarDate = row.Recommendation.SarDate,
                        CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                        CompanyName = row.PaThaKa.CompanyName,
                        UnitLevel = row.PaThaKa.UnitLevel,
                        StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                        QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                        State = row.PaThaKa.State,
                        Country = row.PaThaKa.Country,
                        PostalCode = row.PaThaKa.PostalCode,
                        Allowance = row.Recommendation.Allowance,
                        LicenceNo = permit.ExportPermitNo,
                        FormType = row.History.Type,
                        Remark = row.History.Remark,
                        Balance = row.History.Balance,
                        FullName = row.User.FullName,
                        Position = row.User.Position,
                        HistoryCreatedDate = row.History.CreatedDate
                    }))
            .Concat(BaseRows(db, request)
                .Join(db.ImportPermits,
                    row => row.History.LicencePermitId,
                    permit => permit.Id,
                    (row, permit) => new OgaRecommendationReportRow
                    {
                        Id = row.Recommendation.Id,
                        OGASectionId = row.Recommendation.OgasectionId,
                        OGASectionName = row.Section.EnglishName,
                        ReferenceNo = row.Recommendation.ReferenceNo,
                        SarNo = row.Recommendation.SarNo,
                        SarDate = row.Recommendation.SarDate,
                        CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                        CompanyName = row.PaThaKa.CompanyName,
                        UnitLevel = row.PaThaKa.UnitLevel,
                        StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                        QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                        State = row.PaThaKa.State,
                        Country = row.PaThaKa.Country,
                        PostalCode = row.PaThaKa.PostalCode,
                        Allowance = row.Recommendation.Allowance,
                        LicenceNo = permit.ImportPermitNo,
                        FormType = row.History.Type,
                        Remark = row.History.Remark,
                        Balance = row.History.Balance,
                        FullName = row.User.FullName,
                        Position = row.User.Position,
                        HistoryCreatedDate = row.History.CreatedDate
                    }))
            .Concat(BaseRows(db, request)
                .Join(db.BorderExportLicences,
                    row => row.History.LicencePermitId,
                    licence => licence.Id,
                    (row, licence) => new OgaRecommendationReportRow
                    {
                        Id = row.Recommendation.Id,
                        OGASectionId = row.Recommendation.OgasectionId,
                        OGASectionName = row.Section.EnglishName,
                        ReferenceNo = row.Recommendation.ReferenceNo,
                        SarNo = row.Recommendation.SarNo,
                        SarDate = row.Recommendation.SarDate,
                        CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                        CompanyName = row.PaThaKa.CompanyName,
                        UnitLevel = row.PaThaKa.UnitLevel,
                        StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                        QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                        State = row.PaThaKa.State,
                        Country = row.PaThaKa.Country,
                        PostalCode = row.PaThaKa.PostalCode,
                        Allowance = row.Recommendation.Allowance,
                        LicenceNo = licence.ExportLicenceNo,
                        FormType = row.History.Type,
                        Remark = row.History.Remark,
                        Balance = row.History.Balance,
                        FullName = row.User.FullName,
                        Position = row.User.Position,
                        HistoryCreatedDate = row.History.CreatedDate
                    }))
            .Concat(BaseRows(db, request)
                .Join(db.BorderImportLicences,
                    row => row.History.LicencePermitId,
                    licence => licence.Id,
                    (row, licence) => new OgaRecommendationReportRow
                    {
                        Id = row.Recommendation.Id,
                        OGASectionId = row.Recommendation.OgasectionId,
                        OGASectionName = row.Section.EnglishName,
                        ReferenceNo = row.Recommendation.ReferenceNo,
                        SarNo = row.Recommendation.SarNo,
                        SarDate = row.Recommendation.SarDate,
                        CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                        CompanyName = row.PaThaKa.CompanyName,
                        UnitLevel = row.PaThaKa.UnitLevel,
                        StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                        QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                        State = row.PaThaKa.State,
                        Country = row.PaThaKa.Country,
                        PostalCode = row.PaThaKa.PostalCode,
                        Allowance = row.Recommendation.Allowance,
                        LicenceNo = licence.ImportLicenceNo,
                        FormType = row.History.Type,
                        Remark = row.History.Remark,
                        Balance = row.History.Balance,
                        FullName = row.User.FullName,
                        Position = row.User.Position,
                        HistoryCreatedDate = row.History.CreatedDate
                    }))
            .Concat(BaseRows(db, request)
                .Join(db.BorderExportPermits,
                    row => row.History.LicencePermitId,
                    permit => permit.Id,
                    (row, permit) => new OgaRecommendationReportRow
                    {
                        Id = row.Recommendation.Id,
                        OGASectionId = row.Recommendation.OgasectionId,
                        OGASectionName = row.Section.EnglishName,
                        ReferenceNo = row.Recommendation.ReferenceNo,
                        SarNo = row.Recommendation.SarNo,
                        SarDate = row.Recommendation.SarDate,
                        CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                        CompanyName = row.PaThaKa.CompanyName,
                        UnitLevel = row.PaThaKa.UnitLevel,
                        StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                        QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                        State = row.PaThaKa.State,
                        Country = row.PaThaKa.Country,
                        PostalCode = row.PaThaKa.PostalCode,
                        Allowance = row.Recommendation.Allowance,
                        LicenceNo = permit.ExportPermitNo,
                        FormType = row.History.Type,
                        Remark = row.History.Remark,
                        Balance = row.History.Balance,
                        FullName = row.User.FullName,
                        Position = row.User.Position,
                        HistoryCreatedDate = row.History.CreatedDate
                    }))
            .Concat(BaseRows(db, request)
                .Join(db.BorderImportPermits,
                    row => row.History.LicencePermitId,
                    permit => permit.Id,
                    (row, permit) => new OgaRecommendationReportRow
                    {
                        Id = row.Recommendation.Id,
                        OGASectionId = row.Recommendation.OgasectionId,
                        OGASectionName = row.Section.EnglishName,
                        ReferenceNo = row.Recommendation.ReferenceNo,
                        SarNo = row.Recommendation.SarNo,
                        SarDate = row.Recommendation.SarDate,
                        CompanyRegistrationNo = row.PaThaKa.CompanyRegistrationNo,
                        CompanyName = row.PaThaKa.CompanyName,
                        UnitLevel = row.PaThaKa.UnitLevel,
                        StreetNumberStreetName = row.PaThaKa.StreetNumberStreetName,
                        QuarterCityTownship = row.PaThaKa.QuarterCityTownship,
                        State = row.PaThaKa.State,
                        Country = row.PaThaKa.Country,
                        PostalCode = row.PaThaKa.PostalCode,
                        Allowance = row.Recommendation.Allowance,
                        LicenceNo = permit.ImportPermitNo,
                        FormType = row.History.Type,
                        Remark = row.History.Remark,
                        Balance = row.History.Balance,
                        FullName = row.User.FullName,
                        Position = row.User.Position,
                        HistoryCreatedDate = row.History.CreatedDate
                    }));
    }

    private static IQueryable<OgaRecommendationBaseRow> BaseRows(
        TradeNetDbContext db,
        sp_OGARecommendationReportRequest request)
    {
        return
            from recommendation in db.Ogarecommendations
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join paThaKa in db.PaThaKas on recommendation.PaThaKaId equals paThaKa.Id
            join history in db.OgarecommendationHistories on recommendation.Id equals history.OgarecommendationId
            join user in db.Users on history.MocuserId equals user.Id
            where recommendation.SarDate >= request.FromDate
                && recommendation.SarDate <= request.ToDate
                && recommendation.OgadepartmentId == request.OGADepartmentId
                && (request.OGASectionId == 0 || recommendation.OgasectionId == request.OGASectionId)
                && (request.ReferenceNo == "0" || recommendation.ReferenceNo == request.ReferenceNo)
            select new OgaRecommendationBaseRow
            {
                Recommendation = recommendation,
                Section = section,
                PaThaKa = paThaKa,
                History = history,
                User = user
            };
    }

    private sealed class OgaRecommendationBaseRow
    {
        public API.Model.TradeNet.Ogarecommendation Recommendation { get; set; } = null!;
        public API.Model.TradeNet.Ogasection Section { get; set; } = null!;
        public API.Model.TradeNet.PaThaKa PaThaKa { get; set; } = null!;
        public API.Model.TradeNet.OgarecommendationHistory History { get; set; } = null!;
        public API.Model.TradeNet.User User { get; set; } = null!;
    }

    private sealed class OgaRecommendationReportRow
    {
        public string Id { get; set; } = null!;
        public int OGASectionId { get; set; }
        public string? OGASectionName { get; set; }
        public string ReferenceNo { get; set; } = null!;
        public string SarNo { get; set; } = null!;
        public DateTime SarDate { get; set; }
        public string CompanyRegistrationNo { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string? UnitLevel { get; set; }
        public string StreetNumberStreetName { get; set; } = null!;
        public string QuarterCityTownship { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string? PostalCode { get; set; }
        public string? Allowance { get; set; }
        public string LicenceNo { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public string Remark { get; set; } = null!;
        public string Balance { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Position { get; set; } = null!;
        public DateTime? HistoryCreatedDate { get; set; }
    }
}
