package xyz.foxocube.mediafeeder.androidtv

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.ui.Modifier
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory

/**
 * Subscriptions screen (route: [NavRoutes.SUBSCRIPTIONS]): a D-pad grid of subscription
 * cards. Bearer 401/403 or a lapsed token re-authenticates via [onAuthRequired] (-> settings).
 */
@Composable
fun SubscriptionsScreen(
    onOpenSubscription: (Subscription) -> Unit,
    onAuthRequired: () -> Unit,
) {
    val deps: AppDeps = LocalAppDeps.current
    val api: MediaApiClient = deps.mediaApiClient
    val vm: SubscriptionsViewModel = viewModel(
        factory = viewModelFactory {
            initializer { SubscriptionsViewModel(api) }
        },
    )
    val state: SubscriptionsViewModel.ListState = vm.state.collectAsState().value

    LaunchedEffect(Unit) { vm.load() }
    LaunchedEffect(state) {
        if (state is SubscriptionsViewModel.ListState.AuthRequired) onAuthRequired()
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TvTokens.backgroundColor)
            .padding(TvTokens.screenPadding),
    ) {
        ScreenTitle(text = "Subscriptions")
        Spacer(Modifier.height(TvTokens.itemSpacing))
        when (state) {
            SubscriptionsViewModel.ListState.Loading -> LoadingView()

            is SubscriptionsViewModel.ListState.Error ->
                ErrorView(message = state.message, onRetry = vm::load)

            is SubscriptionsViewModel.ListState.AuthRequired -> Unit

            is SubscriptionsViewModel.ListState.Ready ->
                if (state.subscriptions.isEmpty()) {
                    EmptyView(message = "No subscriptions")
                } else {
                    LazyVerticalGrid(
                        columns = GridCells.Adaptive(minSize = TvTokens.gridCardMinWidth),
                        contentPadding = PaddingValues(bottom = TvTokens.screenPadding),
                        horizontalArrangement = Arrangement.spacedBy(TvTokens.itemSpacing),
                        verticalArrangement = Arrangement.spacedBy(TvTokens.itemSpacing),
                    ) {
                        items(state.subscriptions, key = { it.id }) { subscription ->
                            SubscriptionCard(
                                name = subscription.name,
                                thumbUrl = api.subscriptionThumbnailUrl(subscription),
                                bearer = api.currentBearer(),
                                onClick = { onOpenSubscription(subscription) },
                            )
                        }
                    }
                }
        }
    }
}
