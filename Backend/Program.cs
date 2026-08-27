using API.DBContext;
using API.Interface;
using API.Middleware;
using API.Service;
using API.Service.Activity;
using API.Service.ExcelExport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

// Public so Backend.Tests can boot the app via WebApplicationFactory<Program> (system tests).
public class Program
{
    /// <summary>
    /// Browser origins allowed to call this API. Compiled in because deploy.ps1 excludes
    /// appsettings*.json from the file-share copy — the list has to be correct with no config
    /// present on the server. Cors:AllowedOrigins overrides it when set.
    /// </summary>
    private static readonly string[] DefaultCorsOrigins =
    {
        // Apps running in Capacitor use capacitor://localhost (iOS) or http://localhost (Android)
        // as their origin, which is why those two are here alongside the web origins.
        "https://vehicle.myanmartradenet.com",
        "https://testingvehicle.myanmartradenet.com",
        "https://www.mpu-ecommerce.com",
        "https://www.mpuecomuat.com",
        "https://report.myanmartradenet.com",
        "capacitor://localhost",
        "http://localhost",
        "https://localhost",
        "http://localhost:3000",
        "http://localhost:5173",
        "http://localhost:8100",
    };

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();

        #region Cors
        // One default policy, registered here instead of being built inline in UseCors, so
        // endpoint metadata ([EnableCors]/[DisableCors]) can apply to it later if ever needed.
        // Note AllowAnyMethod/AllowAnyHeader *clear* any WithMethods/WithHeaders list, so listing
        // individual verbs alongside them (as this used to) is dead code.
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(ResolveCorsOrigins(builder.Configuration))
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                // Report pages download Excel/PDF blobs; without this the browser hides the
                // filename header from the JavaScript that reads it.
                .WithExposedHeaders("Content-Disposition")
                // Cache preflights so each report call does not pay for an extra round trip.
                .SetPreflightMaxAge(TimeSpan.FromHours(1)));
        });
        #endregion

        // Trusted proxies are opt-in. Nothing is documented in front of IIS, and honouring
        // X-Forwarded-* from any caller would let a client forge its IP into the activity log and
        // forge "https" past the HTTPS redirect — so the framework default (loopback only) stays
        // unless the server names a proxy in config.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            foreach (var proxy in builder.Configuration.GetSection("Forwarded:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (var network in builder.Configuration.GetSection("Forwarded:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
            {
                var parts = network.Split('/', 2);
                if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var prefixLength))
                {
                    // Fully qualified: System.Net also has an IPNetwork, but KnownNetworks on
                    // net8.0 takes the ASP.NET Core one.
                    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
                }
            }
        });

        // Gives unhandled exceptions a JSON body with a traceId instead of an empty 500, so a
        // user's screenshot can be matched to the logged stack trace (which stays server-side).
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });

        builder.Services.AddMemoryCache();
        // App-wide country list: loaded lazily on first report request and refreshed on demand
        // once stale (request-driven TTL, see CountryCache.Ttl), read in-memory by reports.
        builder.Services.AddSingleton<API.Service.Reports.ICountryCache, API.Service.Reports.CountryCache>();
        builder.Services.AddScoped<API.Service.Reports.IDataImportService, API.Service.Reports.DataImportService>();
        builder.Services.AddScoped<API.Service.Reports.IDataImportJobService, API.Service.Reports.DataImportJobService>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "TradeNet Admin Report API",
                Version = "v1",
                Description = "Swagger UI for testing report pagination and Excel export APIs."
            });

            options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter a JWT bearer token to test authorized endpoints."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
        builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("TemplateDB") ?? builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddDbContextPool<TradeNetDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("TradeNetDBTest"),
                // The report queries fan out over large licence/permit + item tables; the
                // default 30s command timeout throws "Execution Timeout Expired" on heavy
                // date ranges. Raise it so slow-but-valid reports complete (the indexes /
                // fast-pagination changes bring most well under this ceiling).
                sql => sql.CommandTimeout(180)));
        builder.Services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(o =>
        {
            var jwtKey = builder.Configuration["JWT:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT:Key configuration value is missing.");
            }
            var Key = Encoding.UTF8.GetBytes(jwtKey);
            o.SaveToken = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JWT:Issuer"],
                ValidAudience = builder.Configuration["JWT:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Key)
            };
        });
        builder.Services.AddScoped<IJWTManagerService, JWTManagerService>();
        builder.Services.AddScoped(typeof(ICommonService<>), typeof(CommonService<>));
        // Async Excel export queue: jobs in TemplateDB, files on disk, background worker.
        builder.Services.AddExcelExportQueue(builder.Configuration);
        // User-activity audit log: in-memory queue + background batch writer + retention cleanup (TemplateDB).
        builder.Services.AddActivityLogging(builder.Configuration);
        // Daily TemplateDB import: at 1:00 AM local server time, import yesterday for all licence/permit types.
        builder.Services.AddHostedService<API.Service.Reports.DataImportScheduleWorker>();
        // Manual TemplateDB imports: queued by API and processed outside the request pipeline.
        builder.Services.AddHostedService<API.Service.Reports.DataImportWorker>();

        var app = builder.Build();

        // Resolve the export file store eagerly so the resolved storage root is logged at startup
        // (it's I/O-free now), making it easy to verify the path right after a deploy.
        app.Services.GetRequiredService<API.Service.ExcelExport.IExcelExportFileStore>();

        // Configure the HTTP request pipeline. Order below is load-bearing — each step notes why.

        // FIRST, before anything reads the scheme or the client IP: rewrites both from the
        // X-Forwarded-* headers of a trusted proxy. Behind HTTPS redirection / HSTS (as it used to
        // sit) it is too late to matter, and the activity log records the proxy instead of the user.
        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // Wraps the whole pipeline: without it an unhandled exception returns a bare 500 with
            // no body, which the UI can only render as "no data". Writes ProblemDetails + traceId
            // (see AddProblemDetails) and logs the exception itself.
            app.UseExceptionHandler();
            app.UseHsts();
        }

        // Exactly once, and after UseForwardedHeaders so a proxied HTTPS request is not seen as
        // http and bounced. It must also stay above UseCors: a redirect carries no CORS headers,
        // and a redirected preflight fails outright in every browser.
        app.UseHttpsRedirection();

        // Not in Production: Swagger would publish the whole API surface + schema on
        // reportapi.myanmartradenet.com. Set ASPNETCORE_ENVIRONMENT=Staging on a server that needs it.
        if (!app.Environment.IsProduction())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "TradeNet Admin Report API v1");
                options.RoutePrefix = "swagger";
                options.DisplayRequestDuration();
            });
        }

        // Chat attachments: the UI renders VITE_IMAGE_URL + filename => GET /Image/<file>, written
        // by UploadController. Served from an explicit provider rather than the wwwroot web root,
        // because that root is resolved once at startup and a fresh deploy ships no wwwroot — with
        // a plain UseStaticFiles() the uploads 404 until the next restart. Registered before
        // routing, and deliberately outside auth because an <img> tag cannot send a bearer token.
        var imageRoot = ImageStorage.ResolveRoot(app.Environment, app.Configuration);
        try
        {
            Directory.CreateDirectory(imageRoot);
        }
        catch (Exception ex)
        {
            // Never fail startup over this: point Images:Root at a writable path instead.
            app.Logger.LogWarning(ex, "Could not create the image folder {ImageRoot}.", imageRoot);
        }

        if (Directory.Exists(imageRoot))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(imageRoot),
                RequestPath = "/Image",
            });
            app.Logger.LogInformation("Serving /Image from {ImageRoot}.", imageRoot);
        }
        else
        {
            app.Logger.LogWarning("Image folder {ImageRoot} is unavailable; /Image returns 404.", imageRoot);
        }

        app.UseRouting();
        // After UseRouting so the endpoint's CORS metadata is visible, and before the auth
        // middleware so a rejected request still comes back with CORS headers.
        app.UseCors();
        // Above UseAuthentication so requests that authorization rejects (401/403) are audited too
        // — AuthorizationMiddleware short-circuits, so anything below it never sees them. Claims and
        // the status code are still captured: the middleware reads them after awaiting the pipeline.
        // Non-blocking: entries are queued and written by a background worker.
        app.UseMiddleware<ActivityLoggingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        // Lightweight, anonymous liveness probe used by deploy.ps1 / CI to confirm the app
        // restarted successfully after a deployment. Must not require auth or hit the database.
        app.MapGet("/health", () => "ok").AllowAnonymous();
        app.Run();
    }

    /// <summary>
    /// Cors:AllowedOrigins when configured, otherwise <see cref="DefaultCorsOrigins"/>. Trailing
    /// slashes are stripped because an Origin header never has one — "https://host/" would be a
    /// silent dead entry.
    /// </summary>
    private static string[] ResolveCorsOrigins(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        var origins = (configured ?? Array.Empty<string>())
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // A present-but-unusable key (empty array, blank entries) must not lock every browser out.
        return origins.Length > 0 ? origins : DefaultCorsOrigins;
    }
}
