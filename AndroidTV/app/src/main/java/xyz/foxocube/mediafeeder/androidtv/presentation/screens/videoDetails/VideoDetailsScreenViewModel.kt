package xyz.foxocube.mediafeeder.androidtv.presentation.screens.videoDetails

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import xyz.foxocube.mediafeeder.androidtv.data.entities.VideoDetails
import dagger.hilt.android.lifecycle.HiltViewModel
import jakarta.inject.Inject
import xyz.foxocube.mediafeeder.androidtv.data.repositories.VideoRepository

@HiltViewModel
class VideoDetailsScreenViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    repository: VideoRepository,
) : ViewModel() {
    val uiState = savedStateHandle
        .getStateFlow<String?>(VideoDetailsScreen.VideoIdBundleKey, null)
        .map { id ->
            if (id == null) {
                VideoDetailsScreenUiState.Error
            } else {
                val details = repository.getVideoDetails(movieId = id)
                VideoDetailsScreenUiState.Done(videoDetails = details)
            }
        }.stateIn(
            scope = viewModelScope,
            started = SharingStarted.WhileSubscribed(5_000),
            initialValue = VideoDetailsScreenUiState.Loading
        )
}

sealed class VideoDetailsScreenUiState {
    data object Loading : VideoDetailsScreenUiState()
    data object Error : VideoDetailsScreenUiState()
    data class Done(val videoDetails: VideoDetails) : VideoDetailsScreenUiState()
}
