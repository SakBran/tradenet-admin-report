using System.Reflection;
using API.Service.ExcelExport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

/// <summary>
/// The whole feature is wired by one AddExcelExportQueue call, including the pieces
/// (the spec filter, the footer resolver, the cache-invalidating version bump) that are
/// silent when missing.
/// </summary>
public sealed class ExcelExportRegistrationTests
{
    private static ServiceCollection Registered()
    {
        var services = new ServiceCollection();
        services.AddExcelExportQueue(new ConfigurationBuilder().Build());
        return services;
    }

    [Fact]
    public void Every_report_handler_carries_the_shared_generation_on_top_of_its_own_version()
    {
        var handlers = Registered()
            .Where(descriptor => descriptor.ServiceType == typeof(IExcelReportJobHandler))
            .Select(descriptor => (ControllerStreamingExcelReportJobHandler)descriptor.ImplementationInstance!)
            .ToList();

        Assert.NotEmpty(handlers);

        foreach (var handler in handlers)
        {
            var declared = handler.ControllerType.GetCustomAttribute<ExcelFormatVersionAttribute>()?.Version ?? 1;

            Assert.Equal(declared + ExcelExportFormat.Generation, handler.FormatVersion);
            Assert.True(
                handler.FormatVersion >= 2,
                $"{handler.ReportKey} would keep serving files generated in the old sheet shape.");
        }
    }

    [Fact]
    public void Account_summary_is_at_generation_three()
    {
        var handler = Registered()
            .Where(descriptor => descriptor.ServiceType == typeof(IExcelReportJobHandler))
            .Select(descriptor => (ControllerStreamingExcelReportJobHandler)descriptor.ImplementationInstance!)
            .Single(h => h.ReportKey == "AccountSummaryReport");

        Assert.Equal(3, handler.FormatVersion);
        Assert.True(handler.HasTypedLayout);
    }

    [Fact]
    public void The_footer_resolver_and_a_TimeProvider_are_available_to_the_worker()
    {
        var services = Registered();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IExcelFooterTotalsResolver)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void The_spec_filter_is_registered_globally_without_touching_Program()
    {
        var options = Registered().BuildServiceProvider().GetRequiredService<IOptions<MvcOptions>>().Value;

        Assert.Contains(
            options.Filters,
            filter => filter is TypeFilterAttribute typed
                ? typed.ImplementationType == typeof(RequireExcelPresentationSpecFilter)
                : filter.GetType() == typeof(RequireExcelPresentationSpecFilter));
    }
}
