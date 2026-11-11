package xyz.foxocube.mediafeeder.androidtv

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import okhttp3.OkHttpClient
import okhttp3.Request

/** The OIDC endpoints the auth client needs, resolved from the provider's metadata document (RFC 8414). */
data class OidcMetadata(
    /** Token endpoint for the device-code, refresh and client_credentials grants. */
    val tokenEndpoint: String,
    /** Device authorization endpoint (RFC 8628). */
    val deviceAuthorizationEndpoint: String,
)

/**
 * Fetches the provider's OIDC metadata document at
 * `{oidcUrl}/.well-known/openid-configuration` (mirroring scripts/auth.py) and resolves the
 * endpoints the auth client uses from it, instead of assuming provider-specific paths.
 *
 * @throws Exception when the document cannot be fetched, is not valid JSON, or lacks one of the
 * required endpoints.
 */
suspend fun fetchOidcMetadata(httpClient: OkHttpClient, oidcUrl: String): OidcMetadata {
    val base: String = oidcUrl.trimEnd { it == '/' }
    val url: String = "$base/.well-known/openid-configuration"
    val body: String =
        withContext(Dispatchers.IO) {
            httpClient
                .newCall(Request.Builder().url(url).get().build())
                .execute()
                .use { response ->
                    if (!response.isSuccessful) throw Exception("Could not fetch the OIDC metadata from $url: HTTP ${response.code}")
                    response.body.string()
                }
        }
    val dto: OidcMetadataDto =
        try {
            metadataJson.decodeFromString(OidcMetadataDto.serializer(), body)
        } catch (e: Exception) {
            throw Exception("Could not read the OIDC metadata at $url: ${e.message}", e)
        }
    return OidcMetadata(
        tokenEndpoint = checkNotNull(dto.tokenEndpoint) { "OIDC metadata at $url is missing token_endpoint" },
        deviceAuthorizationEndpoint =
            checkNotNull(dto.deviceAuthorizationEndpoint) { "OIDC metadata at $url is missing device_authorization_endpoint" },
    )
}

private val metadataJson: Json = Json { ignoreUnknownKeys = true }

/** Raw OIDC metadata document (RFC 8414) — only the fields this app resolves. */
@Serializable
private data class OidcMetadataDto(
    @SerialName("token_endpoint") val tokenEndpoint: String? = null,
    @SerialName("device_authorization_endpoint") val deviceAuthorizationEndpoint: String? = null,
)
