package xyz.foxocube.mediafeeder.androidtv

import kotlinx.serialization.Serializable

/**
 * One MediaFeeder subscription (GET /api/Subscriptions, camelCase JSON, enums as numbers).
 */
@Serializable
data class Subscription(
    val id: Int,
    val name: String,
    val playlistId: String,
    /** Provider enum as a number (Youtube / RSS / CCC / Sonarr). */
    val provider: Int,
    val thumb: String? = null,
    /** WatchOrder enum as a number (0 = oldest first, 1 = newest first, 2 = playlist order). */
    val watchOrder: Int,
)

/**
 * One MediaFeeder video (GET /api/Subscriptions/{id}/videos, camelCase JSON).
 * `duration` and `playbackPosition` are integer seconds.
 */
@Serializable
data class Video(
    val id: Int,
    val videoId: String,
    val name: String,
    /** ISO-8601 publish date exactly as returned by the server. */
    val publishedDate: String? = null,
    /** Integer seconds (null = unknown). */
    val duration: Int? = null,
    /** Integer seconds (null = not watched / unknown). */
    val playbackPosition: Int? = null,
    val thumb: String? = null,
    val isDownloaded: Boolean,
    /** null | local file path | remote http(s) URL. */
    val downloadedPath: String? = null,
)
