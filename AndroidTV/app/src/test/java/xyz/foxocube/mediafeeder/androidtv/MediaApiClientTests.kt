package xyz.foxocube.mediafeeder.androidtv

import java.util.concurrent.ConcurrentLinkedDeque
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Contract for [MediaApiClient]: bearer authentication (with one refresh attempt),
 * DtoParser-based decoding, 401/403 -> [MediaApiClient.ApiOutcome.AuthRequired], and
 * thumbnail URL derivation. Pure JVM with a scripted OkHttp interceptor.
 */
class MediaApiClientTests {
    private val store: AuthStore = AuthStore(FakeSharedPreferences())
    private val atEpochMillis: Long = 1_750_000_000_000L
    private val http: ScriptedHttp = ScriptedHttp()
    private val client: MediaApiClient =
        MediaApiClient(
            store,
            TokenManager(
                { AuthClient(OIDC_URL, DEVICE_CLIENT_ID, SERVER_CLIENT_ID, METADATA) },
                store,
                OkHttpClient().newBuilder().addInterceptor(http).build(),
                { atEpochMillis },
            ),
            OkHttpClient().newBuilder().addInterceptor(http).build(),
        )

    @Test
    fun `getSubscriptions sends the bearer and parses subscriptions`() {
        store.baseUrl = BASE_URL
        store.serverToken = SERVER_TOKEN
        store.expiresAtEpochMillis = atEpochMillis + 10_000L
        http.enqueue(200, SUBSCRIPTIONS_JSON)

        val outcome: MediaApiClient.ApiOutcome<List<Subscription>> = runBlocking { client.getSubscriptions() }

        val success = outcome as MediaApiClient.ApiOutcome.Success<List<Subscription>>
        assertEquals(2, success.value.size)
        assertEquals("Channel A", success.value.first().name)
        assertEquals(1, success.value.first().id)
        assertEquals(
            "Bearer $SERVER_TOKEN",
            http.requests.first().header("Authorization"),
            "API requests carry the server bearer",
        )
        assertEquals("$BASE_URL/api/Subscriptions", http.requests.first().url.toString())
    }

    @Test
    fun `getSubscriptions reports AuthRequired on 401`() {
        store.baseUrl = BASE_URL
        store.serverToken = SERVER_TOKEN
        store.expiresAtEpochMillis = atEpochMillis + 10_000L
        http.enqueue(401, "{}")

        val outcome: MediaApiClient.ApiOutcome<List<Subscription>> = runBlocking { client.getSubscriptions() }

        assertTrue(outcome is MediaApiClient.ApiOutcome.AuthRequired, "expected AuthRequired, got $outcome")
    }

    @Test
    fun `getSubscriptions refreshes an expired token before the request`() {
        store.baseUrl = BASE_URL
        store.accessToken = DEVICE_ACCESS_TOKEN
        store.refreshToken = DEVICE_REFRESH_TOKEN
        store.serverToken = STALE_TOKEN
        store.expiresAtEpochMillis = atEpochMillis - 1_000L
        http.enqueue(400, """{"error": "invalid_client_assertion"}""")
        http.enqueue(200, REFRESH_JSON)
        http.enqueue(200, EXCHANGE_JSON)
        http.enqueue(200, SUBSCRIPTIONS_JSON)

        val outcome: MediaApiClient.ApiOutcome<List<Subscription>> = runBlocking { client.getSubscriptions() }

        val success = outcome as MediaApiClient.ApiOutcome.Success<List<Subscription>>
        assertEquals(2, success.value.size)
        assertEquals(4, http.requests.size, "failed exchange + device refresh + re-exchange + API call")
        assertEquals("Bearer $SERVER_TOKEN", http.requests.last().header("Authorization"))
    }

    @Test
    fun `getSubscriptions reports AuthRequired when no token can be obtained`() {
        store.baseUrl = BASE_URL

        val outcome: MediaApiClient.ApiOutcome<List<Subscription>> = runBlocking { client.getSubscriptions() }

        assertTrue(outcome is MediaApiClient.ApiOutcome.AuthRequired, "expected AuthRequired, got $outcome")
        assertTrue(http.requests.isEmpty(), "no HTTP calls expected, got ${http.requests}")
    }

