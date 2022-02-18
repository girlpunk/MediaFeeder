using System.Collections.Generic;

namespace MediaFeeder.Models.db
{
    public class AuthPermission
    {
        public AuthPermission()
        {
            AuthGroupPermissions = new HashSet<AuthGroupPermission>();
            AuthUserUserPermissions = new HashSet<AuthUserUserPermission>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public int ContentTypeId { get; set; }
        public string Codename { get; set; }

        public virtual DjangoContentType ContentType { get; set; }
        public virtual ICollection<AuthGroupPermission> AuthGroupPermissions { get; set; }
        public virtual ICollection<AuthUserUserPermission> AuthUserUserPermissions { get; set; }
    }
}
