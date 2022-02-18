using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaFeeder.Models.db
{
    public class YtManagerAppSubscription
    {
        public YtManagerAppSubscription()
        {
            YtManagerAppVideos = new HashSet<YtManagerAppVideo>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string PlaylistId { get; set; }
        public string Description { get; set; }
        public string Thumbnail { get; set; }
        public bool? AutoDownload { get; set; }
        public int? DownloadLimit { get; set; }
        public string? DownloadOrder { get; set; }
        public bool? AutomaticallyDeleteWatched { get; set; }
        public int? ParentFolderId { get; set; }
        public int UserId { get; set; }
        public string ChannelId { get; set; }
        public string ChannelName { get; set; }
        public bool RewritePlaylistIndices { get; set; }
        public DateTime? LastSynchronised { get; set; }
        public string Provider { get; set; }
        public string Thumb { get; set; }

        public virtual YtManagerAppSubscriptionFolder ParentFolder { get; set; }
        public virtual AuthUser User { get; set; }
        public virtual ICollection<YtManagerAppVideo> YtManagerAppVideos { get; set; }
    }
}
