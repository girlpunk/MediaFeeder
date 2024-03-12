using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.API.Models.db
{
    public class DjangoContentType
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string AppLabel { get; set; }

        [MaxLength(100)]
        public string Model { get; set; }

        public virtual ICollection<AuthPermission> AuthPermissions { get; init; }
        public virtual ICollection<DjangoAdminLog> DjangoAdminLogs { get; init; }
    }
}
