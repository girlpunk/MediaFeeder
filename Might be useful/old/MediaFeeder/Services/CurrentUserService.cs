using System.Security.Claims;

namespace MediaFeeder.Services;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public bool IsInRole(params string[] roleName)
    {
        var group = _httpContextAccessor.HttpContext?.User?.Claims?.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToArray();
        return group.Any(x => roleName.Contains(x));

    }
    public IEnumerable<string> GetRoles() => _httpContextAccessor.HttpContext?.User?.Claims?.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToArray();


    public string DisplayName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.GivenName);
}