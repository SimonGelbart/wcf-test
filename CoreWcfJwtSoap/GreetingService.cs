using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CoreWcfJwtSoap;

public sealed class GreetingService : IGreetingService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GreetingMessageStore _store;

    public GreetingService(IHttpContextAccessor httpContextAccessor, GreetingMessageStore store)
    {
        _httpContextAccessor = httpContextAccessor;
        _store = store;
    }

    [Authorize(Policy = GreetingPolicies.Read)]
    public string Greet(string name) => FormatGreeting(name);

    [Authorize]
    public string GreetAuthenticated(string name) => FormatGreeting(name);

    [Authorize(Policy = GreetingPolicies.Write)]
    public string UpdateGreeting(string salutation) => _store.Update(salutation);

    private string FormatGreeting(string name)
    {
        var caller = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub") ?? "authenticated caller";
        return $"{_store.Salutation}, {name}! (from {caller})";
    }
}
