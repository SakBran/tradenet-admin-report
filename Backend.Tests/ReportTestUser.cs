using System.Security.Claims;

namespace Backend.Tests;

internal static class ReportTestUser
{
    internal static ClaimsPrincipal AuthenticatedPrincipal { get; } = new(
        new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "report-test-user"),
                new Claim(ClaimTypes.Name, "Report Test User")
            ],
            "TestBearer"));
}
