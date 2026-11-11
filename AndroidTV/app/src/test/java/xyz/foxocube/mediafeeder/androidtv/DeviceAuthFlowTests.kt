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
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Contract for [DeviceAuthFlow]: device-code request, polling outcomes, and the
 * server-token exchange persisting both token pairs. Pure JVM with a scripted OkHttp
 * interceptor and an injected clock.
 */
class DeviceAuthFlowTests {
    private val atEpochMillis: Long = 1_750_000_000_000L
    private var now: Long = atEpochMillis
    private val store: AuthStore = AuthStore(FakeSharedPreferences())
    private val authClient: AuthClient = AuthClient(OIDC_URL, DEVICE_CLIENT_ID, SERVER_CLIENT_ID, METADATA)
    private val http: ScriptedHttp = ScriptedHttp()
    private val flow: DeviceAuthFlow =
        DeviceAuthFlow(authClient, OkHttpClient().newBuilder().addInterceptor(http).build(), store, { now })
    private val sleptMillis: MutableList<Long> = mutableListOf()
    private val polledFlow: DeviceAuthFlow =
        DeviceAuthFlow(authClient, OkHttpClient().newBuilder().addInterceptor(http).build(), store, { now }) { millis ->
            now += millis
            sleptMillis.add(millis)
        }

    @Test
    fun `requestUserCode parses the device code challenge`() {
        http.enqueue(
            200,
            """
            {
              "device_code": "dc-1",
              "user_code": "ABCD-1234",
              "verification_uri": "https://auth.example.com/verify",
              "interval": 5,
              "expires_in": 900
            }
            """.trimIndent(),
        )

        val outcome: DeviceAuthOutcome<DeviceCodeInfo> = runBlocking { flow.requestUserCode() }

        val success: DeviceAuthOutcome.Success<DeviceCodeInfo> = outcome as DeviceAuthOutcome.Success
        assertEquals("dc-1", success.value.deviceCode)
        assertEquals("ABCD-1234", success.value.userCode)
        assertEquals("https://auth.example.com/verify", success.value.verificationUri)
        assertEquals(5L, success.value.intervalSeconds)
        assertEquals(900L, success.value.expiresInSeconds)
        assertEquals(1, http.requests.size)
        assertEquals(DEVICE_AUTH_ENDPOINT, http.requests.first().url.toString())
    }

    @Test
    fun `requestUserCode applies the rfc default interval when the server omits it`() {
        http.enqueue(200, """{"device_code": "dc-2", "user_code": "WXYZ-5678", "verification_uri": "https://auth.example.com/verify"}""")

        val outcome: DeviceAuthOutcome<DeviceCodeInfo> = runBlocking { flow.requestUserCode() }

        val success: DeviceAuthOutcome.Success<DeviceCodeInfo> = outcome as DeviceAuthOutcome.Success
        assertEquals(5L, success.value.intervalSeconds)
        assertNull(success.value.expiresInSeconds)
    }

    @Test
    fun `requestUserCode fails when the server errors`() {
        http.enqueue(500, "{}")

        val outcome: DeviceAuthOutcome<DeviceCodeInfo> = runBlocking { flow.requestUserCode() }

        assertTrue(outcome is DeviceAuthOutcome.Failure, "expected Failure, got $outcome")
    }

    @Test
    fun `pollOnce reports authorization pending`() {
        http.enqueue(200, PENDING_JSON)

        val result: PollResult = runBlocking { flow.pollOnce("dc-1") }

        assertTrue(result is PollResult.Pending, "expected Pending, got $result")
    }

    @Test
    fun `pollOnce keeps polling on a 400 authorization pending response`() {
        http.enqueue(400, PENDING_JSON)

        val result: PollResult = runBlocking { flow.pollOnce("dc-1") }

        assertTrue(result is PollResult.Pending, "expected Pending, got $result")
    }

    @Test
    fun `pollOnce reports slow down`() {
        http.enqueue(400, SLOW_DOWN_JSON)

        val result: PollResult = runBlocking { flow.pollOnce("dc-1") }

        assertTrue(result is PollResult.SlowDown, "expected SlowDown, got $result")
    }

    @Test
    fun `pollUntilSettled checks every ten seconds when the server asks for less`() {
        http.enqueue(400, PENDING_JSON)
        http.enqueue(200, TOKEN_JSON)
        val info: DeviceCodeInfo = DeviceCodeInfo("dc-1", "ABCD-1234", "https://auth.example.com/verify", 5, 600)

        val outcome: DeviceAuthOutcome<TokenResponse> = runBlocking { polledFlow.pollUntilSettled(info) }

        val success: DeviceAuthOutcome.Success<TokenResponse> = outcome as DeviceAuthOutcome.Success
        assertEquals("access-1", success.value.accessToken)
        assertEquals(listOf(10_000L, 10_000L), sleptMillis)
    }

    @Test
    fun `pollUntilSettled respects a longer interval from the server`() {
        http.enqueue(400, PENDING_JSON)
        http.enqueue(200, TOKEN_JSON)
        val info: DeviceCodeInfo = DeviceCodeInfo("dc-1", "ABCD-1234", "https://auth.example.com/verify", 30, 600)

        val outcome: DeviceAuthOutcome<TokenResponse> = runBlocking { polledFlow.pollUntilSettled(info) }

        assertTrue(outcome is DeviceAuthOutcome.Success, "expected Success, got $outcome")
        assertEquals(listOf(30_000L, 30_000L), sleptMillis)
    }

