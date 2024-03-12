using System;

namespace MediaFeeder.Models.db
{
    public class EasyThumbnailsThumbnail
    {
        public int Id { get; set; }
        public string StorageHash { get; set; }
        public string Name { get; set; }
        public DateTime Modified { get; set; }
        public int SourceId { get; set; }

        public virtual EasyThumbnailsSource Source { get; set; }
        public virtual EasyThumbnailsThumbnaildimension EasyThumbnailsThumbnaildimension { get; set; }
    }
}
