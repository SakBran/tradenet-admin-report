using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

/// <summary>
/// The request the legacy Advance Search screen posted
/// (<c>AdvanceSearchDTO</c>, tradenet-2.0-api/API/Models/DTO/AdvanceSearchDTO.cs:8-28).
///
/// <c>Airport</c> is deliberately absent: the old DTO carried it and the old JS read
/// <c>$("#Airport").val()</c>, but the markup has no such control, so it always posted
/// <c>undefined</c> and the repository never referenced it.
/// </summary>
public sealed class sp_AdvanceSearchRequest
{
    public string TypeOfLicenceOrPermit { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Pathaka { get; set; } = string.Empty;

    /// <summary>
    /// Section id as a string, because "all" is <c>""</c> here and not <c>0</c>
    /// (AdvanceSearchreports.js:141-159). A literal "0" is a real id to the legacy
    /// predicate, not "all" -- kept.
    /// </summary>
    public string Section { get; set; } = string.Empty;

    public int SellerCountry { get; set; }
    public string PortOfDischarge { get; set; } = string.Empty;

    /// <summary>Comma-joined selection of <c>Sea</c> / <c>Road</c> / <c>Air</c>.</summary>
    public string ModeOfTransport { get; set; } = string.Empty;

    public int MethodOfImportExport { get; set; }

    /// <summary>Comma-joined country ids.</summary>
    public string CountryOfOrigin { get; set; } = string.Empty;

    /// <summary>Comma-joined country ids.</summary>
    public string ConsignedCountry { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public int Incoterm { get; set; }
    public int StatementCode { get; set; }
    public string ApplyType { get; set; } = string.Empty;
}

/// <summary>
/// One row of the legacy 23-column Advance Search grid
/// (<c>AdvanceSearchResultDTO</c>, tradenet-2.0-api/API/Models/DTO/AdvanceSearchResultDTO.cs:8-34,
/// rendered by AdvanceSearch.cshtml:163-188).
///
/// The legacy DTO typed every field as <c>String</c> and stringified the dates and decimals in
/// the projection. Here the money and date columns keep their real types so the grid and the
/// Excel sheet can format and align them; the display strings are produced by the column config
/// (<c>dateFormat</c> / <c>numberFormat</c>) instead.
/// </summary>
public sealed class sp_AdvanceSearchResult
{
    public string? Section { get; set; }

    /// <summary>Populated on the four Border types only; null elsewhere, as in the legacy projection.</summary>
    public string? Sakhan { get; set; }

    public string? LicenceNo { get; set; }
    public DateTime? LicenceDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? CompanyName { get; set; }

    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }

    /// <summary>
    /// The legacy "CompanyAddress" cell: the five PaThaKa address columns concatenated with no
    /// separators (AdvanceSearchRepository.cs:194-198). Composed here rather than in SQL so a null
    /// <c>UnitLevel</c> yields the rest of the address instead of the NULL the legacy `+` produced.
    /// Computed, so the grid JSON and the Excel row map both expose it as <c>companyAddress</c>.
    /// </summary>
    public string CompanyAddress => string.Concat(
        UnitLevel,
        StreetNumberStreetName,
        QuarterCityTownship,
        State,
        Country);

    public string? SellerName { get; set; }
    public string? SellerAddress { get; set; }
    public string? SellerCountry { get; set; }
    public string? PortOfDischarge { get; set; }
    public DateTime? LastDate { get; set; }

    /// <summary>Null on the four Permit types -- they have no method column, so the legacy projection left it unset.</summary>
    public string? Method { get; set; }

    /// <summary>Country names, filled after paging by <see cref="AdvanceSearchCountryNames"/>.</summary>
    public string? ConsignedCountry { get; set; }

    /// <summary>Country names, filled after paging by <see cref="AdvanceSearchCountryNames"/>.</summary>
    public string? CountryOfOrigin { get; set; }

    public string? HSCode { get; set; }
    public string? Description { get; set; }
    public string? AorU { get; set; }
    public decimal Price { get; set; }
    public decimal Qty { get; set; }
    public decimal Value { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }

    // --- plumbing, not shown ------------------------------------------------
    // The country columns are a comma-joined id string on six of the eight types and a plain
    // int on the other two (see the AdvanceSearchType table). Both shapes are carried raw and
    // resolved to names after paging, exactly where the legacy did it.

    [JsonIgnore] public string? CountryOfOriginText { get; set; }
    [JsonIgnore] public int? CountryOfOriginNumber { get; set; }
    [JsonIgnore] public string? ConsignedCountryText { get; set; }
    [JsonIgnore] public int? ConsignedCountryNumber { get; set; }

    /// <summary>Item key, used only as the sort tiebreak so OFFSET paging is deterministic.</summary>
    [JsonIgnore] public string ItemId { get; set; } = string.Empty;

    /// <summary>Item key, used only as the sort tiebreak so OFFSET paging is deterministic.</summary>
    [JsonIgnore] public int ItemUniqueId { get; set; }
}

/// <summary>
/// Resolves the raw country id columns to names, after paging -- the legacy
/// <c>countryReplace</c> step (AdvanceSearchRepository.cs:19-28, applied at :1033-1039).
///
/// The legacy helper <b>prepends</b> each name, so a stored "5,12" prints as the names of 12
/// then 5. That reversal is preserved. What is not preserved is the way it crashed: it called
/// <c>.Split(',')</c> on a column the four Permit branches never assigned (NullReferenceException),
/// <c>Convert.ToInt32("")</c> on an empty one (FormatException), and <c>.First()</c> on an id no
/// country carries (InvalidOperationException) -- between them enough to make the four Permit
/// screens return HTTP 400 for any non-empty result. Each is now a blank cell instead.
/// </summary>
public static class AdvanceSearchCountryNames
{
    public static void Apply(
        IEnumerable<sp_AdvanceSearchResult> rows,
        IReadOnlyDictionary<int, string> countryNamesById)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(countryNamesById);

