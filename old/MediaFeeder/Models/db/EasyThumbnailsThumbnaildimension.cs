namespace MediaFeeder.Models.db
{
    public class EasyThumbnailsThumbnaildimension
    {
        public int Id { get; set; }
        public int ThumbnailId { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }

        public virtual EasyThumbnailsThumbnail Thumbnail { get; set; }
    }
}
