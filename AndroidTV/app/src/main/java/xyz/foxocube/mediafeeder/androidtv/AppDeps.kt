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

    /** [AuthClient] from the stored OIDC settings, or null when the settings are incomplete. */
    fun authClient(): AuthClient? {
        val oidcUrl: String = store.oidcUrl?.takeIf { it.isNotBlank() } ?: return null
        val deviceClientId: String = store.deviceClientId?.takeIf { it.isNotBlank() } ?: return null
        val serverClientId: String = store.serverClientId?.takeIf { it.isNotBlank() } ?: return null
        return AuthClient(oidcUrl, deviceClientId, serverClientId)
    }

    /** Fresh [DeviceAuthFlow] over the currently stored settings, or null when incomplete. */
    fun createDeviceAuthFlow(): DeviceAuthFlow? =
        authClient()?.let { DeviceAuthFlow(it, httpClient, store) }

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
    }
}
