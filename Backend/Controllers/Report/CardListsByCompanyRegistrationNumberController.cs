using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Controllers.Report
{
    /// <summary>
    /// Composite "Card Lists By Company Registration Number" (Pathaka) detail report.
    /// Restores the old TradeNet admin RDLC layout: company header + Permit Business,
    /// Director Info and every related card type for a single company. This is a
    /// document-style report keyed by one CompanyRegistrationNo (no paging / no Excel).
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CardListsByCompanyRegistrationNumberController : ControllerBase
    {
        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;

        public CardListsByCompanyRegistrationNumberController(
            TradeNetDbContext context,
            IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpPost("Detail")]
        public async Task<ActionResult<CardListsByPaThaKaDetailResult>> Detail(
            [FromBody] CardListsByCompanyRegistrationNumberRequest? request)
        {
            var registrationNo = request?.CompanyRegistrationNo?.Trim();
            if (string.IsNullOrWhiteSpace(registrationNo))
            {
                return BadRequest("Company Registration No is required.");
            }

            // 1. Company header
            var companyRows = await sp_CardListsByPaThaKaReport.ExecuteAsync(
                _context,
                new sp_CardListsByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo });
            var company = companyRows.FirstOrDefault()?.ToResult();

            if (company == null)
            {
                return Ok(new CardListsByPaThaKaDetailResult
                {
                    CompanyRegistrationNo = registrationNo,
                });
            }

            // 2. Permit Business descriptions
            var permitBusinesses = await sp_PermitBusinessByPaThaKaReport
                .Query(_context, new sp_PermitBusinessByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo })
                .Where(row => row.Description != null && row.Description != "")
                .Select(row => row.Description!)
                .ToListAsync();

            // 3. Director info
            var directors = await sp_DirectorByPaThaKaReport
                .Query(_context, new sp_DirectorByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo })
                .ToListAsync();

            // 4..N. Related cards
            var wholeSaleRetail = await sp_WholeSaleAndRetailByPaThaKaReport
                .Query(_context, new sp_WholeSaleAndRetailByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo })
                .ToListAsync();

            var alcoholicBeverages = await sp_WineImportationByPaThaKaReport_Fast.GetAllResolvedAsync(
                _context, _cache, new sp_WineImportationByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo });

            var businessServiceAgency = await sp_BusinessServiceAgencyByPaThakaReport
                .Query(_context, new sp_BusinessServiceAgencyByPaThakaReportRequest { CompanyRegistrationNo = registrationNo })
                .ToListAsync();

            var reExport = await sp_ReExportByPaThaKaReport
                .Query(_context, new sp_ReExportByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo })
                .ToListAsync();

            var saleCenter = await sp_SaleCenterByPaThaKaReport
                .Query(_context, new sp_SaleCenterByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo })
                .ToListAsync();

            var showRoom = await sp_ShowRoomByPaThaKaReport
                .Query(_context, new sp_ShowRoomByPaThaKaReportRequest { CompanyRegistrationNo = registrationNo })
                .ToListAsync();

            var dutyFreeShop = await sp_DutyFreeShopByReport
                .Query(_context, new sp_DutyFreeShopByReportRequest { CompanyRegistrationNo = registrationNo })
                .ToListAsync();

            return Ok(new CardListsByPaThaKaDetailResult
            {
                CompanyRegistrationNo = registrationNo,
                Company = company,
                PermitBusinesses = permitBusinesses,
                Directors = directors,
                WholeSaleRetail = wholeSaleRetail,
                AlcoholicBeverages = alcoholicBeverages,
                BusinessServiceAgency = businessServiceAgency,
                ReExport = reExport,
                SaleCenter = saleCenter,
                ShowRoom = showRoom,
                DutyFreeShop = dutyFreeShop,
            });
        }
    }

    public sealed class CardListsByCompanyRegistrationNumberRequest
    {
        public string CompanyRegistrationNo { get; set; } = string.Empty;
    }

    public sealed class CardListsByPaThaKaDetailResult
    {
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public sp_CardListsByPaThaKaReportResult? Company { get; set; }
        public List<string> PermitBusinesses { get; set; } = new();
        public List<sp_DirectorByPaThaKaReportResult> Directors { get; set; } = new();
        public List<sp_WholeSaleAndRetailByPaThaKaReportResult> WholeSaleRetail { get; set; } = new();
        public List<sp_WineImportationByPaThaKaReportResult> AlcoholicBeverages { get; set; } = new();
        public List<sp_BusinessServiceAgencyByPaThakaReportResult> BusinessServiceAgency { get; set; } = new();
        public List<sp_ReExportByPaThaKaReportResult> ReExport { get; set; } = new();
        public List<sp_SaleCenterByPaThaKaReportResult> SaleCenter { get; set; } = new();
        public List<sp_ShowRoomByPaThaKaReportResult> ShowRoom { get; set; } = new();
        public List<sp_DutyFreeShopByReportResult> DutyFreeShop { get; set; } = new();
    }
}
