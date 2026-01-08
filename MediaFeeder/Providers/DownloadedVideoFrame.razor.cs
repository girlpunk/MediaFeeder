using System.Text.Json;
using Blazored.Video;
using Blazored.Video.Support;

namespace MediaFeeder.Providers;

public partial class DownloadedVideoFrame
{
    private void CanPlay(VideoState obj)
    {
        Console.WriteLine($"CanPlay {JsonSerializer.Serialize(obj)}");

        if (obj != null)
            Player?.Play(obj);
    }

    private void Ended(VideoState obj)
    {
        Console.WriteLine($"Ended {JsonSerializer.Serialize(obj)}");

        if (Page != null)
            Task.Run(async () => await Page.GoNext(true));
    }

    private void CanPlayThrough(VideoState obj)
    {
        Console.WriteLine($"CanPlayThrough {JsonSerializer.Serialize(obj)}");

        if (obj != null)
            Player?.Play(obj);
    }

    public BlazoredVideo? Player { get; set; }

    private void Abort(VideoState obj)
    {
        Console.WriteLine($"Abort {JsonSerializer.Serialize(obj)}");
    }

    private void Error(VideoState obj)
    {
        Console.WriteLine($"Error {JsonSerializer.Serialize(obj)}");
    }

    private void LoadedData(VideoState obj)
    {
        Console.WriteLine($"LoadedData {JsonSerializer.Serialize(obj)}");
    }
}