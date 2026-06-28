using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Helpers;

public class UserRoleClaimsTransformation : IClaimsTransformation
{
    private readonly UserRepository _userRepository;

    public UserRoleClaimsTransformation(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return principal;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return principal;
        }

        var currentRoleClaims = identity.FindAll(ClaimTypes.Role).ToList();
        foreach (var roleClaim in currentRoleClaims)
        {
            identity.RemoveClaim(roleClaim);
        }

        foreach (var role in user.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return principal;
    }
}
