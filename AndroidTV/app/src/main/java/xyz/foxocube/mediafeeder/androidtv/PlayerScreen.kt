package xyz.foxocube.mediafeeder.androidtv

import android.content.Context
import android.view.KeyEvent
import androidx.compose.foundation.background
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.media3.common.MediaItem
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.util.ExperimentalApi
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.datasource.HttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.ProgressiveMediaSource
import androidx.media3.ui.compose.material3.Player as Media3Player
import androidx.tv.material3.MaterialTheme
import androidx.tv.material3.WideButton

/**
 * Player screen (route: [NavRoutes.PLAYER]): plays the [NavigationSelection.video].
 *
 * Remote playback needs no auth; streamed /play URLs carry the server bearer token. A
 * 412 at play time (or a missing download) shows the "Not downloaded" card with Retry.
 * D-pad center on the focused player surface toggles play/pause; back returns to the list.
 */
@Composable
fun PlayerScreen(onBack: () -> Unit) {
    val deps: AppDeps = LocalAppDeps.current
    val video: Video? = remember { NavigationSelection.video }
    if (video == null) {
        LaunchedEffect(Unit) { onBack() }
        return
    }
    val vm: PlayerViewModel = viewModel(
        factory = viewModelFactory {
            initializer { PlayerViewModel(deps.store, deps.tokenManager, video) }
        },
    )
    val state: PlayerViewModel.PlayerState = vm.state.collectAsState().value

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(TvTokens.backgroundColor),
    ) {
        when (state) {
            PlayerViewModel.PlayerState.NotDownloaded -> NotDownloadedCard(onRetry = vm::retry)
            is PlayerViewModel.PlayerState.Playable ->
                PlayerSurface(video = video, playable = state, onNotDownloaded = vm::onNotDownloaded)
        }
    }
}

@Composable
private fun NotDownloadedCard(onRetry: () -> Unit) {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier
                .background(MaterialTheme.colorScheme.surface, TvTokens.cardShape)
                .padding(32.dp),
        ) {
            Text(
                text = "Not downloaded",
                style = MaterialTheme.typography.headlineMedium,
            )
            Spacer(Modifier.height(TvTokens.itemSpacing))
            WideButton(onClick = onRetry) {
                Text(text = "Retry")
            }
        }
    }
}

/** ExoPlayer surface: auto-plays [playable], D-pad center toggles play/pause. */
@OptIn(ExperimentalApi::class)
@Composable
private fun PlayerSurface(
    video: Video,
    playable: PlayerViewModel.PlayerState.Playable,
    onNotDownloaded: () -> Unit,
) {
    val context: Context = LocalContext.current
    val player: ExoPlayer = remember(playable) {
        ExoPlayer.Builder(context).apply {
            playable.authHeader?.let { header ->
                setMediaSourceFactory(
                    ProgressiveMediaSource.Factory(
                        DefaultHttpDataSource.Factory().apply {
                            setDefaultRequestProperties(mapOf(HEADER_AUTHORIZATION to header))
                        },
                    ),
                )
            }
        }.build()
    }
    var isPlaying by remember(player) { mutableStateOf(true) }

    DisposableEffect(player) {
        val listener = object : Player.Listener {
            override fun onIsPlayingChanged(isNowPlaying: Boolean) {
                isPlaying = isNowPlaying
            }

            override fun onPlayerError(error: PlaybackException) {
                val cause: Throwable = error.cause ?: error
                if (cause is HttpDataSource.InvalidResponseCodeException &&
                    cause.responseCode == HTTP_NOT_DOWNLOADABLE
                ) {
                    onNotDownloaded()
                }
            }
        }
        player.addListener(listener)
        onDispose {
            player.removeListener(listener)
            player.release()
        }
    }

    LaunchedEffect(playable) {
        player.setMediaItem(MediaItem.fromUri(playable.url))
        player.play()
    }

    val focusRequester: FocusRequester = remember { FocusRequester() }
    LaunchedEffect(Unit) { focusRequester.requestFocus() }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .focusRequester(focusRequester)
            .focusable()
            .onPreviewKeyEvent { event ->
                if (event.nativeKeyEvent.keyCode == KeyEvent.KEYCODE_DPAD_CENTER &&
                    event.nativeKeyEvent.action == KeyEvent.ACTION_UP
                ) {
                    if (isPlaying) {
                        player.pause()
                    } else {
                        player.play()
                    }
                    true
                } else {
                    false
                }
            },
    ) {
        Media3Player(
            player = player,
            modifier = Modifier.fillMaxSize(),
            contentScale = ContentScale.Fit,
            showControls = false,
        )
        Column(
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(24.dp),
        ) {
            Text(
                text = video.name,
                style = MaterialTheme.typography.headlineSmall,
                color = Color.White,
            )
            if (video.publishedDate != null) {
                Text(
                    text = video.publishedDate,
                    style = MaterialTheme.typography.bodyMedium,
                    color = Color.White,
                )
            }
        }
        Text(
            text = if (isPlaying) "Playing — press OK to pause" else "Paused — press OK to play",
            style = MaterialTheme.typography.titleMedium,
            color = Color.White,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(24.dp),
        )
    }
}

private const val HEADER_AUTHORIZATION: String = "Authorization"

private const val HTTP_NOT_DOWNLOADABLE: Int = 412
