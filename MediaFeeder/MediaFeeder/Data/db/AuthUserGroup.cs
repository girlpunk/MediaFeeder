using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Data.db;

public class AuthUserGroup : IdentityUserRole<int>
{
    public int Id { get; set; }
    public override int UserId { get; set; }
    public int GroupId { get; set; }

    public virtual AuthGroup Group { get; set; }
    public virtual AuthUser User { get; set; }
}