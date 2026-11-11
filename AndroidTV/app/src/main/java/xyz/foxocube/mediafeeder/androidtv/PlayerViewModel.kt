package xyz.foxocube.mediafeeder.androidtv

import androidx.lifecycle.ViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Player screen state: resolves the [Video] into a [PlayerState] via [MediaUrlResolver].
 *
 * [PlayerState.NotDownloaded] covers both the pre-play check (no local download) and a
 * 412 "Not Downloaded" reported by the server at play time.
 */
class PlayerViewModel(
    private val store: AuthStore,
    private val tokenManager: TokenManager,
    private val video: Video,
) : ViewModel() {

    sealed interface PlayerState {
        data object NotDownloaded : PlayerState

        /** [Playable] media; [authHeader] is null for direct remote playback. */
        data class Playable(val url: String, val authHeader: String?) : PlayerState
    }

    private val _state: MutableStateFlow<PlayerState> = MutableStateFlow(resolve())
    val state: StateFlow<PlayerState> = _state.asStateFlow()

    /** Re-resolve (Retry button on the "Not downloaded" card). */
    fun retry() {
        _state.value = resolve()
    }

    /** The server answered 412 while playing: switch to the "Not downloaded" state. */
    fun onNotDownloaded() {
        _state.value = PlayerState.NotDownloaded
    }

    private fun resolve(): PlayerState {
        val base: String = store.baseUrl.orEmpty()
        return when (val url = MediaUrlResolver.resolve(base, video)) {
            is PlaybackUrl.NotDownloaded -> PlayerState.NotDownloaded
            is PlaybackUrl.Remote -> PlayerState.Playable(url.url, authHeader = null)
            is PlaybackUrl.Streamed -> PlayerState.Playable(url.url, authHeader = bearerHeader())
        }
    }

    private fun bearerHeader(): String? = tokenManager.getValidToken()?.let { "Bearer $it" }
}
