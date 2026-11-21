package xyz.foxocube.mediafeeder.androidtv

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.layout.LazyLayoutCacheWindow
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.tv.material3.Border
import androidx.tv.material3.CardDefaults
import androidx.tv.material3.LocalContentColor
import androidx.tv.material3.MaterialTheme
import androidx.tv.material3.ShapeDefaults
import androidx.tv.material3.Text
import androidx.tv.material3.WideClassicCard
import xyz.foxocube.mediafeeder.androidtv.data.entities.Subscription
import xyz.foxocube.mediafeeder.androidtv.presentation.common.Error
import xyz.foxocube.mediafeeder.androidtv.presentation.common.Loading

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

    CompositionLocalProvider(LocalContentColor provides TvTokens.onBackgroundColor) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(TvTokens.backgroundColor)
                .padding(TvTokens.screenPadding),
        ) {
            ScreenTitle(text = "Subscriptions")
            Spacer(Modifier.height(TvTokens.itemSpacing))
            when (state) {
                SubscriptionsViewModel.ListState.Loading -> Loading()

                is SubscriptionsViewModel.ListState.Error ->
                    Error(message = state.message, onRetry = vm::load)

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
                            cacheWindow = LazyLayoutCacheWindow(0.5f, 0.2f)
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
}

/** One grid card: 16:9 thumbnail over the subscription name. */
@Composable
fun SubscriptionCard(
    name: String,
    thumbUrl: String?,
    bearer: String?,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    WideClassicCard(
        modifier = modifier.height(50.dp),
        shape = CardDefaults.shape(shape = ShapeDefaults.ExtraSmall),
        scale = CardDefaults.scale(focusedScale = 1f),
        border = CardDefaults.border(
            focusedBorder = Border(
                border = BorderStroke(
                    width = 3.dp,
                    color = MaterialTheme.colorScheme.onSurface
                ),
                shape = ShapeDefaults.ExtraSmall
            )
        ),
        title = { Text(
            text = name,
            maxLines = 1,
            style = MaterialTheme.typography.titleSmall,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.padding(8.dp, 8.dp)
        ) },
        onClick = onClick,
        image = { ThumbImage(
            url = thumbUrl,
            bearer = bearer,
            modifier = Modifier.aspectRatio(1f).fillMaxHeight()
        )},
    )
}
