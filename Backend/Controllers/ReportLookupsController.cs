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
}
