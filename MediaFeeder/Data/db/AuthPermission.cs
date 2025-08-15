using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class AuthPermission
{
    public int Id { get; set; }

    [MaxLength(255)] public required string Name { get; set; }

    public int ContentTypeId { get; set; }

    [MaxLength(100)] public required string Codename { get; set; }

    public virtual DjangoContentType? ContentType { get; set; }
    public virtual ICollection<AuthGroupPermission> AuthGroupPermissions { get; } = new List<AuthGroupPermission>();

    public virtual ICollection<AuthUserUserPermission> AuthUserUserPermissions { get; } =
        new List<AuthUserUserPermission>();
}