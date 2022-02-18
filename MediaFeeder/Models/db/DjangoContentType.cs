using System.Collections.Generic;

namespace MediaFeeder.Models.db
{
    public class DjangoContentType
    {
        public DjangoContentType()
        {
            AuthPermissions = new HashSet<AuthPermission>();
            DjangoAdminLogs = new HashSet<DjangoAdminLog>();
        }

        public int Id { get; set; }
        public string AppLabel { get; set; }
        public string Model { get; set; }

        public virtual ICollection<AuthPermission> AuthPermissions { get; set; }
        public virtual ICollection<DjangoAdminLog> DjangoAdminLogs { get; set; }
    }
}
