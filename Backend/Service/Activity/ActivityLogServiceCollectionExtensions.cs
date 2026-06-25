using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API.Service.Activity
{
    public static class ActivityLogServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the user-activity audit log: options, the in-memory queue, the
        /// background batch writer and the retention cleanup worker. The capturing
        /// middleware (<see cref="API.Middleware.ActivityLoggingMiddleware"/>) is added
        /// separately in the request pipeline.
        /// </summary>
        public static IServiceCollection AddActivityLogging(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ActivityLogOptions>(configuration.GetSection(ActivityLogOptions.SectionName));
            services.AddSingleton<IActivityLogQueue, ActivityLogQueue>();
            services.AddHostedService<ActivityLogWriterWorker>();
            services.AddHostedService<ActivityLogCleanupWorker>();
            return services;
        }
    }
}
