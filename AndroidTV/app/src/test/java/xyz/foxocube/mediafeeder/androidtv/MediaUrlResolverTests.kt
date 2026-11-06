package xyz.foxocube.mediafeeder.androidtv

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

private const val BASE_URL: String = "http://192.168.1.10:8080"

/**
 * Contract for the client-side 3-branch playback rule (mirrors GET /api/Video/{id}/play):
 * not downloaded -> 412 marker, remote http(s) -> pass through, local path -> /play endpoint.
 */
class MediaUrlResolverTests {
    private fun video(downloadedPath: String?): Video =
        Video(
            id = 1337,
            videoId = "dQw4w9WgXcQ",
            name = "Never Gonna Give You Up",
            publishDate = "2026-09-01T12:34:56.789Z",
            duration = 212,
            playbackPosition = 100,
            thumb = "videos/1337.jpg",
            isDownloaded = downloadedPath != null,
            downloadedPath = downloadedPath,
        )

    @Test
    fun `null downloaded path resolves to the not downloaded marker`() {
        val result: PlaybackUrl = MediaUrlResolver.resolve(BASE_URL, video(null))

        assertNotDownloaded(result)
    }

    @Test
    fun `blank downloaded path resolves to the not downloaded marker`() {
        val result: PlaybackUrl = MediaUrlResolver.resolve(BASE_URL, video("   "))

        assertNotDownloaded(result)
    }

    @Test
    fun `https downloaded path passes through unchanged`() {
        val remote: String = "https://files.example.com/videos/remote.mp4"
        val result: PlaybackUrl = MediaUrlResolver.resolve(BASE_URL, video(remote))

        assertEquals(remote, remoteUrl(result))
    }

    @Test
    fun `http downloaded path passes through unchanged`() {
        val remote: String = "http://files.example.com/videos/remote.mp4"
        val result: PlaybackUrl = MediaUrlResolver.resolve(BASE_URL, video(remote))

        assertEquals(remote, remoteUrl(result))
    }

    @Test
    fun `local relative path resolves to the play endpoint url`() {
        val local: String = "downloads/Numb (Chin)atoes/Never Gonna Give You Up [dQw4w9WgXcQ].mp4"
        val result: PlaybackUrl = MediaUrlResolver.resolve(BASE_URL, video(local))

        assertEquals("$BASE_URL/api/Video/1337/play", streamedUrl(result))
    }

    @Test
    fun `local absolute path resolves to the play endpoint url`() {
        val local: String = "/mnt/media/downloads/Numb (Chin)atoes/Some Video [abc].mp4"
        val result: PlaybackUrl = MediaUrlResolver.resolve(BASE_URL, video(local))

        assertEquals("$BASE_URL/api/Video/1337/play", streamedUrl(result))
    }

    @Test
    fun `base url with trailing slash does not produce a double slash`() {
        val result: PlaybackUrl = MediaUrlResolver.resolve("$BASE_URL/", video("downloads/x/y.mp4"))

        assertEquals("$BASE_URL/api/Video/1337/play", streamedUrl(result))
    }

    private fun assertNotDownloaded(result: PlaybackUrl) {
        assertTrue(result is PlaybackUrl.NotDownloaded, "expected NotDownloaded (412), got $result")
    }

    private fun remoteUrl(result: PlaybackUrl): String {
        assertTrue(result is PlaybackUrl.Remote, "expected Remote, got $result")
        return (result as PlaybackUrl.Remote).url
    }

    private fun streamedUrl(result: PlaybackUrl): String {
        assertTrue(result is PlaybackUrl.Streamed, "expected Streamed, got $result")
        return (result as PlaybackUrl.Streamed).url
    }
}