    @Test
    fun `pollUntilSettled waits longer after a slow down response`() {
        http.enqueue(400, SLOW_DOWN_JSON)
        http.enqueue(400, PENDING_JSON)
        http.enqueue(200, TOKEN_JSON)
        val info: DeviceCodeInfo = DeviceCodeInfo("dc-1", "ABCD-1234", "https://auth.example.com/verify", 5, 600)

        val outcome: DeviceAuthOutcome<TokenResponse> = runBlocking { polledFlow.pollUntilSettled(info) }

        assertTrue(outcome is DeviceAuthOutcome.Success, "expected Success, got $outcome")
        assertEquals(listOf(10_000L, 15_000L, 15_000L), sleptMillis)
    }

    @Test
    fun `pollUntilSettled fails when the device code expires before approval`() {
        http.enqueue(400, PENDING_JSON)
        http.enqueue(400, PENDING_JSON)
        val info: DeviceCodeInfo = DeviceCodeInfo("dc-1", "ABCD-1234", "https://auth.example.com/verify", 10, 20)

        val outcome: DeviceAuthOutcome<TokenResponse> = runBlocking { polledFlow.pollUntilSettled(info) }

        val failure: DeviceAuthOutcome.Failure = outcome as DeviceAuthOutcome.Failure
        assertEquals("The device code has expired. Please sign in again.", failure.message)
        assertEquals(listOf(10_000L, 10_000L), sleptMillis)
        assertEquals(2, http.requests.size, "no further polls once the deadline is reached")
    }

    @Test
    fun `pollUntilSettled fails on a terminal error`() {
        http.enqueue(400, """{"error": "expired_token", "error_description": "The device code has expired"}""")
        val info: DeviceCodeInfo = DeviceCodeInfo("dc-1", "ABCD-1234", "https://auth.example.com/verify", 10, 600)

        val outcome: DeviceAuthOutcome<TokenResponse> = runBlocking { polledFlow.pollUntilSettled(info) }

        val failure: DeviceAuthOutcome.Failure = outcome as DeviceAuthOutcome.Failure
        assertEquals("Sign-in failed: expired_token (The device code has expired)", failure.message)
    }

    @Test
    fun `pollOnce reports a success token response`() {
        http.enqueue(200, TOKEN_JSON)

        val result: PollResult = runBlocking { flow.pollOnce("dc-1") }

        val success: PollResult.Success = result as PollResult.Success
        assertEquals("access-1", success.token.accessToken)
        assertEquals(atEpochMillis + 3_600_000L, success.token.expiresAtEpochMillis)
    }

    @Test
    fun `pollOnce reports a terminal expired_token error`() {
        http.enqueue(200, """{"error": "expired_token", "error_description": "The device code has expired"}""")

        val result: PollResult = runBlocking { flow.pollOnce("dc-1") }

        val error: PollResult.Error = result as PollResult.Error
        assertEquals("expired_token", error.code)
        assertEquals("The device code has expired", error.description)
    }

    @Test
    fun `completeLogin persists device and server tokens with expiry`() {
        http.enqueue(200, SERVER_TOKEN_JSON)
        val deviceToken: TokenResponse = TokenResponse("dev-acc", "dev-refresh", atEpochMillis + 86_400_000L)

        val outcome: DeviceAuthOutcome<TokenResponse> = runBlocking { flow.completeLogin(deviceToken) }

        val success: DeviceAuthOutcome.Success<TokenResponse> = outcome as DeviceAuthOutcome.Success
        assertEquals("server-acc", success.value.accessToken)
        assertEquals("dev-acc", store.accessToken)
        assertEquals("dev-refresh", store.refreshToken)
        assertEquals("server-acc", store.serverToken)
        assertEquals(atEpochMillis + 3_600_000L, store.expiresAtEpochMillis)
    }

    @Test
    fun `completeLogin keeps the device token when the exchange fails`() {
        http.enqueue(500, "{}")
        val deviceToken: TokenResponse = TokenResponse("dev-acc", "dev-refresh", atEpochMillis + 86_400_000L)

        val outcome: DeviceAuthOutcome<TokenResponse> = runBlocking { flow.completeLogin(deviceToken) }

        assertTrue(outcome is DeviceAuthOutcome.Failure, "expected Failure, got $outcome")
        assertEquals("dev-acc", store.accessToken, "device token keeps a later retry possible")
        assertEquals("dev-refresh", store.refreshToken)
        assertNull(store.serverToken)
    }

    /** One scripted response per call (no network). */
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
                .body(body.toResponseBody(JSON_MEDIA_TYPE))
                .build()
        }
    }

    private companion object {
        const val OIDC_URL: String = "https://auth.example.com"
        const val DEVICE_CLIENT_ID: String = "device-client-1"
        const val SERVER_CLIENT_ID: String = "server-client-2"
        const val TOKEN_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/token/"
        const val DEVICE_AUTH_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/device/"

        private val JSON_MEDIA_TYPE: MediaType = "application/json".toMediaType()
        private val METADATA: OidcMetadata = OidcMetadata(TOKEN_ENDPOINT, DEVICE_AUTH_ENDPOINT)

        const val TOKEN_JSON: String =
            """{"access_token": "access-1", "refresh_token": "refresh-2", "expires_in": 3600}"""

        const val PENDING_JSON: String = """{"error": "authorization_pending"}"""
        const val SLOW_DOWN_JSON: String = """{"error": "slow_down"}"""

        const val SERVER_TOKEN_JSON: String =
            """{"access_token": "server-acc", "refresh_token": "server-refresh", "expires_in": 3600}"""
    }
}
