package xyz.foxocube.mediafeeder.androidtv.presentation.common

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.tv.material3.MaterialTheme
import androidx.tv.material3.Text
import androidx.tv.material3.WideButton
import xyz.foxocube.mediafeeder.androidtv.TvTokens

@Composable
fun Error(
    message: String? = null,
    modifier: Modifier = Modifier,
    onRetry: (() -> Unit)? = null,
) {
    Box(
        modifier = modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text(
                text = "An error occurred", //stringResource(id = R.string.message_error),
                modifier = modifier
            )
            if(message != null) {
                Text(
                    text = message,
                    style = MaterialTheme.typography.titleMedium,
                )
            }
            if(onRetry != null) {
                Spacer(Modifier.height(TvTokens.itemSpacing))
                WideButton(onClick = onRetry) {
                    Text(text = "Retry")
                }
            }
        }
    }
}
