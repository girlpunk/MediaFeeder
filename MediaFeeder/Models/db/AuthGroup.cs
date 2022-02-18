using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Models.db
{
    public class AuthGroup : IdentityRole<int>
    {
        public AuthGroup()
        {
            AuthGroupPermissions = new HashSet<AuthGroupPermission>();
            AuthUserGroups = new HashSet<AuthUserGroup>();
        }

        public override int Id { get; set; }
        public override string Name { get; set; }

        public virtual ICollection<AuthGroupPermission> AuthGroupPermissions { get; set; }
        public virtual ICollection<AuthUserGroup> AuthUserGroups { get; set; }
    }
}
