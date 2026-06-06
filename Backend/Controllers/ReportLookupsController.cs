using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportLookupsController : ControllerBase
    {
        private const string CacheKeyPrefix = "ReportLookups:";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

        // BusinessType is a shared table partitioned by FormType (Pa Tha Ka, Whole Sale,
        // Retail, Duty Free Shop, ...). The business-type dropdown for these reports must
        // only list the "Pa Tha Ka" entries, mirroring the legacy admin which called
        // BusinessTypeRepository.GetAll(AppConfig.PaThaKa). Without this filter the dropdown
        // shows every form type's "Trading" et al., and selecting a non-PaThaKa id returns
        // no rows because PaThaKa records only reference Pa Tha Ka business type ids.
        private const string PaThaKaFormType = "Pa Tha Ka";
        private const string ImportLicenceFormType = "Import Licence";
        private const string ImportPermitFormType = "Import Permit";
        private const string ExportLicenceFormType = "Export Licence";
        private const string ImportTradeType = "Import";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;

        public ReportLookupsController(TradeNetDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("{lookupName}")]
        public async Task<ActionResult<List<ReportLookupOption>>> Get(string lookupName)
        {
            var key = lookupName.ToLowerInvariant();

            Func<Task<List<ReportLookupOption>>>? loadOptions = key switch
            {
                "amendremarks" => GetAmendRemarks,
                "businesstypes" => GetBusinessTypes,
                "countries" => GetCountries,
                "chequenos" => GetChequeNos,
                "exportimportincoterms" => GetExportImportIncoterms,
                "exportimportmethods" => GetExportImportMethods,
                "exportimportsections" => GetExportImportSections,
                "importlicenceincoterms" => GetImportLicenceIncoterms,
                "importlicencemethods" => GetImportLicenceMethods,
                "importlicencesections" => GetImportLicenceSections,
                "importpermitsections" => GetImportPermitSections,
                "borderexportlicencesections" => GetBorderExportLicenceSections,
                "lineofbusinesses" => GetLineofBusinesses,
                "nrcprefixcodes" => GetNrcprefixCodes,
                "nrcprefixes" => GetNrcprefixes,
                "ogadepartments" => GetOgaDepartments,
                "ogasections" => GetOgaSections,
                "pathakatypes" => GetPaThaKaTypes,
                "paymenttypes" => GetPaymentTypes,
                "sakhans" => GetSakhans,
                _ => null,
            };

            if (loadOptions is null)
            {
                return NotFound();
            }

            var options = await _cache.GetOrCreateAsync(
                CacheKeyPrefix + key,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                    return await loadOptions();
                });

            return Ok(options ?? new List<ReportLookupOption>());
        }

        [HttpGet("company-name")]
        public async Task<ActionResult<CompanyNameLookupResult>> GetCompanyName(
            [FromQuery] string companyRegistrationNo)
        {
            var registrationNo = companyRegistrationNo?.Trim() ?? string.Empty;

            if (registrationNo == string.Empty)
            {
                return Ok(new CompanyNameLookupResult(string.Empty, string.Empty));
            }

            var companyName = await _context.PaThaKas
                .AsNoTracking()
                .Where(item => item.CompanyRegistrationNo == registrationNo)
                .OrderByDescending(item => item.CreatedDate)
                .Select(item => item.CompanyName)
                .FirstOrDefaultAsync();

            return Ok(new CompanyNameLookupResult(registrationNo, companyName ?? string.Empty));
        }

        // Structured NRC prefix data for the cascading State/Region -> Township picker used by
        // the List of Directors report. Deliberately SEPARATE from the flat "nrcprefixes" lookup:
        // that one feeds a single-select whose submitted value is the township Id, so adding
        // StatePrefix to it would change what those selects post. Here the frontend gets every
        // township already tagged with its StatePrefix and builds the State dropdown + filters
        // the township dropdown entirely client-side from a single fetch.
        [HttpGet("nrc-prefixes")]
        public async Task<ActionResult<List<NrcPrefixLookupOption>>> GetNrcPrefixOptions()
        {
            var options = await _cache.GetOrCreateAsync(
                CacheKeyPrefix + "nrc-prefixes-structured",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                    return await _context.Nrcprefixes
                        .AsNoTracking()
                        .Where(item => item.IsActive && !item.IsDeleted)
                        .OrderBy(item => item.StatePrefix)
                        .ThenBy(item => item.TownshipPrefix)
                        .Select(item => new NrcPrefixLookupOption(
                            item.Id, item.StatePrefix, item.TownshipPrefix))
                        .ToListAsync();
                });

            return Ok(options ?? new List<NrcPrefixLookupOption>());
        }

        private Task<List<ReportLookupOption>> GetAmendRemarks() =>
            _context.LicencePermitAmendRemarks
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetBusinessTypes() =>
            _context.BusinessTypes
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted && item.FormType == PaThaKaFormType)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetCountries() =>
            _context.Countries
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code ?? string.Empty, item.Name ?? string.Empty))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetChequeNos() =>
            _context.ChequeNos
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code ?? string.Empty, item.Name ?? string.Empty))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetExportImportIncoterms() =>
            _context.ExportImportIncoterms
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetExportImportMethods() =>
            _context.ExportImportMethods
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetExportImportSections() =>
            _context.ExportImportSections
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetImportLicenceIncoterms() =>
            _context.ExportImportIncoterms
                .AsNoTracking()
                .Where(item =>
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.Type == ImportTradeType &&
                    item.IsOversea)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetImportLicenceMethods() =>
            _context.ExportImportMethods
                .AsNoTracking()
                .Where(item =>
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.Type == ImportTradeType &&
                    item.IsOversea)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetImportLicenceSections() =>
            _context.ExportImportSections
                .AsNoTracking()
                .Where(item =>
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.Type == ImportLicenceFormType &&
                    item.IsOversea)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        // Import-only Oversea Permit sections (legacy: GetAll(AppConfig.ImportPermit) where IsOversea).
        // Pinned by the ImportPermit report Section filters so the dropdown does not leak the
        // generic exportImportSections list (Import + Export + Permit) — customer complaint.
        private Task<List<ReportLookupOption>> GetImportPermitSections() =>
            _context.ExportImportSections
                .AsNoTracking()
                .Where(item =>
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.Type == ImportPermitFormType &&
                    item.IsOversea)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        // Border Export Licence sections (legacy: GetAll(AppConfig.ExportLicence) where IsBorder).
        private Task<List<ReportLookupOption>> GetBorderExportLicenceSections() =>
            _context.ExportImportSections
                .AsNoTracking()
                .Where(item =>
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.Type == ExportLicenceFormType &&
                    item.IsBorder)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Name))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetLineofBusinesses() =>
            _context.LineofBusinesses
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code ?? string.Empty, item.Name ?? string.Empty))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetNrcprefixCodes() =>
            _context.NrcprefixCodes
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.Code)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Description))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetNrcprefixes() =>
            _context.Nrcprefixes
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.StatePrefix)
                .ThenBy(item => item.TownshipPrefix)
                .Select(item => new ReportLookupOption(
                    item.Id,
                    item.TownshipPrefix,
                    item.StatePrefix + "/" + item.TownshipPrefix))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetOgaDepartments() =>
            _context.Ogadepartments
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.EnglishName)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.EnglishName ?? string.Empty))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetOgaSections() =>
            _context.Ogasections
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.EnglishName)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.EnglishName ?? string.Empty))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetPaThaKaTypes() =>
            _context.PaThaKaTypes
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Description)
                .Select(item => new ReportLookupOption(item.Id, item.Code, item.Description))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetPaymentTypes() =>
            _context.PaymentTypes
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(
                    item.Id,
                    string.Empty,
                    item.Name,
                    item.Id.ToString()))
                .ToListAsync();

        private Task<List<ReportLookupOption>> GetSakhans() =>
            _context.Sakhans
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new ReportLookupOption(item.Id, item.Code ?? string.Empty, item.Name ?? string.Empty))
                .ToListAsync();
    }

    public sealed record ReportLookupOption(int Id, string Code, string Label)
    {
        public string? Value { get; init; }

        public ReportLookupOption(int id, string code, string label, string? value)
            : this(id, code, label)
        {
            Value = value;
        }
    }

    public sealed record CompanyNameLookupResult(string CompanyRegistrationNo, string CompanyName);

    // Township row tagged with its parent StatePrefix so the List of Directors report can build
    // a State/Region -> Township cascade on the client from one fetch.
    public sealed record NrcPrefixLookupOption(int Id, int StatePrefix, string TownshipPrefix);
}
