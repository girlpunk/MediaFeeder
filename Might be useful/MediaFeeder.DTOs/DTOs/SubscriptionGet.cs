namespace MediaFeeder.DTOs.DTOs;

public class SubscriptionGet
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Thumbnail { get; set; }
    public string Thumb { get; set; }

    public int Unwatched { get; set; }
}