        foreach (var row in rows)
        {
            row.CountryOfOrigin = Resolve(
                row.CountryOfOriginText ?? row.CountryOfOriginNumber?.ToString(CultureInfo.InvariantCulture),
                countryNamesById);
            row.ConsignedCountry = Resolve(
                row.ConsignedCountryText ?? row.ConsignedCountryNumber?.ToString(CultureInfo.InvariantCulture),
                countryNamesById);
        }
    }

    public static Task<Dictionary<int, string>> LoadAsync(
        TradeNetDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Countries
            .AsNoTracking()
            .Select(country => new { country.Id, country.Name })
            .ToDictionaryAsync(
                country => country.Id,
                country => country.Name ?? string.Empty,
                cancellationToken);
    }

    private static string Resolve(string? ids, IReadOnlyDictionary<int, string> countryNamesById)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return string.Empty;
        }

        var names = new List<string>();

        foreach (var token in ids.Split(','))
        {
            if (int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                && countryNamesById.TryGetValue(id, out var name))
            {
                // Prepended, not appended -- the legacy helper built its string this way round.
                names.Insert(0, name);
            }
        }

        return string.Join(",", names);
    }
}

/// <summary>
/// The Tradenet 2.0 Advance Search query, ported from
/// <c>tradenet-2.0-api/API/Business/AdvanceSearchRepository.cs:78-1042</c>.
///
/// Despite living beside the <c>sp_*</c> ports, this one never was a stored procedure: the old
/// admin screen (<c>Views/Reports/AdvanceSearch.cshtml</c>) posted to a separate Web API, whose
/// repository composed the whole thing in EF6 LINQ as eight copy-pasted <c>if/else if</c> blocks.
/// The filters below are that pipeline, written once; the per-type differences are in
/// <see cref="AdvanceSearchType"/> and the eight projections.
///
/// Behaviour is the legacy one, with only its crashes repaired (see
/// <see cref="AdvanceSearchCountryNames"/>, <see cref="ModeCombinations"/> and
/// <see cref="Criteria"/>). In particular these legacy quirks are deliberately kept:
/// <list type="bullet">
/// <item>the date range binds <c>IssuedDate</c>, never <c>LicenceDate</c>, and is always applied;</item>
/// <item>a multi-select Mode of Transport matches rows carrying <b>exactly</b> that set of modes,
/// in any order -- not rows carrying any one of them;</item>
/// <item>Port Of Discharge is an exact match on the name column;</item>
/// <item>every join is an INNER join, so a licence with no item lines, or an unresolvable
/// unit/currency/PaThaKa/Sakhan, does not appear at all;</item>
/// <item><c>Office</c> is accepted by the screen but filters nothing -- the legacy repository
/// never referenced it, and the Sakhan table is joined for display only.</item>
/// </list>
/// </summary>
public static class sp_AdvanceSearch
{
    /// <summary>The eight <c>TypeOfLicenceOrPermit</c> values the legacy tiles passed (AdvanceSearch.cshtml:277-389).</summary>
    public static class AdvanceSearchType
    {
        public const string ImportLicence = "Import Licence";
        public const string ExportLicence = "Export Licence";
        public const string ImportPermit = "Import Permit";
        public const string ExportPermit = "Export Permit";
        public const string BorderImportLicence = "Border Import Licence";
        public const string BorderExportLicence = "Border Export Licence";
        public const string BorderImportPermit = "Border Import Permit";
        public const string BorderExportPermit = "Border Export Permit";

