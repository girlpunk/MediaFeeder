package xyz.foxocube.mediafeeder.androidtv.presentation.common

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.TextStyle
import androidx.tv.material3.MaterialTheme
import androidx.tv.material3.Text
import xyz.foxocube.mediafeeder.androidtv.TvTokens

/** Centered indeterminate progress (loading states, device-code polling). */
@Composable
fun Loading(
    modifier: Modifier = Modifier,
    style: TextStyle = MaterialTheme.typography.displayMedium,
    label: String? = null,
) {
    Box(modifier = modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            CircularProgressIndicator(color = TvTokens.accentColor)
            if (label != null) {
                Spacer(Modifier.height(TvTokens.itemSpacing))
                Text(
                    text = label,
                    style = style,
                )
            }
        }
    }
}
