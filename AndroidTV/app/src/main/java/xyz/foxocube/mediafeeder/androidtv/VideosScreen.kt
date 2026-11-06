package xyz.foxocube.mediafeeder.androidtv

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.ui.Modifier
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory

/**
 * Videos screen (route: [NavRoutes.VIDEOS]): D-pad list of the focused subscription's
 * videos (name + publish date). Bearer 401/403 re-authenticates via [onAuthRequired].
 */
@Composable
fun VideosScreen(
    subscriptionId: Int,
    onPlay: (Video) -> Unit,
    onAuthRequired: () -> Unit,
) {
    val deps: AppDeps = LocalAppDeps.current
    val api: MediaApiClient = deps.mediaApiClient
    val vm: VideosViewModel = viewModel(
        factory = viewModelFactory {
            initializer { VideosViewModel(subscriptionId, api) }
        },
    )
    val state: VideosViewModel.ListState = vm.state.collectAsState().value

    LaunchedEffect(Unit) { vm.load() }
    LaunchedEffect(state) {
        if (state is VideosViewModel.ListState.AuthRequired) onAuthRequired()
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TvTokens.backgroundColor)
            .padding(TvTokens.screenPadding),
    ) {
        val title: String = NavigationSelection.subscription?.name ?: "Videos"
        ScreenTitle(text = title)
        Spacer(Modifier.height(TvTokens.itemSpacing))
        when (state) {
            VideosViewModel.ListState.Loading -> LoadingView()

            is VideosViewModel.ListState.Error ->
                ErrorView(message = state.message, onRetry = vm::load)

            VideosViewModel.ListState.AuthRequired -> Unit

            is VideosViewModel.ListState.Ready ->
                if (state.videos.isEmpty()) {
                    EmptyView(message = "No videos")
                } else {
                    LazyColumn(
                        contentPadding = PaddingValues(bottom = TvTokens.screenPadding),
                        verticalArrangement = Arrangement.spacedBy(TvTokens.itemSpacing),
                    ) {
                        items(state.videos, key = { it.id }) { video ->
                            VideoRow(
                                name = video.name,
                                publishDate = video.publishDate,
                                thumbUrl = api.videoThumbnailUrl(video),
                                bearer = api.currentBearer(),
                                onClick = { onPlay(video) },
                            )
                        }
                    }
                }
        }
    }
}
