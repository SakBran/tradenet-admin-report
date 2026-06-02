using System;
using System.Text.Json;
using System.Threading.Tasks;
using API.DBContext;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.Extensions.DependencyInjection;

namespace API.Service.ExcelExport.Handlers
{
    /// <summary>
    /// Pilot handler for a "_Fast" report with per-chunk CSV reference resolution
    /// (Consigned/Origin countries). Mirrors the mapping the controller does, then
    /// streams resolved chunks into the workbook.
    /// </summary>
    public sealed class ImportLicenceDetailReportExcelHandler : IExcelReportJobHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public string ReportKey => "ImportLicenceDetailReport";
        public string DefaultTitle => "Import Licence Detail Report";
        public string FileNameBase => "ImportLicenceDetailReport";

        public async Task GenerateAsync(ExcelExportContext context)
        {
            var request = JsonSerializer.Deserialize<ImportLicenceDetailReportRequest>(context.RequestJson, JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize request for '{ReportKey}'.");

            var procedureRequest = new sp_ImportLicenceDetailReportRequest
            {
                Type = string.IsNullOrWhiteSpace(request.Type) ? "Oversea" : request.Type,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaThaKaTypeId = request.PaThaKaTypeId,
                ExportImportSectionId = request.ExportImportSectionId,
                ExportImportMethodId = request.ExportImportMethodId,
                ExportImportIncotermId = request.ExportImportIncotermId,
                SellerCountryId = request.SellerCountryId,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                SakhanId = request.SakhanId,
            };

            var db = context.Services.GetRequiredService<TradeNetDbContext>();
            var countryCache = context.Services.GetRequiredService<ICountryCache>();

            await foreach (var chunk in sp_ImportLicenceDetailReport_Fast.StreamResolvedChunksAsync(
                db, countryCache, procedureRequest, context.ChunkSize, context.CancellationToken))
            {
                context.Writer.AppendRows(chunk);
            }
        }
    }
}
