package xyz.foxocube.mediafeeder.androidtv

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.ComposeView
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Single-activity entry point: runs the one-shot startup auth check (valid stored server
 * token, or one refresh attempt) and then starts the NavHost on the matching route.
 */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val deps: AppDeps = AppDeps(applicationContext)
        val composeView = ComposeView(this)
        composeView.setContent {
            MediaFeederTheme {
                CompositionLocalProvider(LocalAppDeps.provides(deps)) {
                    StartupAuthGate(deps = deps) { startDestination ->
                        AppNavHost(startDestination = startDestination)
                    }
                }
            }
        }
        setContentView(composeView)
    }
}

/** Shows a splash while the startup auth check runs, then the NavHost on the result route. */
@Composable
private fun StartupAuthGate(
    deps: AppDeps,
    onReady: @Composable (String) -> Unit,
) {
    var startDestination by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(Unit) {
        val authenticated: Boolean = withContext(Dispatchers.IO) { deps.startupAuth() }
        startDestination = if (authenticated) NavRoutes.SUBSCRIPTIONS else NavRoutes.SETTINGS
    }

    val destination: String? = startDestination
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(TvTokens.backgroundColor),
        contentAlignment = Alignment.Center,
    ) {
        if (destination == null) {
            CircularProgressIndicator(color = TvTokens.accentColor)
        } else {
            onReady(destination)
        }
    }
}

/** Routes: settings -> subscriptions -> videos -> player (back pops; re-auth goes to settings). */
@Composable
private fun AppNavHost(startDestination: String) {
    val navController = rememberNavController()

    NavHost(
        navController = navController,
        startDestination = startDestination,
    ) {
        composable(NavRoutes.SETTINGS) {
            SettingsScreen(
                onSignedIn = {
                    navController.navigate(NavRoutes.SUBSCRIPTIONS)
                },
            )
        }
        composable(NavRoutes.SUBSCRIPTIONS) {
            SubscriptionsScreen(
                onOpenSubscription = { subscription ->
                    NavigationSelection.subscription = subscription
                    navController.navigate(NavRoutes.videos(subscription.id))
                },
                onAuthRequired = {
                    navController.navigate(NavRoutes.SETTINGS)
                },
            )
        }
        composable(
            route = NavRoutes.VIDEOS,
            arguments = listOf(navArgument(NavRoutes.VIDEOS_SUBSCRIPTION_ID) { type = NavType.IntType }),
        ) { entry ->
            val subscriptionId: Int = entry.arguments?.getInt(NavRoutes.VIDEOS_SUBSCRIPTION_ID) ?: 0
            VideosScreen(
                subscriptionId = subscriptionId,
                onPlay = { video ->
                    NavigationSelection.video = video
                    navController.navigate(NavRoutes.PLAYER)
                },
                onAuthRequired = {
                    navController.navigate(NavRoutes.SETTINGS)
                },
            )
        }
        composable(NavRoutes.PLAYER) {
            PlayerScreen(
                onBack = {
                    navController.popBackStack()
                },
            )
        }
    }
}