        public static readonly string[] All =
        {
            ImportLicence,
            ExportLicence,
            ImportPermit,
            ExportPermit,
            BorderImportLicence,
            BorderExportLicence,
            BorderImportPermit,
            BorderExportPermit,
        };
    }

    /// <summary>
    /// Stands in for an id the caller asked for that cannot be a single integer -- a multi-select
    /// on one of the two types whose country column is an <c>int</c>, or a non-numeric Section.
    /// The legacy code reached <c>Convert.ToInt32("5,12")</c> there and returned HTTP 400; this
    /// matches no row instead. Widening those two types to "any of the selected countries" is a
    /// one-line change (<c>ids.Contains(x.CountryofOriginId)</c>) but it would move row counts
    /// away from the old screen, so it needs its own decision.
    /// </summary>
    internal const int NoMatchId = -1;

    public static IQueryable<sp_AdvanceSearchResult> Query(
        TradeNetDbContext db,
        sp_AdvanceSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var c = Criteria.Build(db, request);

        // A PaThaKa No no card carries. The legacy lookup was `.First()`, so this was an HTTP 400.
        if (c.NoRows)
        {
            return NoResults();
        }

        var rows = request.TypeOfLicenceOrPermit switch
        {
            AdvanceSearchType.ImportLicence => ImportLicenceRows(db, c),
            AdvanceSearchType.ExportLicence => ExportLicenceRows(db, c),
            AdvanceSearchType.ImportPermit => ImportPermitRows(db, c),
            AdvanceSearchType.ExportPermit => ExportPermitRows(db, c),
            AdvanceSearchType.BorderImportLicence => BorderImportLicenceRows(db, c),
            AdvanceSearchType.BorderExportLicence => BorderExportLicenceRows(db, c),
            AdvanceSearchType.BorderImportPermit => BorderImportPermitRows(db, c),
            AdvanceSearchType.BorderExportPermit => BorderExportPermitRows(db, c),

            // The legacy repository had no else branch: an unrecognised type left its query null
            // and the pager then threw. An empty result is the same answer without the 500.
            _ => NoResults(),
        };

        // The legacy grid asked for SortColumn "Description", SortOrder "ASC"
        // (AdvanceSearchreports.js:70-105) and pulled 1000 rows in one go, so it never needed a
        // tiebreak. This one is paged, and without a unique tail a row can repeat on one page and
        // vanish from the next.
        return rows
            .OrderBy(row => row.Description)
            .ThenBy(row => row.ItemId)
            .ThenBy(row => row.ItemUniqueId);
    }

