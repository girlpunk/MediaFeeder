package xyz.foxocube.mediafeeder.androidtv

import android.content.SharedPreferences

/**
 * Persists the user's server/OIDC settings and tokens in [SharedPreferences].
 *
 * Constructed by the UI layer with the app's shared preferences instance; every write is
 * persisted immediately so a fresh instance over the same preferences sees the same values.
 */
class AuthStore(private val prefs: SharedPreferences) {
    /** MediaFeeder server base URL, e.g. "http://192.168.1.10:8080". */
    var baseUrl: String?
        get() = prefs.getString(KEY_BASE_URL, null)
        set(value) { prefs.edit().putString(KEY_BASE_URL, value).apply() }

    /** OIDC (Authentik) base URL, e.g. "https://auth.example.com". */
    var oidcUrl: String?
        get() = prefs.getString(KEY_OIDC_URL, null)
        set(value) { prefs.edit().putString(KEY_OIDC_URL, value).apply() }

    /** Device-flow OIDC client id (the TV app's own OIDC app). */
    var deviceClientId: String?
        get() = prefs.getString(KEY_DEVICE_CLIENT_ID, null)
        set(value) { prefs.edit().putString(KEY_DEVICE_CLIENT_ID, value).apply() }

    /** Server-flow OIDC client id (used for the client_credentials server token). */
    var serverClientId: String?
        get() = prefs.getString(KEY_SERVER_CLIENT_ID, null)
        set(value) { prefs.edit().putString(KEY_SERVER_CLIENT_ID, value).apply() }

    /** Current device access token (client assertion for the server token, API bearer). */
    var accessToken: String?
        get() = prefs.getString(KEY_ACCESS_TOKEN, null)
        set(value) { prefs.edit().putString(KEY_ACCESS_TOKEN, value).apply() }

    /** Stored device refresh token. */
    var refreshToken: String?
        get() = prefs.getString(KEY_REFRESH_TOKEN, null)
        set(value) { prefs.edit().putString(KEY_REFRESH_TOKEN, value).apply() }

    /**
     * Cached server access token (client_credentials exchange used as the API bearer).
     * The refresh flow re-derives it from [accessToken] + [refreshToken], so it is cache only.
     */
    var serverToken: String?
        get() = prefs.getString(KEY_SERVER_TOKEN, null)
        set(value) { prefs.edit().putString(KEY_SERVER_TOKEN, value).apply() }

    /** Epoch millis when the stored [serverToken] expires (null = unknown / not set). */
    var expiresAtEpochMillis: Long?
        get() = prefs.getString(KEY_EXPIRES_AT_EPOCH_MILLIS, null)?.toLongOrNull()
        set(value) { prefs.edit().putString(KEY_EXPIRES_AT_EPOCH_MILLIS, value?.toString()).apply() }

    /** Removes all stored settings and tokens (logout). */
    fun clear() {
        prefs.edit().clear().apply()
    }

    private companion object {
        const val KEY_BASE_URL: String = "baseUrl"
        const val KEY_OIDC_URL: String = "oidcUrl"
        const val KEY_DEVICE_CLIENT_ID: String = "deviceClientId"
        const val KEY_SERVER_CLIENT_ID: String = "serverClientId"
        const val KEY_ACCESS_TOKEN: String = "accessToken"
        const val KEY_REFRESH_TOKEN: String = "refreshToken"
        const val KEY_SERVER_TOKEN: String = "serverToken"
        const val KEY_EXPIRES_AT_EPOCH_MILLIS: String = "expiresAtEpochMillis"
    }
}
