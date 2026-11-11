package xyz.foxocube.mediafeeder.androidtv

import java.util.concurrent.ConcurrentLinkedDeque
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.MediaType
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Contract for [TokenManager]: validity check of the stored server token and the renewal
 * procedure (server exchange, then device refresh + re-exchange). Pure JVM: a scripted
 * OkHttp interceptor stands in for the network and time is injected via a clock lambda.
 */
class TokenManagerTests {
    private val atEpochMillis: Long = 1_750_000_000_000L
    private var now: Long = atEpochMillis
    private val store: AuthStore = AuthStore(FakeSharedPreferences())
    private val authClient: AuthClient = AuthClient(OIDC_URL, DEVICE_CLIENT_ID, SERVER_CLIENT_ID, METADATA)

    private val clock: () -> Long = { now }

    /** Scripted one-response-per-call OkHttp interceptor (no network). */
    private class ScriptedHttp(
        private val responses: ConcurrentLinkedDeque<Pair<Int, String>>,
    ) : Interceptor {
        val requests: ConcurrentLinkedDeque<Request> = ConcurrentLinkedDeque()

        override fun intercept(chain: Interceptor.Chain): Response {
            val request: Request = chain.request()
            requests.add(request)
            val (code, body) = responses.removeFirst()
            return Response.Builder()
                .request(request)
                .protocol(Protocol.HTTP_1_1)
                .code(code)
                .message(if (code in 200..299) "OK" else "ERROR")
                .body(body.toResponseBody(JSON_MEDIA_TYPE))
                .build()
        }
    }

    private fun manager(http: ScriptedHttp): TokenManager =
        TokenManager({ authClient }, store, OkHttpClient().newBuilder().addInterceptor(http).build(), clock)

    @Test
    fun `getValidToken returns the stored server token while it is unexpired`() {
        store.serverToken = "srv-1"
        store.expiresAtEpochMillis = now + 10_000L

        assertEquals("srv-1", TokenManager({ null }, store, OkHttpClient(), clock).getValidToken())
    }

    @Test
    fun `getValidToken is null when the token or the expiry is missing`() {
        assertNull(TokenManager({ null }, store, OkHttpClient(), clock).getValidToken(), "no token at all")

        store.serverToken = "srv-1"
        assertNull(TokenManager({ null }, store, OkHttpClient(), clock).getValidToken(), "no expiry")
    }

    @Test
    fun `getValidToken is null once the token is expired`() {
        store.serverToken = "srv-1"
        store.expiresAtEpochMillis = atEpochMillis + 10_000L
        now = atEpochMillis + 11_000L

        assertNull(TokenManager({ null }, store, OkHttpClient(), clock).getValidToken())
    }

    @Test
    fun `refresh keeps a still-valid server token without calling the server`() {
        store.serverToken = "srv-1"
        store.expiresAtEpochMillis = now + 10_000L
        val http: ScriptedHttp = ScriptedHttp(ConcurrentLinkedDeque())

        val result: Boolean = runBlocking { manager(http).refresh() }

        assertTrue(result)
        assertTrue(http.requests.isEmpty(), "no HTTP call expected, got ${http.requests}")
    }

    @Test
    fun `refresh exchanges the stored device token for a new server token`() {
        store.accessToken = "dev-acc"
        val http: ScriptedHttp = ScriptedHttp(
            ConcurrentLinkedDeque(listOf(200 to SERVER_TOKEN_JSON)),
        )

        val result: Boolean = runBlocking { manager(http).refresh() }

        assertTrue(result)
        assertEquals(1, http.requests.size)
        assertEquals("access-1", store.serverToken)
        assertEquals(atEpochMillis + 3_600_000L, store.expiresAtEpochMillis)
    }

    @Test
    fun `refresh refreshes the device token when the server exchange is rejected`() {
        store.accessToken = "dev-acc"
        store.refreshToken = "dev-refresh"
        val http: ScriptedHttp = ScriptedHttp(
            ConcurrentLinkedDeque(
                listOf(
                    400 to "{\"error\": \"invalid_client_assertion\"}",
                    200 to DEVICE_REFRESH_JSON,
                    200 to SERVER_TOKEN_JSON,
                ),
            ),
        )

        val result: Boolean = runBlocking { manager(http).refresh() }

        assertTrue(result)
        assertEquals(3, http.requests.size)
        assertEquals("dev-refreshed", store.accessToken)
        assertEquals("access-1", store.serverToken)
    }

    @Test
    fun `refresh is false when no device token is stored`() {
        val http: ScriptedHttp = ScriptedHttp(ConcurrentLinkedDeque())

        val result: Boolean = runBlocking { manager(http).refresh() }

        assertFalse(result)
        assertTrue(http.requests.isEmpty(), "no HTTP call expected, got ${http.requests}")
    }

    @Test
    fun `refresh is false when the device refresh token is also missing`() {
        store.accessToken = "dev-acc"
        val http: ScriptedHttp = ScriptedHttp(
            ConcurrentLinkedDeque(listOf(400 to "{\"error\": \"invalid_client_assertion\"}")),
        )

        val result: Boolean = runBlocking { manager(http).refresh() }

        assertFalse(result)
        assertEquals(1, http.requests.size)
        assertNull(store.serverToken)
    }

    private companion object {
        const val OIDC_URL: String = "https://auth.example.com"
        const val DEVICE_CLIENT_ID: String = "device-client-1"
        const val SERVER_CLIENT_ID: String = "server-client-2"
        const val TOKEN_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/token/"
        const val DEVICE_AUTH_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/device/"

        private val JSON_MEDIA_TYPE: MediaType = "application/json".toMediaType()
        private val METADATA: OidcMetadata = OidcMetadata(TOKEN_ENDPOINT, DEVICE_AUTH_ENDPOINT)

        const val SERVER_TOKEN_JSON: String =
            """{"access_token": "access-1", "refresh_token": "refresh-2", "expires_in": 3600}"""

        const val DEVICE_REFRESH_JSON: String =
            """{"access_token": "dev-refreshed", "refresh_token": "refresh-3", "expires_in": 500}"""
    }
}
