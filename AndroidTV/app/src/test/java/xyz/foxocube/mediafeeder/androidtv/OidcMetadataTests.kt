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
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Contract for [fetchOidcMetadata]: well-known document lookup and endpoint resolution, with
 * fetch/parse failures surfaced as exceptions. Pure JVM with a scripted OkHttp interceptor.
 */
class OidcMetadataTests {
    private val http: ScriptedHttp = ScriptedHttp()

    private fun httpClient(): OkHttpClient = OkHttpClient().newBuilder().addInterceptor(http).build()

    @Test
    fun `resolves both endpoints from the well-known document`() {
        http.enqueue(200, METADATA_JSON)

        val metadata: OidcMetadata = runBlocking { fetchOidcMetadata(httpClient(), OIDC_URL) }

        assertEquals(TOKEN_ENDPOINT, metadata.tokenEndpoint)
        assertEquals(DEVICE_AUTH_ENDPOINT, metadata.deviceAuthorizationEndpoint)
        assertEquals(1, http.requests.size)
        assertEquals("GET", http.requests.first().method)
        assertEquals("$OIDC_URL/.well-known/openid-configuration", http.requests.first().url.toString())
    }

    @Test
    fun `trailing slash on the base url still hits the well-known document`() {
        http.enqueue(200, METADATA_JSON)

        runBlocking { fetchOidcMetadata(httpClient(), "$OIDC_URL/") }

        assertEquals("$OIDC_URL/.well-known/openid-configuration", http.requests.first().url.toString())
    }

    @Test
    fun `non-2xx response fails with the status code in the message`() {
        http.enqueue(404, "{}")

        val failure: Exception = assertThrows(Exception::class.java) {
            runBlocking { fetchOidcMetadata(httpClient(), OIDC_URL) }
        }
        val message: String = failure.message ?: "no message"
        assertTrue(message.contains("404"), "expected the status code in the failure, got $message")
    }

    @Test
    fun `missing device authorization endpoint is reported by name`() {
        http.enqueue(200, """{"issuer": "$OIDC_URL", "token_endpoint": "$TOKEN_ENDPOINT"}""")

        val failure: Exception = assertThrows(Exception::class.java) {
            runBlocking { fetchOidcMetadata(httpClient(), OIDC_URL) }
        }
        val message: String = failure.message ?: "no message"
        assertTrue(
            message.contains("device_authorization_endpoint"),
            "expected the missing field named, got $message",
        )
    }

    @Test
    fun `missing token endpoint is reported by name`() {
        http.enqueue(200, """{"issuer": "$OIDC_URL", "device_authorization_endpoint": "$DEVICE_AUTH_ENDPOINT"}""")

        val failure: Exception = assertThrows(Exception::class.java) {
            runBlocking { fetchOidcMetadata(httpClient(), OIDC_URL) }
        }
        val message: String = failure.message ?: "no message"
        assertTrue(
            message.contains("token_endpoint"),
            "expected the missing field named, got $message",
        )
    }

    @Test
    fun `non-JSON metadata document fails as unreadable`() {
        http.enqueue(200, "this is not json")

        val failure: Exception = assertThrows(Exception::class.java) {
            runBlocking { fetchOidcMetadata(httpClient(), OIDC_URL) }
        }
        val message: String = failure.message ?: "no message"
        assertTrue(
            message.contains("Could not read"),
            "expected an unreadable-metadata failure, got $message",
        )
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
        const val TOKEN_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/token/"
        const val DEVICE_AUTH_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/device/"

        private val JSON_MEDIA_TYPE: MediaType = "application/json".toMediaType()

        const val METADATA_JSON: String =
            """{"issuer": "$OIDC_URL", "token_endpoint": "$TOKEN_ENDPOINT", "device_authorization_endpoint": "$DEVICE_AUTH_ENDPOINT"}"""
    }
}
