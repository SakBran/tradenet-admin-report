using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace API.Service.Reports;

public sealed class ReportAccessService
{
    private readonly TradeNetDbContext _db;
    private readonly IConfiguration _configuration;
    private bool _resolved;
    private ReportAccessPolicy? _access;

    public ReportAccessService(TradeNetDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<ReportAccessPolicy?> GetAsync(ClaimsPrincipal principal)
    {
        if (_resolved) return _access;
        _resolved = true;

        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.Name), out var userId))
            return null;

        var user = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.UserType })
            .FirstOrDefaultAsync();
        if (user == null) return null;

        var details = await _db.UserDetails.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
        var viewAllReports = _configuration.GetSection("ReportAccess:ViewAllReportUserIds")
            .GetChildren()
            .Any(entry => int.TryParse(entry.Value, out var configuredId) && configuredId == userId);
        _access = ReportAccessPolicy.Create(user.UserType, details, viewAllReports);
        return _access;
    }
}