    @Test
    fun `getVideos parses videos for the subscription`() {
        store.baseUrl = BASE_URL
        store.serverToken = SERVER_TOKEN
        store.expiresAtEpochMillis = atEpochMillis + 10_000L
        http.enqueue(200, VIDEOS_JSON)

        val outcome: MediaApiClient.ApiOutcome<List<Video>> = runBlocking { client.getVideos(7) }

        val success = outcome as MediaApiClient.ApiOutcome.Success<List<Video>>
        assertEquals(2, success.value.size)
        assertEquals("First", success.value.first().name)
        assertEquals("$BASE_URL/api/Subscriptions/7/videos", http.requests.first().url.toString())
    }

    @Test
    fun `thumbnail urls derive the endpoint or null without thumb`() {
        store.baseUrl = BASE_URL
        val withThumb: Subscription = DtoParser.parseSubscription(
            """{"id": 3, "name": "A", "playlistId": "p", "provider": "youtube", "watchOrder": 0, "thumb": "s/3.jpg"}""",
        )
        val withoutThumb: Subscription = DtoParser.parseSubscription(
            """{"id": 4, "name": "B", "playlistId": "p", "provider": "youtube", "watchOrder": 0}""",
        )
        val video: Video = DtoParser.parseVideo(
            """{"id": 9, "videoId": "v9", "name": "V", "isDownloaded": false}""",
        )

        assertEquals("$BASE_URL/api/Subscriptions/3/thumbnail", client.subscriptionThumbnailUrl(withThumb))
        assertNull(client.subscriptionThumbnailUrl(withoutThumb))
        assertNull(client.videoThumbnailUrl(video))
    }

    /** One scripted response per call (no network), recording every request. */
    private class ScriptedHttp : Interceptor {
        private val responses: ConcurrentLinkedDeque<Pair<Int, String>> = ConcurrentLinkedDeque()
        val requests: ConcurrentLinkedDeque<Request> = ConcurrentLinkedDeque()

        fun enqueue(code: Int, body: String) {
            responses.add(code to body)
        }

        override fun intercept(chain: Interceptor.Chain): Response {
            val request: Request = chain.request()
            requests.add(request)
            val (code, body) = responses.removeFirst()
            return Response.Builder()
                .request(request)
                .protocol(Protocol.HTTP_1_1)
                .code(code)
                .message(if (code in 200..299) "OK" else "ERROR")
                .body(body.toResponseBody("application/json".toMediaType()))
                .build()
        }
    }

    private companion object {
        const val BASE_URL: String = "http://192.168.1.10:8080"
        const val OIDC_URL: String = "https://auth.example.com"
        const val DEVICE_CLIENT_ID: String = "device-client-1"
        const val SERVER_CLIENT_ID: String = "server-client-2"
        const val TOKEN_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/token/"
        const val DEVICE_AUTH_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/device/"
        const val SERVER_TOKEN: String = "server-token-1"
        const val STALE_TOKEN: String = "stale-server-token"
        const val DEVICE_ACCESS_TOKEN: String = "device-access-1"
        const val DEVICE_REFRESH_TOKEN: String = "device-refresh-1"

        const val REFRESH_JSON: String =
            """{"access_token": "device-access-2", "refresh_token": "device-refresh-2", "expires_in": 86400}"""

        const val EXCHANGE_JSON: String =
            """{"access_token": "server-token-1", "refresh_token": "server-refresh-1", "expires_in": 3600}"""

        private val METADATA: OidcMetadata = OidcMetadata(TOKEN_ENDPOINT, DEVICE_AUTH_ENDPOINT)

        val SUBSCRIPTIONS_JSON: String =
            """
            [
               {"id": 1, "name": "Channel A", "playlistId": "pl-1", "provider": "youtube", "watchOrder": 0, "thumb": "s/1.jpg"},
               {"id": 2, "name": "Channel B", "playlistId": "pl-2", "provider": "RSS", "watchOrder": 1}
            ]
            """.trimIndent()

        val VIDEOS_JSON: String =
            """
            [
              {
                "id": 11,
                "videoId": "abc",
                 "name": "First",
                 "publishDate": "2026-09-01T00:00:00Z",
                "duration": 100,
                "isDownloaded": true,
                "downloadedPath": "downloads/a.mp4",
                "thumb": "v/11.jpg"
              },
              {
                "id": 12,
                "videoId": "def",
                "name": "Second",
                "isDownloaded": false
              }
            ]
            """.trimIndent()
    }
}
