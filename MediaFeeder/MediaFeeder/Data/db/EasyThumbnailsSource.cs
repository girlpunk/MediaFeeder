using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class EasyThumbnailsSource
{
    public int Id { get; set; }

    [MaxLength(40)] public required string StorageHash { get; set; }

    [MaxLength(255)] public required string Name { get; set; }

    public DateTime Modified { get; set; }

    public virtual ICollection<EasyThumbnailsThumbnail> EasyThumbnailsThumbnails { get; init; } = [];
}