package xyz.foxocube.mediafeeder.androidtv

import android.content.Context
import androidx.compose.runtime.ProvidableCompositionLocal
import androidx.compose.runtime.compositionLocalOf
import okhttp3.OkHttpClient

/** Provided by [MainActivity] so every screen can reach the app services. */
val LocalAppDeps: ProvidableCompositionLocal<AppDeps> =
    compositionLocalOf { error("LocalAppDeps must be provided by the app root") }

/**
 * Application wiring: builds the [AuthStore] (shared preferences), an [OkHttpClient] and the
 * auth/HTTP services the screens consume. Exposed to the composition via [LocalAppDeps].
 */
class AppDeps(context: Context) {
    val store: AuthStore = AuthStore(context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE))
    val httpClient: OkHttpClient = OkHttpClient()
    // Factory (not a fixed instance) so a sign-in during the same session is visible to refresh().
    val tokenManager: TokenManager = TokenManager({ authClient() }, store, httpClient)
    val mediaApiClient: MediaApiClient = MediaApiClient(store, tokenManager, httpClient)

    /** OIDC metadata fetched during this session, keyed by base URL. */
    @Volatile
    private var metadataCache: Map<String, OidcMetadata> = emptyMap()

    /**
     * [AuthClient] from the stored OIDC settings, resolving its endpoints from the provider's
     * metadata document (fetched once per base URL). Null when the settings are incomplete.
     *
     * @throws Exception when the metadata document cannot be fetched, is unreadable, or lacks
     * the required endpoints.
     */
    suspend fun authClient(): AuthClient? {
        val oidcUrl: String = store.oidcUrl?.takeIf { it.isNotBlank() } ?: return null
        val deviceClientId: String = store.deviceClientId?.takeIf { it.isNotBlank() } ?: return null
        val serverClientId: String = store.serverClientId?.takeIf { it.isNotBlank() } ?: return null
        val metadata: OidcMetadata =
            metadataCache[oidcUrl] ?: fetchOidcMetadata(httpClient, oidcUrl).also { fetched ->
                metadataCache = metadataCache + (oidcUrl to fetched)
            }
        return AuthClient(deviceClientId, serverClientId, metadata)
    }

    /**
     * Fresh [DeviceAuthFlow] over the currently stored settings: [DeviceAuthOutcome.Success] when
     * ready to log in; [DeviceAuthOutcome.Failure] when the settings are incomplete or the OIDC
     * metadata cannot be fetched.
     */
    suspend fun createDeviceAuthFlow(): DeviceAuthOutcome<DeviceAuthFlow> {
        return try {
            val client: AuthClient? = authClient()
            if (client == null) return DeviceAuthOutcome.Failure(INCOMPLETE_SETTINGS)
            DeviceAuthOutcome.Success(DeviceAuthFlow(client, httpClient, store))
        } catch (e: Exception) {
            DeviceAuthOutcome.Failure(e.message ?: METADATA_UNAVAILABLE)
        }
    }

    /**
     * Startup auth check (run once before the first screen): a still-valid server token is
     * enough; otherwise one refresh attempt (device refresh + server exchange) is made when
     * a refresh token is stored. True when a valid token is available afterwards.
     */
    suspend fun startupAuth(): Boolean {
        if (tokenManager.getValidToken() != null) return true
        if (store.refreshToken == null) return false
        return tokenManager.refresh()
    }

    companion object {
        const val PREFS_NAME: String = "mediafeeder_prefs"
        const val INCOMPLETE_SETTINGS: String = "Server settings are incomplete"
        const val METADATA_UNAVAILABLE: String = "No response from the server"
    }
}
