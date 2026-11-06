package xyz.foxocube.mediafeeder.androidtv

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.colorResource
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.tv.material3.MaterialTheme
import androidx.tv.material3.darkColorScheme

/**
 * Design tokens of the MediaFeeder TV UI — the single source of truth for visual values.
 * Brand colors live in res/values/colors.xml; spacing/scale/shape are defined here.
 */
object TvTokens {
    val backgroundColor: Color
        @Composable get() = colorResource(id = R.color.background_dark)

    val accentColor: Color
        @Composable get() = colorResource(id = R.color.accent_blue)

    /** Scale factor applied to a card while it has D-pad focus. */
    val focusScale: Float = 1.04f

    val focusAnimationMs: Int = 120

    val focusBorderWidth: Dp = 2.dp

    val cornerRadius: Dp = 12.dp

    val cardShape: RoundedCornerShape = RoundedCornerShape(cornerRadius)

    val screenPadding: Dp = 32.dp

    val itemSpacing: Dp = 16.dp

    /** Minimum width of one grid card; the grid derives the column count from it. */
    val gridCardMinWidth: Dp = 280.dp

    /** 16:9 aspect ratio for all subscription/video thumbnails. */
    val thumbAspectRatio: Float = 16f / 9f
}

/** App-wide theme: TV Material 3 dark scheme over the MediaFeeder brand background. */
@Composable
fun MediaFeederTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = darkColorScheme(),
        content = content,
    )
}
