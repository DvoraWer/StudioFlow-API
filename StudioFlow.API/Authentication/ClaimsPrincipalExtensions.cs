using System.Security.Claims;
using StudioFlow.Core.Enums;

namespace StudioFlow.API.Authentication;

/// <summary>
/// Reads the authenticated caller's identity from JWT claims (spec §19, §20).
/// Controllers use these instead of trusting any id supplied in the request body
/// or route.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id. Throws if the principal is not authenticated.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");

        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The authenticated principal has no valid user id claim.");
    }

    /// <summary>The authenticated user's role.</summary>
    public static UserRole GetRole(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.Role);

        return Enum.TryParse<UserRole>(value, out var role)
            ? role
            : throw new InvalidOperationException("The authenticated principal has no valid role claim.");
    }
}
