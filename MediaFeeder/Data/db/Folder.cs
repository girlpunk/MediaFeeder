using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace MediaFeeder.Data.db;

public class Folder : ITreeSelectable
{
    public int Id { get; set; }

    [MaxLength(250)] public required string Name { get; set; }

    public int? ParentId { get; set; }
    public required int UserId { get; set; }

    public virtual Folder? Parent { get; set; }
    public virtual AuthUser? User { get; set; }
    public virtual ICollection<Folder> Subfolders { get; init; } = [];
    public virtual ICollection<Subscription> Subscriptions { get; init; } = [];

    public string OnSelectedNavigate => "folder/" + Id;

    public static Expression<Func<Folder, Folder>> GetProjection(int maxDepth, int currentDepth = 0)
    {
        return folder => new Folder
        {
            Id = folder.Id,
            Name = folder.Name,
            ParentId = folder.ParentId,
            UserId = folder.UserId,
            Parent = folder.Parent,
            User = folder.User,
            Subfolders = maxDepth == currentDepth
                ? new List<Folder>()
                : folder.Subfolders.AsQueryable().Select(GetProjection(maxDepth, currentDepth + 1)).ToList(),
            Subscriptions = folder.Subscriptions.AsQueryable().ToList(),
        };
    }
}