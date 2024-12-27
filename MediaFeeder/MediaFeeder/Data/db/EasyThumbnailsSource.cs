using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class EasyThumbnailsSource
{
    public EasyThumbnailsSource()
    {
        EasyThumbnailsThumbnails = new HashSet<EasyThumbnailsThumbnail>();
    }

    public int Id { get; set; }

    [MaxLength(40)] public string StorageHash { get; set; }

    [MaxLength(255)] public string Name { get; set; }

    public DateTime Modified { get; set; }

    public virtual ICollection<EasyThumbnailsThumbnail> EasyThumbnailsThumbnails { get; init; }
}