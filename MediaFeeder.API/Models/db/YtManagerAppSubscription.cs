using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.API.Models.db
{
    public class YtManagerAppSubscription
    {
        public YtManagerAppSubscription()
        {
            YtManagerAppVideos = new HashSet<YtManagerAppVideo>();
        }

        public int Id { get; set; }
        [MaxLength(1024)]
        public string Name { get; set; }

        [MaxLength(128)]
        public string PlaylistId { get; set; }
        public string Description { get; set; }
        [MaxLength(1024)]
        public string Thumbnail { get; set; }
        public bool? AutoDownload { get; set; }
        public int? DownloadLimit { get; set; }
        [MaxLength(128)]
        public string? DownloadOrder { get; set; }
        public bool? AutomaticallyDeleteWatched { get; set; }
        public int? ParentFolderId { get; set; }
        public int UserId { get; set; }
        [MaxLength(128)]
        public string ChannelId { get; set; }
        [MaxLength(1024)]
        public string ChannelName { get; set; }
        public bool RewritePlaylistIndices { get; set; }
        public DateTime? LastSynchronised { get; set; }
        [MaxLength(64)]
        public string Provider { get; set; }
        [MaxLength(100)]
        public string Thumb { get; set; }

        public virtual YtManagerAppSubscriptionFolder ParentFolder { get; set; }
        public virtual AuthUser User { get; set; }
        public virtual ICollection<YtManagerAppVideo> YtManagerAppVideos { get; init; }
    }
}
