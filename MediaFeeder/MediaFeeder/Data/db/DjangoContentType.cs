using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DjangoContentType
{
    public int Id { get; set; }

    [MaxLength(100)] public required string AppLabel { get; set; }

    [MaxLength(100)] public required string Model { get; set; }

    public virtual ICollection<AuthPermission> AuthPermissions { get; init; } = [];
    public virtual ICollection<DjangoAdminLog> DjangoAdminLogs { get; init; } = [];
}