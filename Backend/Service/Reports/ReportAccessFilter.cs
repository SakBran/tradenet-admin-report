using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Service.Reports;

public sealed class ReportAccessFilter : IAsyncAuthorizationFilter
{
    private readonly ReportAccessService _accessService;

    public ReportAccessFilter(ReportAccessService accessService) => _accessService = accessService;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action ||
            action.ControllerTypeInfo.Namespace != "Backend.Controllers.Report")
            return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var access = await _accessService.GetAsync(context.HttpContext.User);
        var controller = action.ControllerName;
        if (access == null ||
            (controller.EndsWith("DataImport", StringComparison.Ordinal) && !access.IsAdmin) ||
            !access.CanAccess(controller))
            context.Result = new ForbidResult();
    }
}
