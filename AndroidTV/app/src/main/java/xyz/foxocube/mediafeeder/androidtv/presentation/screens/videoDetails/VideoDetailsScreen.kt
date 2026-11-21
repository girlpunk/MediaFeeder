package xyz.foxocube.mediafeeder.androidtv.presentation.screens.videoDetails

import androidx.activity.compose.BackHandler
import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import xyz.foxocube.mediafeeder.androidtv.data.entities.VideoDetails
import xyz.foxocube.mediafeeder.androidtv.presentation.common.Error
import xyz.foxocube.mediafeeder.androidtv.presentation.common.Loading
import xyz.foxocube.mediafeeder.androidtv.rememberChildPadding

object VideoDetailsScreen {
    const val VideoIdBundleKey = "videoId"
}

@Composable
fun VideoDetailsScreen(
    goToVideoPlayer: () -> Unit,
    onBackPressed: () -> Unit,
    videoDetailsScreenViewModel: VideoDetailsScreenViewModel = hiltViewModel()
) {
    val uiState by videoDetailsScreenViewModel.uiState.collectAsStateWithLifecycle()

    when (val s = uiState) {
        is VideoDetailsScreenUiState.Loading -> {
            Loading(modifier = Modifier.fillMaxSize())
        }

        is VideoDetailsScreenUiState.Error -> {
            Error(modifier = Modifier.fillMaxSize())
        }

        is VideoDetailsScreenUiState.Done -> {
            Details(
                videoDetails = s.videoDetails,
                goToVideoPlayer = goToVideoPlayer,
                onBackPressed = onBackPressed,
                modifier = Modifier
                    .fillMaxSize()
                    .animateContentSize()
            )
        }
    }
}

@Composable
private fun Details(
    videoDetails: VideoDetails,
    goToVideoPlayer: () -> Unit,
    onBackPressed: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val childPadding = rememberChildPadding()

    BackHandler(onBack = onBackPressed)
    LazyColumn(
        contentPadding = PaddingValues(bottom = 135.dp),
        modifier = modifier,
    ) {
        item {
            VideoDetails(
                videoDetails = videoDetails,
                goToVideoPlayer = goToVideoPlayer
            )
        }

        item {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = childPadding.start),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                val itemModifier = Modifier.width(192.dp)

                TitleValueText(
                    modifier = itemModifier,
                    title = "Status", // stringResource(R.string.status),
                    value = videoDetails.status
                )
                TitleValueText(
                    modifier = itemModifier,
                    title = "Original Language", //stringResource(R.string.original_language),
                    value = videoDetails.originalLanguage
                )
                TitleValueText(
                    modifier = itemModifier,
                    title = "Budget", //stringResource(R.string.budget),
                    value = videoDetails.budget
                )
                TitleValueText(
                    modifier = itemModifier,
                    title = "Revenue", //stringResource(R.string.revenue),
                    value = videoDetails.revenue
                )
            }
        }
    }
}
