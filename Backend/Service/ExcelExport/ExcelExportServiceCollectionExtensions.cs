using System;
using API.Service.ExcelExport.Handlers;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API.Service.ExcelExport
{
    public static class ExcelExportServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the async Excel export queue: options, file store, registry,
        /// enqueue service, background worker + cleanup, and the per-report handlers.
        /// Add one handler line here per report when rolling out the remaining controllers.
        /// </summary>
        public static IServiceCollection AddExcelExportQueue(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ExcelExportOptions>(configuration.GetSection(ExcelExportOptions.SectionName));

            services.AddSingleton<IExcelExportFileStore, ExcelExportFileStore>();
            services.AddSingleton<ExcelReportJobRegistry>();
            services.AddScoped<IExcelExportJobService, ExcelExportJobService>();

            services.AddHostedService<ExcelExportWorker>();
            services.AddHostedService<ExcelExportCleanupWorker>();

            RegisterHandlers(services);

            return services;
        }

        private static void RegisterHandlers(IServiceCollection services)
        {
            // --- Pilot: simple IQueryable report ---
            services.AddSingleton<IExcelReportJobHandler>(
                new QueryableExcelReportJobHandler<MemberRegistrationReportRequest, sp_MemberRegistrationReportResult>(
                    reportKey: "MemberRegistrationReport",
                    title: "Member Registration Report",
                    fileNameBase: "MemberRegistrationReport",
                    queryFactory: (db, req) => sp_MemberRegistrationReport.Query(db, new sp_MemberRegistrationReportRequest
                    {
                        FromDate = req.FromDate,
                        ToDate = req.ToDate,
                        ApplyType = NormalizeApplyType(req.ApplyType)
                    })));

            // --- Pilot: _Fast report with per-chunk CSV reference resolution ---
            services.AddSingleton<IExcelReportJobHandler, ImportLicenceDetailReportExcelHandler>();

            // Roll out the remaining reports here, one AddSingleton<IExcelReportJobHandler>(...) per report.
        }

        /// <summary>Mirrors MemberRegistrationReportController's ApplyType normalization.</summary>
        private static string NormalizeApplyType(string? applyType)
        {
            var value = string.IsNullOrWhiteSpace(applyType) ? "All" : applyType.Trim();
            return value.ToUpperInvariant() switch
            {
                "NEW" => "New",
                "EXTENSION" => "Extension",
                _ => "All"
            };
        }
    }
}
