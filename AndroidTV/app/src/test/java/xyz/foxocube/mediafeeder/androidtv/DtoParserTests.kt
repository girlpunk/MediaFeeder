package xyz.foxocube.mediafeeder.androidtv

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

private const val SUBSCRIPTION_JSON: String =
    """
    {
      "id": 42,
      "name": "Numb (Chin)atoes",
      "playlistId": "PL52a7f0b3",
      "provider": "youtube",
      "thumb": "thumbnails/42.png",
      "watchOrder": 1
    }
    """

private const val SUBSCRIPTION_JSON_NO_THUMB: String =
    """
    {
      "id": 1,
      "name": "No Thumb",
      "playlistId": "PL00000001",
      "provider": "RSS",
      "watchOrder": 0
    }
    """

private const val SUBSCRIPTION_FULL_PAYLOAD_JSON: String =
    """
    {
      "id": 9,
      "name": "Extra Fields",
      "playlistId": "PLx1y2z3",
      "provider": "youtube",
      "thumb": null,
      "watchOrder": 2,
      "description": "A long description with details",
      "autoDownload": true,
      "downloadLimit": 5,
      "downloadOrder": 1,
      "automaticallyDeleteWatched": false,
      "parentFolderId": 3,
      "userId": 1,
      "channelId": "UC1234567890",
      "channelName": "Channel One",
      "rewritePlaylistIndices": false,
      "lastSynchronised": "2026-09-10T10:00:00Z",
      "disableSync": false
    }
    """

private const val VIDEO_DOWNLOADED_JSON: String =
    """
    {
      "id": 1337,
      "videoId": "dQw4w9WgXcQ",
      "name": "Never Gonna Give You Up",
      "publishDate": "2026-09-01T12:34:56.789Z",
      "duration": 212,
      "playbackPosition": 100,
      "thumb": "videos/1337.jpg",
      "isDownloaded": true,
      "downloadedPath": "downloads/Numb (Chin)atoes/Never Gonna Give You Up [dQw4w9WgXcQ].mp4"
    }
    """

private const val VIDEO_NOT_DOWNLOADED_JSON: String =
    """
    {
      "id": 7,
      "videoId": "abc-123",
      "name": "Some Fresh Upload",
      "publishDate": "2026-09-09T08:00:00Z",
      "isDownloaded": false
    }
    """

private const val VIDEO_FULL_PAYLOAD_JSON: String =
    """
    {
      "id": 200,
      "videoId": "vid-200",
      "name": "Full Payload Video",
      "publishDate": "2026-08-30T18:22:10.5Z",
      "duration": 720,
      "playbackPosition": 0,
      "thumb": "videos/200.jpg",
      "isDownloaded": true,
      "downloadedPath": "downloads/Sub/Full Payload [vid-200].mp4",
      "description": "desc",
      "tags": [],
      "watched": false,
      "watchedDate": null,
      "playlistIndex": 4,
      "star": false,
      "starDate": null,
      "thumbnail": null,
      "subscriptionId": 9,
      "rating": null,
      "uploaderName": "Uploader",
      "views": 1234,
      "new": true,
      "downloadError": null
    }
    """

private const val SUBSCRIPTIONS_ARRAY_JSON: String =
    """
    [
      {"id": 1, "name": "First", "playlistId": "PL1", "provider": "youtube", "thumb": null, "watchOrder": 0},
      {"id": 2, "name": "Second", "playlistId": "PL2", "provider": "RSS", "thumb": "t2.png", "watchOrder": 1}
    ]
    """

private const val VIDEOS_ARRAY_JSON: String =
    """
    [
      {
        "id": 10, "videoId": "v10", "name": "Video Ten", "publishDate": null, "duration": 10,
        "playbackPosition": null, "thumb": null, "isDownloaded": false, "downloadedPath": null
      },
      {
        "id": 11, "videoId": "v11", "name": "Video Eleven", "publishDate": "2026-09-02T00:00:00Z",
        "duration": 11, "playbackPosition": 3, "thumb": "t11.jpg", "isDownloaded": true,
        "downloadedPath": "http://remote.example.com/video.mp4"
      }
    ]
    """

/**
 * Contract for parsing real MediaFeeder REST JSON (camelCase, watchOrder as a number,
 * provider as a string, duration as integer seconds) into the Kotlin models.
 */
class DtoParserTests {
    @Test
    fun `subscription json parses into all subscription fields`() {
        val subscription: Subscription = DtoParser.parseSubscription(SUBSCRIPTION_JSON)

        assertEquals(42, subscription.id)
        assertEquals("Numb (Chin)atoes", subscription.name)
        assertEquals("PL52a7f0b3", subscription.playlistId)
        assertEquals("youtube", subscription.provider)
        assertEquals("thumbnails/42.png", subscription.thumb)
        assertEquals(1, subscription.watchOrder)
    }

