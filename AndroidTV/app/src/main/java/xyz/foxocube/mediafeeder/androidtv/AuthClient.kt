package xyz.foxocube.mediafeeder.androidtv

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import okhttp3.FormBody
import okhttp3.Request

/**
 * OIDC device-code auth (mirrors scripts/auth.py): device-code grant -> refresh grant ->
 * client_credentials exchange for the "server token".
 *
 * The OAuth2 endpoints are not hardcoded: they are resolved from the provider's OIDC metadata
 * document ([OidcMetadata], fetched by [AppDeps] from
 * `{oidcUrl}/.well-known/openid-configuration`).
 *
 * The request builders are pure: they construct the form-encoded OAuth2 [Request]s the app
 * will POST with OkHttp; the parsers turn raw token-endpoint JSON into [TokenResponse].
 */
class AuthClient(
    val oidcUrl: String,
    val deviceClientId: String,
    val serverClientId: String,
    private val metadata: OidcMetadata,
) {
    private val jsonFormat: Json = Json { ignoreUnknownKeys = true }

    private val deviceEndpoint: String = metadata.deviceAuthorizationEndpoint
    private val tokenEndpoint: String = metadata.tokenEndpoint

    /** First step of the device-code flow (device authorization endpoint, RFC 8628: client_id + scope only). */
    fun initialDeviceCodeRequest(): Request = formRequest(
        deviceEndpoint,
        "client_id" to deviceClientId,
        "scope" to "openid offline_access",
    )

    /** Poll the token endpoint with the device code until the user approves on their phone. */
    fun deviceCodePollRequest(deviceCode: String): Request = formRequest(
        tokenEndpoint,
        "grant_type" to DEVICE_CODE_GRANT,
        "device_code" to deviceCode,
        "client_id" to deviceClientId,
    )

    /** Exchange the device access token (client assertion) for the server token (client_credentials). */
    fun serverTokenRequest(deviceAccessToken: String): Request = formRequest(
        tokenEndpoint,
        "grant_type" to "client_credentials",
        "client_id" to serverClientId,
        "client_assertion_type" to CLIENT_ASSERTION_TYPE,
        "client_assertion" to deviceAccessToken,
    )

    /** Refresh the device access token using the stored refresh token. */
    fun refreshRequest(refreshToken: String): Request = formRequest(
        tokenEndpoint,
        "grant_type" to "refresh_token",
        "refresh_token" to refreshToken,
        "client_id" to deviceClientId,
    )

    /** Parse an OAuth2 token response; `expiresAtEpochMillis` = `atEpochMillis` + expires_in * 1000. */
    fun parseTokenResponse(json: String, atEpochMillis: Long): TokenResponse {
        val dto: TokenDto = jsonFormat.decodeFromString(TokenDto.serializer(), json)
        val accessToken: String = checkNotNull(dto.accessToken) { "token response is missing access_token" }
        val refreshToken: String = checkNotNull(dto.refreshToken) { "token response is missing refresh_token" }
        val expiresIn: Long = checkNotNull(dto.expiresIn) { "token response is missing expires_in" }
        return TokenResponse(accessToken, refreshToken, atEpochMillis + expiresIn * 1000L)
    }

    /** Parse a device-code poll response body (success token / authorization_pending / terminal error). */
    fun parsePollResponse(json: String, atEpochMillis: Long): PollResult {
        val dto: TokenDto = jsonFormat.decodeFromString(TokenDto.serializer(), json)
        val error: String? = dto.error
        if (error == null) {
            return PollResult.Success(parseTokenResponse(json, atEpochMillis))
        }
        if (error == ERROR_AUTHORIZATION_PENDING) {
            return PollResult.Pending
        }
        return PollResult.Error(error, dto.errorDescription)
    }

    private fun formRequest(url: String, vararg fields: Pair<String, String>): Request {
        val body: FormBody = FormBody.Builder().apply { fields.forEach { (name, value) -> add(name, value) } }.build()
        return Request.Builder().url(url).post(body).build()
    }

    private companion object {
        const val DEVICE_CODE_GRANT: String = "urn:ietf:params:oauth:grant-type:device_code"
        const val CLIENT_ASSERTION_TYPE: String = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"
        const val ERROR_AUTHORIZATION_PENDING: String = "authorization_pending"
    }
}

/** Parsed OAuth2 token response from the OIDC token endpoint. */
data class TokenResponse(
    val accessToken: String,
    val refreshToken: String,
    /** Epoch millis when the access token expires (issue time + expires_in seconds). */
    val expiresAtEpochMillis: Long,
)

/** Outcome of one device-code poll against the token endpoint. */
sealed interface PollResult {
    /** The user approved: tokens were issued. */
    data class Success(val token: TokenResponse) : PollResult

    /** authorization_pending: keep polling. */
    data object Pending : PollResult

    /** A terminal authorization_server error (e.g. expired_token, access_denied). */
    data class Error(val code: String, val description: String?) : PollResult
}

/** Raw token-endpoint response: token fields on success, `error`/`error_description` on failure. */
@Serializable
private data class TokenDto(
    @SerialName("access_token") val accessToken: String? = null,
    @SerialName("refresh_token") val refreshToken: String? = null,
    @SerialName("expires_in") val expiresIn: Long? = null,
    @SerialName("error") val error: String? = null,
    @SerialName("error_description") val errorDescription: String? = null,
)
