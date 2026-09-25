using System.Security.Claims;
using System.Text.Json;

namespace SmartHotel.Identity.API.Middleware;

public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var mustChangePasswordClaim = user.FindFirst("must_change_password")?.Value;
            if (string.Equals(mustChangePasswordClaim, "true", StringComparison.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Value?.TrimEnd('/');
                // Public login must remain callable even if a browser accidentally
                // sends an existing restricted bearer token with the request.
                var isLogin = string.Equals(path, "/api/v1/auth/employee/login", StringComparison.OrdinalIgnoreCase);
                if (!isLogin && !string.Equals(path, "/api/v1/auth/change-password", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    var response = new
                    {
                        message = "Password change required. You cannot access this resource until your password has been changed.",
                        mustChangePassword = true
                    };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    return;
                }
            }
        }

        await _next(context);
    }
}