    /// <summary>
    /// Every scalar the eight branches filter on, resolved once. Each nullable member is null
    /// when that filter is not in play, so the branches read as a flat list of
    /// <c>WhereIf</c> lines and cannot drift apart the way the legacy copies did.
    /// </summary>
    private sealed class Criteria
    {
        public DateTime From { get; private set; }
        public DateTime To { get; private set; }
        public string? PaThaKaId { get; private set; }

        /// <summary>A PaThaKa No was given that no card carries -- the whole result is empty.</summary>
        public bool NoRows { get; private set; }

        public int? SectionId { get; private set; }
        public int? SellerCountryId { get; private set; }
        public string? PortOfDischarge { get; private set; }
        public List<string>? ModeCombinations { get; private set; }
        public int? MethodId { get; private set; }
        public string? CountryOfOriginText { get; private set; }
        public int? CountryOfOriginNumber { get; private set; }
        public string? ConsignedCountryText { get; private set; }
        public int? ConsignedCountryNumber { get; private set; }
        public string? Description { get; private set; }
        public int? IncotermId { get; private set; }
        public int? StatementCodeId { get; private set; }
        public string? ApplyType { get; private set; }

        public static Criteria Build(TradeNetDbContext db, sp_AdvanceSearchRequest r)
        {
            var c = new Criteria
            {
                From = r.StartDate,
                To = r.EndDate,
                SectionId = string.IsNullOrEmpty(r.Section)
                    ? null
                    : int.TryParse(r.Section, NumberStyles.Integer, CultureInfo.InvariantCulture, out var section)
                        ? section
                        : NoMatchId,
                SellerCountryId = r.SellerCountry == 0 ? null : r.SellerCountry,
                PortOfDischarge = string.IsNullOrWhiteSpace(r.PortOfDischarge) ? null : r.PortOfDischarge,
                ModeCombinations = ModeCombinations(r.ModeOfTransport),
                // The legacy code looked the method id up by id and used the result
                // (`.Where(x => x.Id == id).First().Id`) purely to throw when it did not exist.
                // Filtering on the id directly gives the same rows, and an unknown id now simply
                // matches nothing.
                MethodId = r.MethodOfImportExport == 0 ? null : r.MethodOfImportExport,
                Description = string.IsNullOrEmpty(r.Description) ? null : r.Description,
                IncotermId = r.Incoterm == 0 ? null : r.Incoterm,
                StatementCodeId = r.StatementCode == 0 ? null : r.StatementCode,
                ApplyType = string.IsNullOrWhiteSpace(r.ApplyType) ? null : r.ApplyType,
            };

            (c.CountryOfOriginText, c.CountryOfOriginNumber) = SplitCountry(r.CountryOfOrigin);
            (c.ConsignedCountryText, c.ConsignedCountryNumber) = SplitCountry(r.ConsignedCountry);

            if (!string.IsNullOrEmpty(r.Pathaka))
            {
                c.PaThaKaId = db.PaThaKas
                    .AsNoTracking()
                    .Where(p => p.PaThaKaNo == r.Pathaka)
                    .Select(p => p.Id)
                    .FirstOrDefault();

                // The legacy lookup was `.First()`, so an unknown PaThaKa No was an HTTP 400
                // rather than an empty grid.
                c.NoRows = c.PaThaKaId == null;
            }

            return c;
        }

    }

    /// <summary>
    /// The comma-joined country selection, as both shapes the eight header tables use: the raw
    /// string for the six <c>nvarchar</c> columns, and a single parsed id for the two <c>int</c>
    /// ones — <see cref="NoMatchId"/> when the user picked more than one, since there is no
    /// single integer to compare and the legacy <c>Convert.ToInt32("5,12")</c> threw.
    ///
    /// Internal so the contract tests can pin it.
    /// </summary>
    internal static (string? Text, int? Number) SplitCountry(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (null, null);
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? (value, id)
            : (value, NoMatchId);
    }

