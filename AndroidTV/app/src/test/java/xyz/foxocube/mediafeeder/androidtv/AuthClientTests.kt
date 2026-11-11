package xyz.foxocube.mediafeeder.androidtv

import okhttp3.FormBody
import okhttp3.Request
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

private const val OIDC_URL: String = "https://auth.example.com"
private const val DEVICE_CLIENT_ID: String = "device-client-1"
private const val SERVER_CLIENT_ID: String = "server-client-2"
private const val TOKEN_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/token/"
private const val DEVICE_AUTH_ENDPOINT: String = "$OIDC_URL/application/o/mediafeeder/device/"
private val METADATA: OidcMetadata = OidcMetadata(TOKEN_ENDPOINT, DEVICE_AUTH_ENDPOINT)
private const val DEVICE_CODE_GRANT: String = "urn:ietf:params:oauth:grant-type:device_code"
private const val TOKEN_JSON: String =
    """
    {
      "access_token": "access-1",
      "refresh_token": "refresh-2",
      "expires_in": 3600,
      "token_type": "Bearer",
      "scope": "openid offline_access"
    }
    """
private const val AT_EPOCH_MILLIS: Long = 1_750_000_000_000L
private const val EXPECTED_EXPIRES_AT: Long = AT_EPOCH_MILLIS + 3_600_000L

/**
 * Contract for the OIDC device-code auth request construction and token response parsing
 * (grant shapes from scripts/auth.py).
 */
class AuthClientTests {
    private val client: AuthClient = AuthClient(OIDC_URL, DEVICE_CLIENT_ID, SERVER_CLIENT_ID, METADATA)

    @Test
    fun `initial device code request posts only client id and scope per rfc 8628`() {
        val request: Request = client.initialDeviceCodeRequest()

        assertEquals("POST", request.method)
        assertEquals(DEVICE_AUTH_ENDPOINT, request.url.toString())
        assertTrue(request.body is FormBody, "expected a form-encoded body, got ${request.body}")
        assertNull(request.formValue("grant_type"), "device authorization takes no grant_type")
        assertEquals(DEVICE_CLIENT_ID, request.formValue("client_id"))
        assertEquals("openid offline_access", request.formValue("scope"))
        assertEquals("application/x-www-form-urlencoded", (request.body as FormBody).contentType().toString())
    }

    @Test
    fun `device code poll request posts device code and client id to the token endpoint`() {
        val request: Request = client.deviceCodePollRequest("dc-742")

        assertEquals("POST", request.method)
        assertEquals(TOKEN_ENDPOINT, request.url.toString())
        assertEquals(DEVICE_CODE_GRANT, request.formValue("grant_type"))
        assertEquals("dc-742", request.formValue("device_code"))
        assertEquals(DEVICE_CLIENT_ID, request.formValue("client_id"))
    }

    @Test
    fun `client credentials request exchanges the device access token for the server token`() {
        val request: Request = client.serverTokenRequest("device-access-token-abc")

        assertEquals("POST", request.method)
        assertEquals(TOKEN_ENDPOINT, request.url.toString())
        assertEquals("client_credentials", request.formValue("grant_type"))
        assertEquals(SERVER_CLIENT_ID, request.formValue("client_id"))
        assertEquals(
            "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            request.formValue("client_assertion_type"),
        )
        assertEquals("device-access-token-abc", request.formValue("client_assertion"))
    }

    @Test
    fun `refresh request posts the refresh token grant with the device client id`() {
        val request: Request = client.refreshRequest("refresh-token-123")

        assertEquals("POST", request.method)
        assertEquals(TOKEN_ENDPOINT, request.url.toString())
        assertEquals("refresh_token", request.formValue("grant_type"))
        assertEquals("refresh-token-123", request.formValue("refresh_token"))
        assertEquals(DEVICE_CLIENT_ID, request.formValue("client_id"))
    }

    @Test
    fun `token response parses access token, refresh token and expiry as epoch millis`() {
        val token: TokenResponse = client.parseTokenResponse(TOKEN_JSON, AT_EPOCH_MILLIS)

        assertEquals("access-1", token.accessToken)
        assertEquals("refresh-2", token.refreshToken)
        assertEquals(EXPECTED_EXPIRES_AT, token.expiresAtEpochMillis)
    }

    @Test
    fun `device code poll parses a successful token response as success`() {
        val result: PollResult = client.parsePollResponse(TOKEN_JSON, AT_EPOCH_MILLIS)
        val success: PollResult.Success = result as PollResult.Success

        assertEquals("access-1", success.token.accessToken)
        assertEquals("refresh-2", success.token.refreshToken)
        assertEquals(EXPECTED_EXPIRES_AT, success.token.expiresAtEpochMillis)
    }

    @Test
    fun `device code poll treats authorization pending as pending`() {
        val result: PollResult = client.parsePollResponse("""{"error": "authorization_pending"}""", AT_EPOCH_MILLIS)

        assertTrue(result is PollResult.Pending, "expected Pending, got $result")
    }

    @Test
    fun `device code poll treats slow down as a request to wait longer`() {
        val result: PollResult = client.parsePollResponse("""{"error": "slow_down"}""", AT_EPOCH_MILLIS)

        assertTrue(result is PollResult.SlowDown, "expected SlowDown, got $result")
    }

    @Test
    fun `device code poll parses a terminal error with description`() {
        val result: PollResult =
            client.parsePollResponse(
                """{"error": "expired_token", "error_description": "The device code has expired"}""",
                AT_EPOCH_MILLIS,
            )
        val error: PollResult.Error = result as PollResult.Error

        assertEquals("expired_token", error.code)
        assertEquals("The device code has expired", error.description)
    }

    @Test
    fun `device code poll parses a terminal error without description`() {
        val result: PollResult = client.parsePollResponse("""{"error": "access_denied"}""", AT_EPOCH_MILLIS)
        val error: PollResult.Error = result as PollResult.Error

        assertEquals("access_denied", error.code)
        assertNull(error.description, "error_description absent means null description, got ${error.description}")
    }
}

private fun Request.formFields(): Map<String, String> {
    val body = this.body as FormBody
    return (0 until body.size).associate { body.name(it) to body.value(it) }
}

private fun Request.formValue(name: String): String? = formFields()[name]
