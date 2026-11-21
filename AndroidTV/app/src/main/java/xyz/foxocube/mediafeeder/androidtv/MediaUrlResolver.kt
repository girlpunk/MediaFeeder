package xyz.foxocube.mediafeeder.androidtv

import xyz.foxocube.mediafeeder.androidtv.data.entities.Video

/**
 * How the player should fetch a video's media; mirrors the server's /play 3-branch rule
 * (no download -> 412, remote file -> play the URL directly, local file -> stream via /play).
 */
sealed interface PlaybackUrl {
    /** `Video.downloadedPath` is null or blank: the server would answer 412 "Not Downloaded". */
    data class NotDownloaded(val provider: String, val providerId: String) : PlaybackUrl

    /** `Video.downloadedPath` is a remote http(s) URL; the server redirects there, play it directly. */
    data class Remote(val url: String) : PlaybackUrl

    /** `Video.downloadedPath` is a local file; stream it through the /play endpoint. */
    data class Streamed(val url: String) : PlaybackUrl
}

/**
 * Resolves a [Video] into a [PlaybackUrl] for the given server base URL:
 * null/blank downloaded path -> [PlaybackUrl.NotDownloaded] (the server's 412 state),
 * http(s) path -> [PlaybackUrl.Remote] unchanged, local path -> [PlaybackUrl.Streamed]
 * through `${baseUrl}/api/Video/{id}/play`.
 */
object MediaUrlResolver {
    fun resolve(baseUrl: String, video: Video): PlaybackUrl {
        val path: String? = video.downloadedPath
        if (path == null || path.isBlank()) {
            //TODO: Add provider
            return PlaybackUrl.NotDownloaded("", video.videoId)
        }
        if (path.startsWith("http://") || path.startsWith("https://")) {
            return PlaybackUrl.Remote(path)
        }
        val base: String = baseUrl.trimEnd { it == '/' }
        return PlaybackUrl.Streamed("$base/api/Video/${video.id}/play")
    }
}
