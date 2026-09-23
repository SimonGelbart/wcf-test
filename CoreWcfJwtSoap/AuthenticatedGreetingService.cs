using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CoreWcfJwtSoap;

public sealed class AuthenticatedGreetingService : IAuthenticatedGreetingService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticatedGreetingService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    [Authorize]
    public string GreetAuthenticated(string name)
    {
        var caller = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub") ?? "authenticated caller";
        return $"Hello, {name}! (from {caller})";
    }
}
