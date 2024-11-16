using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db
{
    public class YtManagerAppVideo
    {
        public int Id { get; set; }
        [MaxLength(12)]
        public string VideoId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Watched { get; set; }
        public string? DownloadedPath { get; set; }
        public int PlaylistIndex { get; set; }
        public DateTime PublishDate { get; set; }
        public string Thumbnail { get; set; }
        public int SubscriptionId { get; set; }
        public double Rating { get; set; }
        [MaxLength(255)]
        public string UploaderName { get; set; }
        public int Views { get; set; }
        public bool New { get; set; }
        public int Duration { get; set; }
        [MaxLength(100)]
        public string Thumb { get; set; }

        public TimeSpan DurationSpan => TimeSpan.FromSeconds(Duration);

        public virtual YtManagerAppSubscription Subscription { get; set; }
    }
}
