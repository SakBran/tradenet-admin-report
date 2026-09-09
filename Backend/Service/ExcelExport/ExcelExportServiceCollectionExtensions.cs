using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

            // The footer numbers are read by replaying the report's own Post, which needs
            // the scoped DbContext the export job already runs inside.
            services.AddScoped<IExcelFooterTotalsResolver, DefaultExcelFooterTotalsResolver>();
            services.TryAddSingleton(TimeProvider.System);

            // Registered here rather than in Program.cs so the whole feature stays one
            // AddExcelExportQueue call.
            services.Configure<MvcOptions>(options => options.Filters.Add<RequireExcelPresentationSpecFilter>());

            services.AddHostedService<ExcelExportWorker>();
            services.AddHostedService<ExcelExportCleanupWorker>();

            RegisterReportHandlers(services);

            // The DCCA export copies eight OOXML parts out of an embedded workbook. Check it at
            // startup so a bad resource shows up in the log rather than as a broken download
            // hours later — but LOG, never throw: the DCCA variant of one report must not be
            // able to take down an API serving ~160 others.
            var skeletonProblems = Dcca.DccaWorkbookSkeleton.Validate();
            if (skeletonProblems.Count > 0)
            {
                services.AddSingleton<IHostedService>(provider => new DccaSkeletonProblemReporter(
                    provider.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("API.Service.ExcelExport.Dcca"),
                    skeletonProblems));
            }

            return services;
        }

        /// <summary>
        /// Reports a broken DCCA skeleton once, at startup, through the configured logger.
        /// A hosted service (rather than a log call here) because <see cref="AddExcelExportQueue"/>
        /// runs while the container is still being built and has no logger yet.
        /// </summary>
        private sealed class DccaSkeletonProblemReporter : IHostedService
        {
            private readonly ILogger _logger;
            private readonly IReadOnlyList<string> _problems;

            internal DccaSkeletonProblemReporter(ILogger logger, IReadOnlyList<string> problems)
            {
                _logger = logger;
                _problems = problems;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                foreach (var problem in _problems)
                {
                    _logger.LogError(
                        "The DCCA export skeleton is invalid and that export will fail: {Problem}",
                        problem);
                }

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
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
                // + Generation: the shared sheet shape (header block, footer rows, freeze
                // pane) changed, so every report's cached files must be invalidated, not
                // just the ones that also bumped their own version.
                var formatVersion = (type.GetCustomAttribute<ExcelFormatVersionAttribute>()?.Version ?? 1)
                    + ExcelExportFormat.Generation;
                var handler = new ControllerStreamingExcelReportJobHandler(
                    type,
                    reportKey,
                    PrettifyTitle(reportKey),
                    reportKey,
                    formatVersion);

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
