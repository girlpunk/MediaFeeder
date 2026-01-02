namespace MediaFeeder.Data.db;

public class VideoTag
{
    public int Id { get; set; }

    public int VideoId { get; set; }
    public virtual Video? Video { get; set; }

    public string Tag { get; set; }
}