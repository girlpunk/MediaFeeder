using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

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

    public static async Task<ICollection<int>> RecursiveFolderIds(MediaFeederDataContext context, int folderId, int userId, int maxDepth = 5, int currentDepth = 0)
    {
        var folder = await context.Folders
                .Where(f => f.Id == folderId && f.UserId == userId)
                .Select(GetProjection(5))
                .SingleAsync();
        return folder.RecursiveFolderIds(5).ToList();
    }

    private ICollection<int> RecursiveFolderIds(int maxDepth, int currentDepth = 0)
    {
        var ret = new HashSet<int> { Id };
        foreach (var f in Subfolders)
        {
            ret.Add(f.Id);
            if (currentDepth < maxDepth)
                ret.UnionWith(f.RecursiveFolderIds(maxDepth, currentDepth + 1));
        }
        return ret;
    }

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