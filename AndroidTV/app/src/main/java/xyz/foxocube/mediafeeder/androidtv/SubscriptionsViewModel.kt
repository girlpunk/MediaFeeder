package xyz.foxocube.mediafeeder.androidtv

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/** Loads the subscription list; [AuthRequired] tells the UI to route back to settings. */
class SubscriptionsViewModel(
    private val api: MediaApiClient,
) : ViewModel() {

    /** UI state of the list: loading / ready / failure / re-auth. */
    sealed interface ListState {
        data object Loading : ListState

        data class Ready(val subscriptions: List<Subscription>) : ListState

        data class Error(val message: String) : ListState

        data object AuthRequired : ListState
    }

    private val _state: MutableStateFlow<ListState> = MutableStateFlow(ListState.Loading)
    val state: StateFlow<ListState> = _state.asStateFlow()

    fun load() {
        viewModelScope.launch {
            _state.value = ListState.Loading
            when (val result = api.getSubscriptions()) {
                is MediaApiClient.ApiOutcome.Success -> _state.value = ListState.Ready(result.value)
                is MediaApiClient.ApiOutcome.Failure -> _state.value = ListState.Error(result.message)
                MediaApiClient.ApiOutcome.AuthRequired -> _state.value = ListState.AuthRequired
            }
        }
    }
}
