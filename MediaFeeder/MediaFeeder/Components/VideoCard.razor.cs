﻿using AntDesign;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components;

public sealed partial class VideoCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public Video? Video { get; set; }

    [Inject] public required IDbContextFactory<MediaFeederDataContext> ContextFactory { get; set; }

    [Inject] public required IMessageService MessageService { get; set; }

    private string? Badge
    {
        get
        {
            if (Video == null)
                return null;

            if (Video.New)
                return "New";

            if (Video.DownloadedPath != null)
                return "Downloaded";

            if (Video.Watched)
                return "Watched";

            return null;
        }
    }

    private string? BadgeColour
    {
        get
        {
            if (Video == null)
                return "#000000";

            if (Video.New)
                return "#007bff";

            if (Video.DownloadedPath != null)
                return "#59b352";

            if (Video.Watched)
                return "#444";

            return "#000000";
        }
    }

    private async Task Watch()
    {
        ArgumentNullException.ThrowIfNull(Video);

        await using var context = await ContextFactory.CreateDbContextAsync();
        var video = await context.Videos.FindAsync(Video.Id);

        if (video != null)
            video.Watched = !video.Watched;
        await context.SaveChangesAsync();
    }

    private async Task Delete()
    {
        await MessageService.Error("This feature is not implemented");
    }
}