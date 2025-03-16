using System.ComponentModel.DataAnnotations;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Data.db;

public class Subscription : ITreeSelectable
{
    public int Id { get; set; }

    [MaxLength(1024)] public required string Name { get; set; }

    [MaxLength(128)] public required string PlaylistId { get; set; }

    [MaxLength(1000000000)] public required string Description { get; set; }

    [MaxLength(1024)] public string? Thumbnail { get; set; }

    public bool AutoDownload { get; set; } = false;
    public int? DownloadLimit { get; set; }

    public DownloadOrder? DownloadOrder { get; set; }

    public bool AutomaticallyDeleteWatched { get; set; } = false;
    public required int ParentFolderId { get; set; }
    public required int UserId { get; set; }

    [MaxLength(128)] public required string ChannelId { get; set; }

    [MaxLength(1024)] public required string ChannelName { get; set; }

    public bool RewritePlaylistIndices { get; set; }
    public DateTimeOffset? LastSynchronised { get; set; }

    [MaxLength(64)] public required string Provider { get; set; }

    [MaxLength(100)] public string? Thumb { get; set; }


    public virtual Folder? ParentFolder { get; set; }
    public virtual AuthUser? User { get; set; }
    public virtual ICollection<Video> Videos { get; init; } = [];

    public string OnSelectedNavigate => "subscription/" + Id;
}