    /// <summary>An empty, provider-agnostic result — used where the legacy code threw instead.</summary>
    private static IQueryable<sp_AdvanceSearchResult> NoResults()
        => Enumerable.Empty<sp_AdvanceSearchResult>().AsQueryable();

    /// <summary>
    /// Every ordering of the selected modes, because <c>ModeofTransport</c> is a denormalised
    /// comma-joined column and the legacy predicate matched it whole
    /// (<c>motQuery.Contains(x.ModeofTransport)</c>).
    ///
    /// The legacy builder (AdvanceSearchRepository.cs:32-74) spelled the one- and two-mode cases
    /// out by hand and read <c>modeList[3]</c> in the three-mode case -- past the end of a
    /// three-element list, so picking all three modes was an HTTP 400. Generating the
    /// permutations gives the two working cases byte for byte and makes the third work.
    ///
    /// Internal so the contract tests can pin it.
    /// </summary>
    internal static List<string>? ModeCombinations(string? modeOfTransport)
    {
        if (string.IsNullOrEmpty(modeOfTransport))
        {
            return null;
        }

        var modes = modeOfTransport.Split(',').ToList();
        var combinations = new List<string>();

        Permute(modes, new List<string>(), combinations);

        return combinations;
    }

    private static void Permute(List<string> remaining, List<string> prefix, List<string> results)
    {
        if (remaining.Count == 0)
        {
            results.Add(string.Join(",", prefix));
            return;
        }

        for (var i = 0; i < remaining.Count; i++)
        {
            var next = new List<string>(remaining);
            next.RemoveAt(i);

            prefix.Add(remaining[i]);
            Permute(next, prefix, results);
            prefix.RemoveAt(prefix.Count - 1);
        }
    }

    private static IQueryable<T> WhereIf<T>(
        this IQueryable<T> source,
        bool condition,
        Expression<Func<T, bool>> predicate)
        => condition ? source.Where(predicate) : source;

    // --- the eight branches -------------------------------------------------

