using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediaFeeder.Data.db;

public class Video
{
    public int Id { get; set; }

    [MaxLength(128)] public required string VideoId { get; set; }

    [MaxLength(1000000000)] public required string Name { get; set; }
    [MaxLength(1000000000)] public required string Description { get; set; }
    public bool Watched { get; set; }
    [MaxLength(1000000000)] public string? DownloadedPath { get; set; }
    public int? PlaylistIndex { get; set; }
    public DateTimeOffset? PublishDate { get; set; }

    [Obsolete]
    [MaxLength(1000000000)]
    public string? Thumbnail { get; set; }

    public required int SubscriptionId { get; set; }
    public double? Rating { get; set; }

    [MaxLength(255)] public required string UploaderName { get; set; }

    public int? Views { get; set; }
    public bool New { get; set; }
    public int? Duration { get; set; }

    public string? DownloadError { get; set; }

    [MaxLength(100)] public string? Thumb { get; set; }

    [NotMapped]
    public TimeSpan? DurationSpan
    {
        get => Duration != null ? TimeSpan.FromSeconds(Duration.Value) : null;
        set => Duration = value != null ? (int)value.Value.TotalSeconds : null;
    }

    public virtual Subscription? Subscription { get; set; }
    public virtual bool IsDownloaded { get; }
}
