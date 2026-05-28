using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ApplicationHistoryRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string FilterMemberId { get; set; } = string.Empty;
}

public sealed class sp_ApplicationHistoryResult
{
    public string ApplicationNo { get; set; } = null!;
    public DateTime? Date { get; set; }
    public string FormType { get; set; } = null!;
    public string ApplyType { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string CardLicencePermitNo { get; set; } = null!;
    public string? Message { get; set; }
    public string MemberId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string FullName { get; set; } = null!;
}

public static class sp_ApplicationHistory
{
    private const string Approved = "Approved";
    private const string Reject = "Reject";

    public static IQueryable<sp_ApplicationHistoryResult> Query(
        TradeNetDbContext db,
        sp_ApplicationHistoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return HistoryRows(db, request, PaThaKaRegistrationSources(db))
            .Concat(HistoryRows(db, request, BorderExportLicenceSources(db)))
            .Concat(HistoryRows(db, request, BorderExportPermitSources(db)))
            .Concat(HistoryRows(db, request, BorderImportLicenceSources(db)))
            .Concat(HistoryRows(db, request, BorderImportPermitSources(db)))
            .Concat(HistoryRows(db, request, ExportLicenceSources(db)))
            .Concat(HistoryRows(db, request, ExportPermitSources(db)))
            .Concat(HistoryRows(db, request, ImportLicenceSources(db)))
            .Concat(HistoryRows(db, request, ImportPermitSources(db)))
            .Concat(HistoryRows(db, request, BusinessServiceAgencySources(db)))
            .Concat(HistoryRows(db, request, DutyFreeShopSources(db)))
            .Concat(HistoryRows(db, request, SaleCenterSources(db)))
            .Concat(HistoryRows(db, request, ShowRoomSources(db)))
            .Concat(HistoryRows(db, request, WholeSaleRetailSources(db)))
            .Concat(HistoryRows(db, request, WineImportationSources(db)))
            .OrderByDescending(row => row.Date);
    }

    private static IQueryable<sp_ApplicationHistoryResult> HistoryRows(
        TradeNetDbContext db,
        sp_ApplicationHistoryRequest request,
        IQueryable<ApplicationHistorySource> sources)
    {
        var companyRegistrationNo = db.PaThaKas
            .Where(paThaKa => paThaKa.MemberId == request.MemberId)
            .Select(paThaKa => paThaKa.CompanyRegistrationNo)
            .FirstOrDefault();

        var nonApprovedRows =
            from source in sources
            join status in db.Statuses on source.Status equals status.Status1
            join member in db.Members on source.MemberId equals member.Id
            where source.CompanyRegistrationNo == companyRegistrationNo
                && source.Status != Approved
                && source.Status != string.Empty
                && source.ApplicationDate >= request.FromDate
                && source.ApplicationDate <= request.ToDate
                && (request.FilterMemberId == string.Empty || source.MemberId == request.FilterMemberId)
            select new sp_ApplicationHistoryResult
            {
                ApplicationNo = source.ApplicationNo,
                Date = source.ApplicationDate,
                FormType = source.FormType,
                ApplyType = source.ApplyType,
                CompanyRegistrationNo = source.CompanyRegistrationNo,
                CompanyName = source.CompanyName,
                CardLicencePermitNo = source.CardLicencePermitNo,
                Message = source.Status == Reject
                    ? db.Messages
                        .Where(message => message.TransactionId == source.Id)
                        .Select(message => message.Message1)
                        .FirstOrDefault()
                    : status.Message,
                MemberId = source.MemberId,
                Status = source.Status,
                FullName = member.FullName
            };

        var approvedRows =
            from source in sources
            join status in db.Statuses on source.Status equals status.Status1
            join member in db.Members on source.MemberId equals member.Id
            where source.CompanyRegistrationNo == companyRegistrationNo
                && source.Status == Approved
                && source.CreatedDate >= request.FromDate
                && source.CreatedDate <= request.ToDate
                && (request.FilterMemberId == string.Empty || source.MemberId == request.FilterMemberId)
            select new sp_ApplicationHistoryResult
            {
                ApplicationNo = source.ApplicationNo,
                Date = source.CreatedDate,
                FormType = source.FormType,
                ApplyType = source.ApplyType,
                CompanyRegistrationNo = source.CompanyRegistrationNo,
                CompanyName = source.CompanyName,
                CardLicencePermitNo = source.CardLicencePermitNo,
                Message = source.Status == Reject
                    ? db.Messages
                        .Where(message => message.TransactionId == source.Id)
                        .Select(message => message.Message1)
                        .FirstOrDefault()
                    : status.Message,
                MemberId = source.MemberId,
                Status = source.Status,
                FullName = member.FullName
            };

        return nonApprovedRows.Concat(approvedRows);
    }

