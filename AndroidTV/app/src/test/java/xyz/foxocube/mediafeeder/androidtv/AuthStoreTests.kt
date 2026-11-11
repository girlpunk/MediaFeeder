package xyz.foxocube.mediafeeder.androidtv

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

/**
 * Contract for persisting the user's server/OIDC settings and tokens in SharedPreferences.
 * Uses a hand-written [FakeSharedPreferences] (pure JVM, no Robolectric, no androidx.test).
 */
class AuthStoreTests {
    private val baseUrl: String = "http://192.168.1.10:8080"
    private val oidcUrl: String = "https://auth.example.com"
    private val deviceClientId: String = "device-client-1"
    private val serverClientId: String = "server-client-2"
    private val accessToken: String = "device-access-token"
    private val refreshToken: String = "device-refresh-token"

    @Test
    fun `fresh store over empty preferences exposes no settings`() {
        val store: AuthStore = AuthStore(FakeSharedPreferences())

        assertNull(store.baseUrl)
        assertNull(store.oidcUrl)
        assertNull(store.deviceClientId)
        assertNull(store.serverClientId)
        assertNull(store.accessToken)
        assertNull(store.refreshToken)
    }

    @Test
    fun `settings round trip through preferences into a new store instance`() {
        val preferences: FakeSharedPreferences = FakeSharedPreferences()
        val first: AuthStore = AuthStore(preferences)
        first.baseUrl = baseUrl
        first.oidcUrl = oidcUrl
        first.deviceClientId = deviceClientId
        first.serverClientId = serverClientId
        first.accessToken = accessToken
        first.refreshToken = refreshToken

        val second: AuthStore = AuthStore(preferences)
        assertEquals(baseUrl, second.baseUrl)
        assertEquals(oidcUrl, second.oidcUrl)
        assertEquals(deviceClientId, second.deviceClientId)
        assertEquals(serverClientId, second.serverClientId)
        assertEquals(accessToken, second.accessToken)
        assertEquals(refreshToken, second.refreshToken)
    }

    @Test
    fun `updating one setting leaves the others untouched`() {
        val preferences: FakeSharedPreferences = FakeSharedPreferences()
        val store: AuthStore = AuthStore(preferences)

        store.baseUrl = baseUrl

        assertEquals(baseUrl, store.baseUrl)
        assertNull(store.oidcUrl)
        assertNull(store.accessToken)
    }

    @Test
    fun `clear removes all stored settings and tokens`() {
        val preferences: FakeSharedPreferences = FakeSharedPreferences()
        val store: AuthStore = AuthStore(preferences)
        store.baseUrl = baseUrl
        store.oidcUrl = oidcUrl
        store.deviceClientId = deviceClientId
        store.serverClientId = serverClientId
        store.accessToken = accessToken
        store.refreshToken = refreshToken

        store.clear()

        val second: AuthStore = AuthStore(preferences)
        assertNull(second.baseUrl)
        assertNull(second.oidcUrl)
        assertNull(second.deviceClientId)
        assertNull(second.serverClientId)
        assertNull(second.accessToken)
        assertNull(second.refreshToken)
    }
}