    @Test
    fun `subscription json without thumb parses a null thumb`() {
        val subscription: Subscription = DtoParser.parseSubscription(SUBSCRIPTION_JSON_NO_THUMB)

        assertEquals(1, subscription.id)
        assertEquals("No Thumb", subscription.name)
        assertEquals("PL00000001", subscription.playlistId)
        assertEquals("RSS", subscription.provider)
        assertNull(subscription.thumb)
        assertEquals(0, subscription.watchOrder)
    }

    @Test
    fun `subscription parses from a full server payload with extra fields`() {
        val subscription: Subscription = DtoParser.parseSubscription(SUBSCRIPTION_FULL_PAYLOAD_JSON)

        assertEquals(9, subscription.id)
        assertEquals("Extra Fields", subscription.name)
        assertEquals("PLx1y2z3", subscription.playlistId)
        assertEquals("youtube", subscription.provider)
        assertNull(subscription.thumb)
        assertEquals(2, subscription.watchOrder)
    }

    @Test
    fun `downloaded video json parses into all video fields`() {
        val video: Video = DtoParser.parseVideo(VIDEO_DOWNLOADED_JSON)

        assertEquals(1337, video.id)
        assertEquals("dQw4w9WgXcQ", video.videoId)
        assertEquals("Never Gonna Give You Up", video.name)
        assertEquals("2026-09-01T12:34:56.789Z", video.publishDate)
        assertEquals(212, video.duration)
        assertEquals(100, video.playbackPosition)
        assertEquals("videos/1337.jpg", video.thumb)
        assertEquals(true, video.isDownloaded)
        assertEquals("downloads/Numb (Chin)atoes/Never Gonna Give You Up [dQw4w9WgXcQ].mp4", video.downloadedPath)
    }

    @Test
    fun `not downloaded video json parses with null optional fields`() {
        val video: Video = DtoParser.parseVideo(VIDEO_NOT_DOWNLOADED_JSON)

        assertEquals(7, video.id)
        assertEquals("abc-123", video.videoId)
        assertEquals("Some Fresh Upload", video.name)
        assertEquals("2026-09-09T08:00:00Z", video.publishDate)
        assertNull(video.duration)
        assertNull(video.playbackPosition)
        assertNull(video.thumb)
        assertEquals(false, video.isDownloaded)
        assertNull(video.downloadedPath)
    }

    @Test
    fun `video parses from a full server payload with extra fields`() {
        val video: Video = DtoParser.parseVideo(VIDEO_FULL_PAYLOAD_JSON)

        assertEquals(200, video.id)
        assertEquals("vid-200", video.videoId)
        assertEquals("Full Payload Video", video.name)
        assertEquals("2026-08-30T18:22:10.5Z", video.publishDate)
        assertEquals(720, video.duration)
        assertEquals(0, video.playbackPosition)
        assertEquals("videos/200.jpg", video.thumb)
        assertEquals(true, video.isDownloaded)
        assertEquals("downloads/Sub/Full Payload [vid-200].mp4", video.downloadedPath)
    }

    @Test
    fun `subscriptions array json parses into a list of subscriptions`() {
        val subscriptions: List<Subscription> = DtoParser.parseSubscriptions(SUBSCRIPTIONS_ARRAY_JSON)

        assertEquals(2, subscriptions.size)
        assertEquals(1, subscriptions[0].id)
        assertEquals("First", subscriptions[0].name)
        assertEquals("youtube", subscriptions[0].provider)
        assertEquals(2, subscriptions[1].id)
        assertEquals("Second", subscriptions[1].name)
        assertEquals("RSS", subscriptions[1].provider)
        assertEquals("t2.png", subscriptions[1].thumb)
        assertEquals(1, subscriptions[1].watchOrder)
    }

    @Test
    fun `videos array json parses into a list of videos`() {
        val videos: List<Video> = DtoParser.parseVideos(VIDEOS_ARRAY_JSON)

        assertEquals(2, videos.size)
        assertEquals(10, videos[0].id)
        assertEquals("v10", videos[0].videoId)
        assertEquals(false, videos[0].isDownloaded)
        assertNull(videos[0].downloadedPath)
        assertEquals(11, videos[1].id)
        assertEquals("v11", videos[1].videoId)
        assertEquals(true, videos[1].isDownloaded)
        assertEquals("http://remote.example.com/video.mp4", videos[1].downloadedPath)
        assertEquals(11, videos[1].duration)
        assertEquals(3, videos[1].playbackPosition)
    }
}
