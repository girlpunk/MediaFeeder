using MediaFeeder.Data.db;
using MediaFeeder.PlaybackManager;
using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Providers;

public abstract class VideoFrame : ComponentBase
{
    [Parameter] public Video? Video { get; set; }
    [Parameter] public MediaFeeder.Components.Pages.Video? Page { get; set; }
    [Parameter] public PlaybackSession? PlaybackSession { get; set; }
}
