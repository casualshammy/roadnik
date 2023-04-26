using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Roadnik.Attributes;
using Roadnik.Interfaces;
using System.Reflection;

namespace Roadnik.Toolkit;

public class ApiKeyFilter : IAuthorizationFilter
{
  private readonly ISettings p_settings;

  public ApiKeyFilter(ISettings _settings)
  {
    p_settings = _settings;
  }

  public void OnAuthorization(AuthorizationFilterContext _context)
  {
    var desc = _context.ActionDescriptor as ControllerActionDescriptor;
    var method = desc?.MethodInfo;
    int? errorStatusCode = null;

    var apiRequiredAttr = method?.GetCustomAttribute<ApiKeyRequiredAttribute>();

    if (apiRequiredAttr != null)
    {
      var httpCtx = _context.HttpContext;
      var apiKeyStr = httpCtx.Request.Headers["apiKey"].FirstOrDefault();

      if (string.IsNullOrWhiteSpace(p_settings.AdminApiKey))
        errorStatusCode = StatusCodes.Status403Forbidden;
      else if (p_settings.AdminApiKey != apiKeyStr)
        errorStatusCode = StatusCodes.Status403Forbidden;
    }

    if (errorStatusCode != null)
    {
      _context.Result = new ObjectResult("Forbidden!")
      {
        StatusCode = errorStatusCode
      };
    }
  }

}
