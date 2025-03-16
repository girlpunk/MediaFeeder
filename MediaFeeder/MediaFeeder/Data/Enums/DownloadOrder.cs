namespace MediaFeeder.Data.Enums;

public enum DownloadOrder
{
    Newest,
    // ('newest', 'Newest'),

    // ('oldest', 'Oldest')
        Oldest,

    // ('playlist', 'Playlist order'),
    PlaylistOrder,

    // ('playlist_reverse', 'Reverse playlist order'),
    ReversePlaylistOrder,

    // ('popularity', 'Popularity'),
    Popularity,

    // ('rating', 'Top rated'),
    TopRated,

}