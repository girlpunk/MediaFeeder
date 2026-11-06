package xyz.foxocube.mediafeeder.androidtv

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request

/**
 * Keeps the stored server token valid for API calls.
 *
 * [getValidToken] is the synchronous validity check (stored token + expiry). [refresh]
 * performs the renewal procedure: exchange the stored device access token for a new server
 * token; if the exchange is rejected and a device refresh token is stored, refresh the
 * device token once and retry the exchange.
 *
 * Time is injected via [clock] for deterministic tests.
 */
class TokenManager(
    private val authClientFactory: () -> AuthClient?,
    private val store: AuthStore,
    private val httpClient: OkHttpClient,
    private val clock: () -> Long = { System.currentTimeMillis() },
) {
    /** The stored server token, or null when absent or expired. */
    fun getValidToken(): String? {
        val token: String? = store.serverToken
        if (token == null) return null
        val expiresAt: Long = store.expiresAtEpochMillis ?: return null
        return if (clock() < expiresAt) token else null
    }

    /** Renew the stored server token in place. Returns true when a valid token is stored afterwards. */
    suspend fun refresh(): Boolean {
        if (getValidToken() != null) return true
        val client: AuthClient = authClientFactory() ?: return false
        var deviceAccessToken: String? = store.accessToken
        if (deviceAccessToken == null) return false
        if (exchangeServerToken(client, deviceAccessToken)) return true
        if (store.refreshToken == null) return false
        if (!updateDeviceToken(client)) return false
        deviceAccessToken = store.accessToken
        return deviceAccessToken != null && exchangeServerToken(client, deviceAccessToken)
    }

    private suspend fun exchangeServerToken(client: AuthClient, deviceAccessToken: String): Boolean {
        val json: String? = postForm(client.serverTokenRequest(deviceAccessToken))
        if (json == null) return false
        return try {
            val token: TokenResponse = client.parseTokenResponse(json, clock())
            store.serverToken = token.accessToken
            store.expiresAtEpochMillis = token.expiresAtEpochMillis
            true
        } catch (e: Exception) {
            false
        }
    }

    private suspend fun updateDeviceToken(client: AuthClient): Boolean {
        val refreshToken: String = store.refreshToken ?: return false
        val json: String? = postForm(client.refreshRequest(refreshToken))
        if (json == null) return false
        return try {
            val token: TokenResponse = client.parseTokenResponse(json, clock())
            store.accessToken = token.accessToken
            store.refreshToken = token.refreshToken
            true
        } catch (e: Exception) {
            false
        }
    }

    private suspend fun postForm(request: Request): String? =
        withContext(Dispatchers.IO) {
            try {
                httpClient.newCall(request).execute().use { response ->
                    if (response.isSuccessful) response.body?.string() else null
                }
            } catch (e: Exception) {
                null
            }
        }
}
