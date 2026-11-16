namespace MediaFeeder.Data.DTOs;

using System.Security.Claims;
using System.Security.Principal;

[Serializable]
public sealed record UserResponse(
    IIdentity? Identity,
    IEnumerable<ClaimsIdentity> OtherIdentities,
    IEnumerable<Claim> Claims
);
