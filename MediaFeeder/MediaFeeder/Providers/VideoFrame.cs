using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;

namespace MediaFeeder.Providers;

public abstract class VideoFrame : ComponentBase
{
    [Parameter] public Video? Video { get; set; }
}
