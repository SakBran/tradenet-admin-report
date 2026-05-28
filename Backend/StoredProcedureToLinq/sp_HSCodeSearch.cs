using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_HSCodeSearchRequest
{
    public string Type { get; set; } = string.Empty;
    public int ExportImportSectionId { get; set; }
    public string LicenceType { get; set; } = string.Empty;
}

public sealed class sp_HSCodeSearchResult
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? GroupDescription { get; set; }
    public string? LicenceType { get; set; }
    public string? Section { get; set; }
    public string? UnitCode { get; set; }
}

public static class sp_HSCodeSearch
{
    private const int HsCodeYear = 2022;

    public static IQueryable<sp_HSCodeSearchResult> Query(
        TradeNetDbContext db,
        sp_HSCodeSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return request.Type.StartsWith("Export", StringComparison.OrdinalIgnoreCase)
            ? ExportQuery(db, request)
            : ImportQuery(db, request);
    }

    private static IQueryable<sp_HSCodeSearchResult> ExportQuery(
        TradeNetDbContext db,
        sp_HSCodeSearchRequest request)
    {
        return from hsCode in db.Hscodes
               from unit in db.Units
                   .Where(unit => hsCode.UnitId == unit.Id)
                   .DefaultIfEmpty()
               where hsCode.Year == HsCodeYear
                   && (request.LicenceType == string.Empty
                       ? hsCode.ExportLicenceType == hsCode.ExportLicenceType
                       : hsCode.ExportLicenceType == request.LicenceType)
                   && (request.ExportImportSectionId == 0
                       ? hsCode.ExportSection == hsCode.ExportSection
                       : hsCode.ExportSection != null
                           && hsCode.ExportSection.Contains("'+@ExportImportSectionCode+'"))
                   && hsCode.ExportProhibited == "No"
                   && hsCode.ExportRestricted == "No"
               select new sp_HSCodeSearchResult
               {
                   Id = hsCode.Id,
                   Code = hsCode.Code,
                   Description = hsCode.Description,
                   GroupDescription = string.Empty,
                   LicenceType = hsCode.ExportLicenceType,
                   Section = hsCode.ExportSection,
                   UnitCode = unit.Code
               };
    }

    private static IQueryable<sp_HSCodeSearchResult> ImportQuery(
        TradeNetDbContext db,
        sp_HSCodeSearchRequest request)
    {
        return from hsCode in db.Hscodes
               from unit in db.Units
                   .Where(unit => hsCode.UnitId == unit.Id)
                   .DefaultIfEmpty()
               from groupCode in db.GroupCodes
                   .Where(groupCode => hsCode.ImportGroupCode == groupCode.GroupCode1)
                   .DefaultIfEmpty()
               from section in db.ExportImportSections
                   .Where(section => section.Id == request.ExportImportSectionId)
                   .DefaultIfEmpty()
               where hsCode.Year == HsCodeYear
                   && (request.LicenceType == string.Empty
                       ? hsCode.ImportLicenceType == hsCode.ImportLicenceType
                       : hsCode.ImportLicenceType == request.LicenceType)
                   && (request.ExportImportSectionId == 0
                       ? hsCode.ImportSection == hsCode.ImportSection
                       : hsCode.ImportSection != null
                           && section.Code != null
                           && hsCode.ImportSection.Contains(section.Code))
                   && hsCode.ImportProhibited == "No"
                   && hsCode.ImportRestricted == "No"
               select new sp_HSCodeSearchResult
               {
                   Id = hsCode.Id,
                   Code = hsCode.Code,
                   Description = hsCode.Description,
                   GroupDescription = groupCode.Description,
                   LicenceType = hsCode.ImportLicenceType,
                   Section = request.ExportImportSectionId == 0 ? hsCode.ImportSection : section.Code,
                   UnitCode = unit.Code
               };
    }
}
