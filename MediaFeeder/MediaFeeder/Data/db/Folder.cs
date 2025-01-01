using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class Folder : ITreeSelectable
{
    public int Id { get; set; }

    [MaxLength(250)] public required string Name { get; set; }

    public int? ParentId { get; set; }
    public int UserId { get; set; }

    public virtual Folder? Parent { get; set; }
    public virtual required AuthUser User { get; set; }
    public virtual required ICollection<Folder> Subfolders { get; init; }
    public virtual required ICollection<Subscription> Subscriptions { get; init; }

    public string OnSelectedNavigate => "/folder/" + Id;
}