package xyz.foxocube.mediafeeder.androidtv

import android.content.Context
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.tv.material3.MaterialTheme
import androidx.tv.material3.WideButton
import coil.compose.AsyncImage
import coil.request.ImageRequest

/**
 * D-pad focusable card — the base unit of every interactive TV element on the subscription
 * and video screens. Focus = scale up + accent border; D-pad center press triggers [onClick].
 */
@Composable
fun FocusableCard(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    content: @Composable BoxScope.() -> Unit,
) {
    var focused by remember { mutableStateOf(false) }
    val animatedScale by animateFloatAsState(
        targetValue = if (focused) TvTokens.focusScale else 1f,
        animationSpec = tween(TvTokens.focusAnimationMs),
        label = "FocusScale",
    )
    val animatedBorder by animateColorAsState(
        targetValue = if (focused) TvTokens.accentColor else Color.Transparent,
        animationSpec = tween(TvTokens.focusAnimationMs),
        label = "FocusBorder",
    )
    Box(
        modifier = modifier
            .focusable()
            .onFocusChanged { focused = it.isFocused }
            .scale(animatedScale)
            .background(MaterialTheme.colorScheme.surface, TvTokens.cardShape)
            .border(TvTokens.focusBorderWidth, animatedBorder, TvTokens.cardShape)
            .clickable(onClick = onClick),
    ) {
        content()
    }
}

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
    Box(
        modifier = modifier
            .background(MaterialTheme.colorScheme.surfaceVariant, TvTokens.cardShape),
        contentAlignment = Alignment.Center,
    ) {
        if (url == null) {
            PlaceholderThumb()
        } else {
            AsyncImage(
                model = ImageRequest.Builder(context).apply {
                    data(url)
                    if (bearer != null) addHeader("Authorization", "Bearer $bearer")
                }.build(),
                contentDescription = null,
                modifier = Modifier.fillMaxSize(),
                contentScale = ContentScale.Crop,
            )
        }
    }
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

/** Centered indeterminate progress (loading states, device-code polling). */
@Composable
fun LoadingView(modifier: Modifier = Modifier, label: String? = null) {
    Box(modifier = modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            CircularProgressIndicator(color = TvTokens.accentColor)
            if (label != null) {
                Spacer(Modifier.height(TvTokens.itemSpacing))
                Text(
                    text = label,
                    style = MaterialTheme.typography.titleMedium,
                )
            }
        }
    }
}

/** Error state with a retry action. */
@Composable
fun ErrorView(message: String, onRetry: () -> Unit, modifier: Modifier = Modifier) {
    Box(modifier = modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text(
                text = message,
                style = MaterialTheme.typography.titleMedium,
            )
            Spacer(Modifier.height(TvTokens.itemSpacing))
            WideButton(onClick = onRetry) {
                Text(text = "Retry")
            }
        }
    }
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

/** One grid card: 16:9 thumbnail over the subscription name. */
@Composable
fun SubscriptionCard(
    name: String,
    thumbUrl: String?,
    bearer: String?,
    onClick: () -> Unit,
) {
    FocusableCard(onClick = onClick) {
        Column {
            ThumbImage(
                url = thumbUrl,
                bearer = bearer,
                modifier = Modifier
                    .fillMaxWidth()
                    .aspectRatio(TvTokens.thumbAspectRatio),
            )
            Text(
                text = name,
                style = MaterialTheme.typography.titleMedium,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.padding(12.dp),
            )
        }
    }
}

/** One video row: 16:9 thumbnail plus name and publish date. */
@Composable
fun VideoRow(
    name: String,
    publishedDate: String?,
    thumbUrl: String?,
    bearer: String?,
    onClick: () -> Unit,
) {
    FocusableCard(onClick = onClick) {
        Row {
            ThumbImage(
                url = thumbUrl,
                bearer = bearer,
                modifier = Modifier
                    .width(160.dp)
                    .aspectRatio(TvTokens.thumbAspectRatio),
            )
            Spacer(Modifier.width(TvTokens.itemSpacing))
            Column(
                verticalArrangement = Arrangement.Center,
                modifier = Modifier
                    .weight(1f)
                    .padding(vertical = 8.dp),
            ) {
                Text(
                    text = name,
                    style = MaterialTheme.typography.titleMedium,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                if (publishedDate != null) {
                    Text(
                        text = publishedDate,
                        style = MaterialTheme.typography.bodyMedium,
                        maxLines = 1,
                    )
                }
            }
        }
    }
}
