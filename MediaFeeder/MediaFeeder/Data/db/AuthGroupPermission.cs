using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Data.db;

public class AuthGroupPermission : IdentityRoleClaim<int>
{
    public override int Id { get; set; }
    public int GroupId { get; set; }
    public int PermissionId { get; set; }

    public virtual AuthGroup Group { get; set; }
    public virtual AuthPermission Permission { get; set; }
}