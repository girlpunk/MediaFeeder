using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Data.db;

public class AuthGroup : IdentityRole<int>
{
    public override int Id { get; set; }

    [MaxLength(150)] public override string Name { get; set; }

    public virtual ICollection<AuthGroupPermission> AuthGroupPermissions { get; } = new List<AuthGroupPermission>();
    public virtual ICollection<AuthUserGroup> AuthUserGroups { get; } = new List<AuthUserGroup>();
}