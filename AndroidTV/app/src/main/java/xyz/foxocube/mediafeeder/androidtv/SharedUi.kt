package xyz.foxocube.mediafeeder.androidtv

import android.content.Context
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.calculateEndPadding
import androidx.compose.foundation.layout.calculateStartPadding
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import androidx.tv.material3.ClassicCard
import androidx.tv.material3.MaterialTheme
import coil.compose.AsyncImage
import coil.request.ImageRequest

///**
// * D-pad focusable card — the base unit of every interactive TV element on the subscription
// * and video screens. Focus = scale up + accent border; D-pad center press triggers [onClick].
// */
//@Composable
//fun FocusableCard(
//    onClick: () -> Unit,
//    modifier: Modifier = Modifier,
//    content: @Composable ColumnScope.() -> Unit,
//) {
//    var focused by remember { mutableStateOf(false) }
//    val animatedScale by animateFloatAsState(
//        targetValue = if (focused) TvTokens.focusScale else 1f,
//        animationSpec = tween(TvTokens.focusAnimationMs),
//        label = "FocusScale",
//    )
//    val animatedBorder by animateColorAsState(
//        targetValue = if (focused) TvTokens.accentColor else Color.Transparent,
//        animationSpec = tween(TvTokens.focusAnimationMs),
//        label = "FocusBorder",
//    )
//    Box(
//        modifier = modifier
//            .focusable()
//            .onFocusChanged { focused = it.isFocused }
//            .scale(animatedScale)
//            .background(MaterialTheme.colorScheme.surface, TvTokens.cardShape)
//            .border(TvTokens.focusBorderWidth, animatedBorder, TvTokens.cardShape)
//            .clickable(onClick = onClick),
//    ) {
//        content()
//    }
//}

/**
 * Subscription/video thumbnail: loads [url] with an optional bearer header and tolerates
 * any failure (401/412/404/network) by leaving the placeholder visible — no image.
 */
@Composable
fun ThumbImage(
    url: String?,
    bearer: String?,
    modifier: Modifier = Modifier,
) {
    val context: Context = LocalContext.current
//    Box(
//        modifier = modifier
//            .background(MaterialTheme.colorScheme.surfaceVariant, TvTokens.cardShape),
//        contentAlignment = Alignment.Center,
//    ) {
        if (url == null) {
            PlaceholderThumb()
        } else {
            AsyncImage(
                model = ImageRequest.Builder(context).apply {
                    data(url)
                    if (bearer != null) addHeader("Authorization", "Bearer $bearer")
                }.build(),
                contentDescription = "Thumbnail",
                modifier = modifier.fillMaxSize(),
                contentScale = ContentScale.Crop,
            )
        }
//    }
}

@Composable
private fun PlaceholderThumb() {
    Text(
        text = "?",
        style = MaterialTheme.typography.headlineSmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
}

/** Screen heading (the single typography spot for page titles). */
@Composable
fun ScreenTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.headlineSmall,
        modifier = modifier,
    )
}

/** Shown instead of a grid/list when the server has no items. */
@Composable
fun EmptyView(message: String, modifier: Modifier = Modifier) {
    Box(modifier = modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Text(
            text = message,
            style = MaterialTheme.typography.titleMedium,
        )
    }
}


/** One video row: 16:9 thumbnail plus name and publish date. */
@Composable
fun VideoRow(
    name: String,
    publishDate: String?,
    thumbUrl: String?,
    bearer: String?,
    onClick: () -> Unit,
) {
    ClassicCard(
        onClick = onClick,
        image = { ThumbImage(url = thumbUrl, bearer = bearer, modifier = Modifier.width(160.dp).aspectRatio(TvTokens.thumbAspectRatio)) },
        title = { Text(text = name, style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis) },
        subtitle = { if (publishDate != null) { Text(text = publishDate, style = MaterialTheme.typography.bodyMedium, maxLines = 1)}}
    )
}

val ParentPadding = PaddingValues(vertical = 16.dp, horizontal = 58.dp)

@Composable
fun rememberChildPadding(direction: LayoutDirection = LocalLayoutDirection.current): Padding {
    return remember {
        Padding(
            start = ParentPadding.calculateStartPadding(direction) + 8.dp,
            top = ParentPadding.calculateTopPadding(),
            end = ParentPadding.calculateEndPadding(direction) + 8.dp,
            bottom = ParentPadding.calculateBottomPadding()
        )
    }
}

@Immutable
data class Padding(
    val start: Dp,
    val top: Dp,
    val end: Dp,
    val bottom: Dp,
)