    private static IQueryable<ApplicationHistorySource> PaThaKaRegistrationSources(TradeNetDbContext db)
    {
        return db.PaThaKaRegistrations.Select(row => new ApplicationHistorySource
        {
            Id = row.Id,
            ApplicationNo = row.ApplicationNo,
            ApplicationDate = row.ApplicationDate,
            CreatedDate = row.CreatedDate,
            FormType = "Pa Tha Ka",
            ApplyType = row.ApplyType,
            CompanyRegistrationNo = row.CompanyRegistrationNo,
            CompanyName = row.CompanyName,
            CardLicencePermitNo = row.CompanyRegistrationNo,
            MemberId = row.MemberId,
            Status = row.Status
        });
    }

    private static IQueryable<ApplicationHistorySource> BorderExportLicenceSources(TradeNetDbContext db)
    {
        return
            from row in db.BorderExportLicences
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Border Export Licence",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ExportLicenceNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> BorderExportPermitSources(TradeNetDbContext db)
    {
        return
            from row in db.BorderExportPermits
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Border Export Permit",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ExportPermitNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> BorderImportLicenceSources(TradeNetDbContext db)
    {
        return
            from row in db.BorderImportLicences
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Border Import Licence",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ImportLicenceNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> BorderImportPermitSources(TradeNetDbContext db)
    {
        return
            from row in db.BorderImportPermits
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Border Import Permit",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ImportPermitNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> ExportLicenceSources(TradeNetDbContext db)
    {
        return
            from row in db.ExportLicences
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Export Licence",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ExportLicenceNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> ExportPermitSources(TradeNetDbContext db)
    {
        return
            from row in db.ExportPermits
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Export Permit",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ExportPermitNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> ImportLicenceSources(TradeNetDbContext db)
    {
        return
            from row in db.ImportLicences
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Import Licence",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ImportLicenceNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> ImportPermitSources(TradeNetDbContext db)
    {
        return
            from row in db.ImportPermits
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Import Permit",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ImportPermitNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> BusinessServiceAgencySources(TradeNetDbContext db)
    {
        return
            from row in db.BusinessServiceAgencyRegistrations
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Business Service Agency",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.BusinessServiceAgencyNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> DutyFreeShopSources(TradeNetDbContext db)
    {
        return
            from row in db.DutyFreeShopRegistrations
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Duty Free Shop",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.DutyFreeShopNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> SaleCenterSources(TradeNetDbContext db)
    {
        return
            from row in db.SaleCenterRegistrations
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = row.RegistrationType,
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.SaleCenterNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> ShowRoomSources(TradeNetDbContext db)
    {
        return
            from row in db.ShowRoomRegistrations
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = row.RegistrationType,
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.ShowRoomNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> WholeSaleRetailSources(TradeNetDbContext db)
    {
        return
            from row in db.WholeSaleRetailRegistrations
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = row.RegistrationType,
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                CompanyName = row.CompanyName,
                CardLicencePermitNo = row.WholeSaleRetailNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private static IQueryable<ApplicationHistorySource> WineImportationSources(TradeNetDbContext db)
    {
        return
            from row in db.WineImportationRegistrations
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new ApplicationHistorySource
            {
                Id = row.Id,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                CreatedDate = row.CreatedDate,
                FormType = "Alcoholic Beverages Importation",
                ApplyType = row.ApplyType,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                CardLicencePermitNo = row.WineImportationNo,
                MemberId = row.MemberId,
                Status = row.Status
            };
    }

    private sealed class ApplicationHistorySource
    {
        public string Id { get; set; } = null!;
        public string ApplicationNo { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string FormType { get; set; } = null!;
        public string ApplyType { get; set; } = null!;
        public string CompanyRegistrationNo { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CardLicencePermitNo { get; set; } = null!;
        public string MemberId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }
}
