using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DjangoAdminLog
{
    public int Id { get; set; }
    public DateTime ActionTime { get; set; }
    [MaxLength(1000000000)] public required string ObjectId { get; set; }

    [MaxLength(200)] public required string ObjectRepr { get; set; }

    public short ActionFlag { get; set; }
    [MaxLength(1000000000)] public required string ChangeMessage { get; set; }
    public int? ContentTypeId { get; set; }
    public int UserId { get; set; }

    public virtual DjangoContentType? ContentType { get; set; }
    public virtual AuthUser? User { get; set; }
}