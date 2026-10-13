using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;

namespace API.Service.Reports;

public sealed class ReportAccessService
{
    private readonly TradeNetDbContext _db;
    private bool _resolved;
    private ReportAccessPolicy? _access;

    public ReportAccessService(TradeNetDbContext db) => _db = db;

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
        _access = ReportAccessPolicy.Create(user.UserType, details);
        return _access;
    }
}
