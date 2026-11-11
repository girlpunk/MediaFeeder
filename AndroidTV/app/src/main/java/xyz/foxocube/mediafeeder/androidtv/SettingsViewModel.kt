package xyz.foxocube.mediafeeder.androidtv

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * Settings + sign-in screen state: the four OIDC/server fields plus the device-code login
 * phase. The whole sign-in sequence (device code -> poll loop -> server token exchange)
 * runs in one coroutine on [signIn].
 */
class SettingsViewModel(
    private val store: AuthStore,
    private val flowFactory: suspend () -> DeviceAuthOutcome<DeviceAuthFlow>,
) : ViewModel() {

    /** Sign-in phase shown below the settings form. */
    sealed interface LoginState {
        data object Idle : LoginState

        data object RequestingUserCode : LoginState

        data class AwaitingUser(val userCode: String, val verificationUri: String) : LoginState

        data object Exchanging : LoginState

        data class Failed(val message: String) : LoginState

        data object SignedIn : LoginState
    }

    data class State(
        val baseUrl: String,
        val oidcUrl: String,
        val deviceClientId: String,
        val serverClientId: String,
        val login: LoginState,
    )

    private val isBusy: (LoginState) -> Boolean = {
        it is LoginState.RequestingUserCode || it is LoginState.AwaitingUser || it is LoginState.Exchanging
    }

    private val _state: MutableStateFlow<State> = MutableStateFlow(
        State(
            baseUrl = store.baseUrl.orEmpty(),
            oidcUrl = store.oidcUrl.orEmpty(),
            deviceClientId = store.deviceClientId.orEmpty(),
            serverClientId = store.serverClientId.orEmpty(),
            login = LoginState.Idle,
        ),
    )
    val state: StateFlow<State> = _state.asStateFlow()

    fun onBaseUrlChange(value: String) {
        _state.update { it.copy(baseUrl = value) }
    }

    fun onOidcUrlChange(value: String) {
        _state.update { it.copy(oidcUrl = value) }
    }

    fun onDeviceClientIdChange(value: String) {
        _state.update { it.copy(deviceClientId = value) }
    }

    fun onServerClientIdChange(value: String) {
        _state.update { it.copy(serverClientId = value) }
    }

    /** Persist the form, then run the device-code flow end to end. */
    fun signIn() {
        val form: State = _state.value
        if (isBusy(form.login)) return
        val incomplete: Boolean = form.baseUrl.isBlank() ||
            form.oidcUrl.isBlank() ||
            form.deviceClientId.isBlank() ||
            form.serverClientId.isBlank()
        if (incomplete) return
        store.baseUrl = form.baseUrl.trim()
        store.oidcUrl = form.oidcUrl.trim()
        store.deviceClientId = form.deviceClientId.trim()
        store.serverClientId = form.serverClientId.trim()
        viewModelScope.launch { runLogin() }
    }

    private suspend fun runLogin() {
        val flow: DeviceAuthFlow = when (val outcome: DeviceAuthOutcome<DeviceAuthFlow> = flowFactory()) {
            is DeviceAuthOutcome.Failure -> {
                _state.update { it.copy(login = LoginState.Failed(outcome.message)) }
                return
            }

            is DeviceAuthOutcome.Success -> outcome.value
        }
        _state.update { it.copy(login = LoginState.RequestingUserCode) }
        when (val challenge = flow.requestUserCode()) {
            is DeviceAuthOutcome.Failure -> {
                _state.update { it.copy(login = LoginState.Failed(challenge.message)) }
                return
            }

            is DeviceAuthOutcome.Success -> {
                val info: DeviceCodeInfo = challenge.value
                _state.update { it.copy(login = LoginState.AwaitingUser(info.userCode, info.verificationUri)) }
                when (val poll = pollUntilSettled(flow, info)) {
                    is PollResult.Pending -> Unit

                    is PollResult.Error -> {
                        _state.update { it.copy(login = LoginState.Failed("Sign-in failed: ${poll.code}")) }
                        return
                    }

                    is PollResult.Success -> {
                        _state.update { it.copy(login = LoginState.Exchanging) }
                        when (val exchange = flow.completeLogin(poll.token)) {
                            is DeviceAuthOutcome.Success ->
                                _state.update { it.copy(login = LoginState.SignedIn) }
                            is DeviceAuthOutcome.Failure ->
                                _state.update { it.copy(login = LoginState.Failed(exchange.message)) }
                        }
                    }
                }
            }
        }
    }

    private suspend fun pollUntilSettled(flow: DeviceAuthFlow, info: DeviceCodeInfo): PollResult {
        while (true) {
            val result: PollResult = flow.pollOnce(info.deviceCode)
            if (result !is PollResult.Pending) return result
            delay(info.intervalSeconds.coerceAtLeast(1L) * 1000L)
        }
    }
}
