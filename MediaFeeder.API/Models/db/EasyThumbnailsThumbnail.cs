using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.API.Models.db
{
    public class EasyThumbnailsThumbnail
    {
        public int Id { get; set; }
        [MaxLength(40)]
        public string StorageHash { get; set; }
        [MaxLength(255)]
        public string Name { get; set; }
        public DateTime Modified { get; set; }
        public int SourceId { get; set; }

        public virtual EasyThumbnailsSource Source { get; set; }
        public virtual EasyThumbnailsThumbnaildimension EasyThumbnailsThumbnaildimension { get; set; }
    }
}
