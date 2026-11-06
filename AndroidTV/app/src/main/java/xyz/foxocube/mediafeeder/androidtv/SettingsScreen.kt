package xyz.foxocube.mediafeeder.androidtv

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.input.ImeAction
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.tv.material3.MaterialTheme
import androidx.tv.material3.WideButton

/**
 * Settings + sign-in screen (route: [NavRoutes.SETTINGS]).
 *
 * Idle/failed: the four server/OIDC fields plus a "Sign in" button running the OIDC
 * device-code flow. While the flow runs, a panel shows the user code to enter on a phone
 * (then the waiting/progress states until the server token is stored).
 */
@Composable
fun SettingsScreen(onSignedIn: () -> Unit) {
    val deps: AppDeps = LocalAppDeps.current
    val vm: SettingsViewModel = viewModel(
        factory = viewModelFactory {
            initializer { SettingsViewModel(deps.store) { deps.createDeviceAuthFlow() } }
        },
    )
    val state by vm.state.collectAsState()

    LaunchedEffect(state.login) {
        if (state.login is SettingsViewModel.LoginState.SignedIn) onSignedIn()
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TvTokens.backgroundColor)
            .padding(TvTokens.screenPadding),
    ) {
        if (isBusyLogin(state.login)) {
            LoginProgressPanel(state.login)
        } else {
            SettingsForm(vm = vm, login = state.login, onSignIn = vm::signIn)
        }
    }
}

private fun isBusyLogin(login: SettingsViewModel.LoginState): Boolean =
    login is SettingsViewModel.LoginState.RequestingUserCode ||
        login is SettingsViewModel.LoginState.AwaitingUser ||
        login is SettingsViewModel.LoginState.Exchanging

@Composable
private fun SettingsForm(
    vm: SettingsViewModel,
    login: SettingsViewModel.LoginState,
    onSignIn: () -> Unit,
) {
    val state: SettingsViewModel.State = vm.state.value
    val firstField: FocusRequester = remember { FocusRequester() }

    LaunchedEffect(login) {
        if (login is SettingsViewModel.LoginState.Idle) firstField.requestFocus()
    }

    ScreenTitle(text = "Settings")
    Spacer(Modifier.height(TvTokens.itemSpacing))
    SettingField(
        label = "Server base URL",
        value = state.baseUrl,
        onValueChange = vm::onBaseUrlChange,
        focusRequester = firstField,
    )
    Spacer(Modifier.height(TvTokens.itemSpacing))
    SettingField(
        label = "OIDC URL",
        value = state.oidcUrl,
        onValueChange = vm::onOidcUrlChange,
        focusRequester = null,
    )
    Spacer(Modifier.height(TvTokens.itemSpacing))
    SettingField(
        label = "Device client ID",
        value = state.deviceClientId,
        onValueChange = vm::onDeviceClientIdChange,
        focusRequester = null,
    )
    Spacer(Modifier.height(TvTokens.itemSpacing))
    SettingField(
        label = "Server client ID",
        value = state.serverClientId,
        onValueChange = vm::onServerClientIdChange,
        focusRequester = null,
    )
    Spacer(Modifier.height(TvTokens.itemSpacing))
    Row(horizontalArrangement = Arrangement.spacedBy(TvTokens.itemSpacing)) {
        WideButton(onClick = onSignIn) {
            Text(text = "Sign in")
        }
    }
    if (login is SettingsViewModel.LoginState.Failed) {
        Spacer(Modifier.height(TvTokens.itemSpacing))
        Text(
            text = login.message,
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.error,
        )
    }
}

@Composable
private fun SettingField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    focusRequester: FocusRequester?,
) {
    Column(Modifier.fillMaxWidth()) {
        Text(text = label, style = MaterialTheme.typography.titleSmall)
        OutlinedTextField(
            value = value,
            onValueChange = onValueChange,
            singleLine = true,
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
            modifier = Modifier
                .fillMaxWidth()
                .let { m -> if (focusRequester != null) m.focusRequester(focusRequester) else m },
        )
    }
}

/** Device-code flow progress: the user code to enter on a phone, then the wait states. */
@Composable
private fun LoginProgressPanel(login: SettingsViewModel.LoginState) {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            when (login) {
                SettingsViewModel.LoginState.RequestingUserCode -> Unit

                is SettingsViewModel.LoginState.AwaitingUser -> {
                    Text(
                        text = "Enter this code on your phone: ${login.userCode}",
                        style = MaterialTheme.typography.headlineMedium,
                    )
                    Spacer(Modifier.height(TvTokens.itemSpacing))
                    Text(
                        text = login.verificationUri,
                        style = MaterialTheme.typography.titleMedium,
                    )
                }

                SettingsViewModel.LoginState.Exchanging -> Unit

                else -> Unit
            }
            Spacer(Modifier.height(TvTokens.itemSpacing))
            CircularProgressIndicator(color = TvTokens.accentColor)
            Spacer(Modifier.height(TvTokens.itemSpacing))
            val label: String = when (login) {
                SettingsViewModel.LoginState.RequestingUserCode -> "Requesting device code…"
                is SettingsViewModel.LoginState.AwaitingUser -> "Waiting for approval…"
                SettingsViewModel.LoginState.Exchanging -> "Signing in…"
                else -> ""
            }
            Text(text = label, style = MaterialTheme.typography.titleMedium)
        }
    }
}
