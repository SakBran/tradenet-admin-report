using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API.Service.ExcelExport
{
    public static class ExcelExportServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the async Excel export queue: options, file store, registry,
        /// enqueue service, background worker + cleanup, and one handler per report.
        /// Handlers are auto-discovered: every controller implementing
        /// <see cref="IStreamingExcelReport"/> gets a
        /// <see cref="ControllerStreamingExcelReportJobHandler"/>.
        /// </summary>
        public static IServiceCollection AddExcelExportQueue(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ExcelExportOptions>(configuration.GetSection(ExcelExportOptions.SectionName));

            // Storage backend: "Ftp" uploads to an FTP server; anything else (default) writes to disk.
            var storage = configuration[$"{ExcelExportOptions.SectionName}:Storage"];
            if (string.Equals(storage, "Ftp", StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<IExcelExportFileStore, FtpExcelExportFileStore>();
            }
            else
            {
                services.AddSingleton<IExcelExportFileStore, ExcelExportFileStore>();
            }
            services.AddSingleton<ExcelReportJobRegistry>();
            services.AddScoped<IExcelExportJobService, ExcelExportJobService>();

            services.AddHostedService<ExcelExportWorker>();
            services.AddHostedService<ExcelExportCleanupWorker>();

            RegisterReportHandlers(services);

            return services;
        }

        private static void RegisterReportHandlers(IServiceCollection services)
        {
            var reportTypes = typeof(ExcelExportServiceCollectionExtensions).Assembly
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                    && typeof(IStreamingExcelReport).IsAssignableFrom(t));

            foreach (var type in reportTypes)
            {
                var reportKey = StripControllerSuffix(type.Name);
                var handler = new ControllerStreamingExcelReportJobHandler(
                    type,
                    reportKey,
                    PrettifyTitle(reportKey),
                    reportKey);

                services.AddSingleton<IExcelReportJobHandler>(handler);
            }
        }

        private static string StripControllerSuffix(string typeName)
            => typeName.EndsWith("Controller", StringComparison.Ordinal)
                ? typeName[..^"Controller".Length]
                : typeName;

        /// <summary>Insert spaces before internal capitals: "BorderExportLicence" → "Border Export Licence".</summary>
        private static string PrettifyTitle(string name)
        {
            var sb = new StringBuilder(name.Length + 8);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                {
                    sb.Append(' ');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
