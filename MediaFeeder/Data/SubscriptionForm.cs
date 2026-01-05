using MediaFeeder.Data.Enums;

namespace MediaFeeder.Data;

public sealed class SubscriptionForm
{
    public string Name { get; set; } = "";
    public int ParentFolderId { get; set; }
    public string PlaylistId { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public bool AutoDownload { get; set; }
    public int? DownloadLimit { get; set; }
    public DownloadOrder? DownloadOrder { get; set; }
    public bool AutomaticallyDeleteWatched { get; set; }
    public bool RewritePlaylistIndices { get; set; }
    public string? Provider { get; set; }
    public bool DisableSync { get; set; }
}