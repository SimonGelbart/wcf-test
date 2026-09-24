using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CoreWcfJwtSoap;

public sealed class GreetingService : IGreetingService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GreetingService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    [Authorize(Policy = "GreetingRead")]
    public string Greet(string name) => FormatGreeting(name);

    [Authorize]
    public string GreetAuthenticated(string name) => FormatGreeting(name);

    private string FormatGreeting(string name)
    {
        var caller = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub") ?? "authenticated caller";
        return $"Hello, {name}! (from {caller})";
    }
}