    private static IQueryable<sp_AdvanceSearchResult> ImportLicenceRows(TradeNetDbContext db, Criteria c)
    {
        var licences = db.ImportLicences.AsNoTracking()
            .Where(x => x.ImportLicenceNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            .WhereIf(c.SellerCountryId != null, x => x.SellerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.ModeCombinations != null, x => c.ModeCombinations!.Contains(x.ModeofTransport))
            .WhereIf(c.MethodId != null, x => x.ExportImportMethodId == c.MethodId)
            .WhereIf(c.CountryOfOriginText != null, x => x.CountryofOriginId == c.CountryOfOriginText)
            .WhereIf(c.ConsignedCountryText != null, x => x.ConsignedCountryId == c.ConsignedCountryText)
            .WhereIf(c.IncotermId != null, x => x.ExportImportIncotermId == c.IncotermId)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.ImportLicenceItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from licence in licences
               join item in items on licence.Id equals item.ImportLicenceId
               join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on licence.PaThaKaId equals pathaka.Id
               join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   LicenceNo = licence.ImportLicenceNo,
                   LicenceDate = licence.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = licence.SellerName,
                   SellerAddress = licence.SellerAddress,
                   SellerCountry = db.Countries.Where(x => x.Id == licence.SellerCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = licence.PortofDischarge,
                   LastDate = licence.LastDate,
                   Method = method.Name,
                   CountryOfOriginText = licence.CountryofOriginId,
                   ConsignedCountryText = licence.ConsignedCountryId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = licence.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }

    private static IQueryable<sp_AdvanceSearchResult> ExportLicenceRows(TradeNetDbContext db, Criteria c)
    {
        var licences = db.ExportLicences.AsNoTracking()
            .Where(x => x.ExportLicenceNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            // The Export types filter the "Seller Country" box against the BUYER column.
            .WhereIf(c.SellerCountryId != null, x => x.BuyerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.ModeCombinations != null, x => c.ModeCombinations!.Contains(x.ModeofTransport))
            .WhereIf(c.MethodId != null, x => x.ExportImportMethodId == c.MethodId)
            .WhereIf(c.CountryOfOriginNumber != null, x => x.CountryofOriginId == c.CountryOfOriginNumber)
            .WhereIf(c.ConsignedCountryNumber != null, x => x.ConsignedCountryId == c.ConsignedCountryNumber)
            .WhereIf(c.IncotermId != null, x => x.ExportImportIncotermId == c.IncotermId)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.ExportLicenceItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from licence in licences
               join item in items on licence.Id equals item.ExportLicenceId
               join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on licence.PaThaKaId equals pathaka.Id
               join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   LicenceNo = licence.ExportLicenceNo,
                   LicenceDate = licence.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = licence.BuyerName,
                   SellerAddress = licence.BuyerAddress,
                   SellerCountry = db.Countries.Where(x => x.Id == licence.BuyerCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = licence.PortofDischarge,
                   LastDate = licence.LastDate,
                   Method = method.Name,
                   CountryOfOriginNumber = licence.CountryofOriginId,
                   ConsignedCountryNumber = licence.ConsignedCountryId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = licence.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }

    private static IQueryable<sp_AdvanceSearchResult> ImportPermitRows(TradeNetDbContext db, Criteria c)
    {
        // No Mode of Transport, Method, Incoterm or Consigned Country: ImportPermit carries none
        // of those columns, which is why the legacy branch had them commented out (:480-507).
        var permits = db.ImportPermits.AsNoTracking()
            .Where(x => x.ImportPermitNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            .WhereIf(c.SellerCountryId != null, x => x.SellerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.CountryOfOriginText != null, x => x.CountryofOriginId == c.CountryOfOriginText)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.ImportPermitItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from permit in permits
               join item in items on permit.Id equals item.ImportPermitId
               join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on permit.PaThaKaId equals pathaka.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   LicenceNo = permit.ImportPermitNo,
                   LicenceDate = permit.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = permit.AuthorisedAgentName,
                   SellerAddress = permit.AuthorisedAgentAddress,
                   SellerCountry = db.Countries.Where(x => x.Id == permit.SellerCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = permit.PortofDischarge,
                   LastDate = permit.LastDate,
                   CountryOfOriginText = permit.CountryofOriginId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = permit.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }

    private static IQueryable<sp_AdvanceSearchResult> ExportPermitRows(TradeNetDbContext db, Criteria c)
    {
        // No Method or Incoterm: ExportPermit carries neither column (legacy :372-374, :394-395).
        var permits = db.ExportPermits.AsNoTracking()
            .Where(x => x.ExportPermitNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            .WhereIf(c.SellerCountryId != null, x => x.BuyerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.ModeCombinations != null, x => c.ModeCombinations!.Contains(x.ModeofTransport))
            .WhereIf(c.CountryOfOriginText != null, x => x.CountryofOriginId == c.CountryOfOriginText)
            .WhereIf(c.ConsignedCountryNumber != null, x => x.ConsignedCountryId == c.ConsignedCountryNumber)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.ExportPermitItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from permit in permits
               join item in items on permit.Id equals item.ExportPermitId
               join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on permit.PaThaKaId equals pathaka.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   LicenceNo = permit.ExportPermitNo,
                   LicenceDate = permit.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = permit.ConsigneeName,
                   SellerAddress = permit.ConsigneeAddress,
                   // The legacy projection resolved this one from the CONSIGNED country, even
                   // though the box beside it filters on the buyer.
                   SellerCountry = db.Countries.Where(x => x.Id == permit.ConsignedCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = permit.PortofDischarge,
                   LastDate = permit.LastDate,
                   CountryOfOriginText = permit.CountryofOriginId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = permit.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }

    private static IQueryable<sp_AdvanceSearchResult> BorderImportLicenceRows(TradeNetDbContext db, Criteria c)
    {
        var licences = db.BorderImportLicences.AsNoTracking()
            .Where(x => x.ImportLicenceNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            .WhereIf(c.SellerCountryId != null, x => x.SellerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.ModeCombinations != null, x => c.ModeCombinations!.Contains(x.ModeofTransport))
            .WhereIf(c.MethodId != null, x => x.ExportImportMethodId == c.MethodId)
            .WhereIf(c.CountryOfOriginText != null, x => x.CountryofOriginId == c.CountryOfOriginText)
            .WhereIf(c.ConsignedCountryText != null, x => x.ConsignedCountryId == c.ConsignedCountryText)
            .WhereIf(c.IncotermId != null, x => x.ExportImportIncotermId == c.IncotermId)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.BorderImportLicenceItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from licence in licences
               join item in items on licence.Id equals item.BorderImportLicenceId
               join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on licence.PaThaKaId equals pathaka.Id
               join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   Sakhan = sakhan.Code,
                   LicenceNo = licence.ImportLicenceNo,
                   LicenceDate = licence.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = licence.SellerName,
                   SellerAddress = licence.SellerAddress,
                   SellerCountry = db.Countries.Where(x => x.Id == licence.SellerCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = licence.PortofDischarge,
                   LastDate = licence.LastDate,
                   Method = method.Name,
                   CountryOfOriginText = licence.CountryofOriginId,
                   ConsignedCountryText = licence.ConsignedCountryId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = licence.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }

    private static IQueryable<sp_AdvanceSearchResult> BorderExportLicenceRows(TradeNetDbContext db, Criteria c)
    {
        var licences = db.BorderExportLicences.AsNoTracking()
            .Where(x => x.ExportLicenceNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            .WhereIf(c.SellerCountryId != null, x => x.BuyerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.ModeCombinations != null, x => c.ModeCombinations!.Contains(x.ModeofTransport))
            .WhereIf(c.MethodId != null, x => x.ExportImportMethodId == c.MethodId)
            .WhereIf(c.CountryOfOriginNumber != null, x => x.CountryofOriginId == c.CountryOfOriginNumber)
            .WhereIf(c.ConsignedCountryNumber != null, x => x.ConsignedCountryId == c.ConsignedCountryNumber)
            .WhereIf(c.IncotermId != null, x => x.ExportImportIncotermId == c.IncotermId)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.BorderExportLicenceItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from licence in licences
               join item in items on licence.Id equals item.BorderExportLicenceId
               join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on licence.PaThaKaId equals pathaka.Id
               join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   Sakhan = sakhan.Code,
                   LicenceNo = licence.ExportLicenceNo,
                   LicenceDate = licence.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = licence.BuyerName,
                   SellerAddress = licence.BuyerAddress,
                   SellerCountry = db.Countries.Where(x => x.Id == licence.BuyerCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = licence.PortofDischarge,
                   LastDate = licence.LastDate,
                   Method = method.Name,
                   CountryOfOriginNumber = licence.CountryofOriginId,
                   ConsignedCountryNumber = licence.ConsignedCountryId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = licence.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }

    private static IQueryable<sp_AdvanceSearchResult> BorderImportPermitRows(TradeNetDbContext db, Criteria c)
    {
        // No Mode of Transport, Method, Incoterm or Consigned Country -- as Import Permit.
        var permits = db.BorderImportPermits.AsNoTracking()
            .Where(x => x.ImportPermitNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            .WhereIf(c.SellerCountryId != null, x => x.SellerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.CountryOfOriginText != null, x => x.CountryofOriginId == c.CountryOfOriginText)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.BorderImportPermitItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from permit in permits
               join item in items on permit.Id equals item.BorderImportPermitId
               join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on permit.PaThaKaId equals pathaka.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               join sakhan in db.Sakhans on permit.SakhanId equals sakhan.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   Sakhan = sakhan.Code,
                   LicenceNo = permit.ImportPermitNo,
                   LicenceDate = permit.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = permit.AuthorisedAgentName,
                   SellerAddress = permit.AuthorisedAgentAddress,
                   SellerCountry = db.Countries.Where(x => x.Id == permit.SellerCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = permit.PortofDischarge,
                   LastDate = permit.LastDate,
                   CountryOfOriginText = permit.CountryofOriginId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = permit.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }

    private static IQueryable<sp_AdvanceSearchResult> BorderExportPermitRows(TradeNetDbContext db, Criteria c)
    {
        // No Method or Incoterm -- as Export Permit.
        var permits = db.BorderExportPermits.AsNoTracking()
            .Where(x => x.ExportPermitNo != "")
            .Where(x => x.IssuedDate >= c.From)
            .Where(x => x.IssuedDate <= c.To)
            .WhereIf(c.PaThaKaId != null, x => x.PaThaKaId == c.PaThaKaId)
            .WhereIf(c.SectionId != null, x => x.ExportImportSectionId == c.SectionId)
            .WhereIf(c.SellerCountryId != null, x => x.BuyerCountryId == c.SellerCountryId)
            .WhereIf(c.PortOfDischarge != null, x => x.PortofDischarge == c.PortOfDischarge)
            .WhereIf(c.ModeCombinations != null, x => c.ModeCombinations!.Contains(x.ModeofTransport))
            .WhereIf(c.CountryOfOriginText != null, x => x.CountryofOriginId == c.CountryOfOriginText)
            .WhereIf(c.ConsignedCountryNumber != null, x => x.ConsignedCountryId == c.ConsignedCountryNumber)
            .WhereIf(c.StatementCodeId != null, x => x.ProductItemId == c.StatementCodeId)
            .WhereIf(c.ApplyType != null, x => x.ApplyType == c.ApplyType);

        var items = db.BorderExportPermitItems.AsNoTracking()
            .WhereIf(c.Description != null, x => x.Description!.Contains(c.Description!));

        return from permit in permits
               join item in items on permit.Id equals item.BorderExportPermitId
               join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
               join pathaka in db.PaThaKas on permit.PaThaKaId equals pathaka.Id
               join unit in db.Units on item.UnitId equals unit.Id
               join currency in db.Currencies on item.CurrencyId equals currency.Id
               join sakhan in db.Sakhans on permit.SakhanId equals sakhan.Id
               select new sp_AdvanceSearchResult
               {
                   Section = section.Code,
                   Sakhan = sakhan.Code,
                   LicenceNo = permit.ExportPermitNo,
                   LicenceDate = permit.LicenceDate,
                   CompanyRegistrationNo = pathaka.CompanyRegistrationNo,
                   CompanyName = pathaka.CompanyName,
                   UnitLevel = pathaka.UnitLevel,
                   StreetNumberStreetName = pathaka.StreetNumberStreetName,
                   QuarterCityTownship = pathaka.QuarterCityTownship,
                   State = pathaka.State,
                   Country = pathaka.Country,
                   SellerName = permit.ConsigneeName,
                   SellerAddress = permit.ConsigneeAddress,
                   SellerCountry = db.Countries.Where(x => x.Id == permit.ConsignedCountryId).Select(x => x.Name).FirstOrDefault(),
                   PortOfDischarge = permit.PortofDischarge,
                   LastDate = permit.LastDate,
                   CountryOfOriginText = permit.CountryofOriginId,
                   HSCode = item.Hscode,
                   Description = item.Description,
                   AorU = unit.Code,
                   Price = item.Price,
                   Qty = item.Quantity,
                   Value = item.Amount,
                   Currency = currency.Code,
                   Conditions = permit.Remark,
                   ItemId = item.Id,
                   ItemUniqueId = item.UniqueId,
               };
    }
}
