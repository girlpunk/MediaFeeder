package xyz.foxocube.mediafeeder.androidtv

import android.graphics.drawable.GradientDrawable
import android.text.Editable
import android.text.InputType
import android.text.TextWatcher
import android.util.TypedValue
import android.view.inputmethod.EditorInfo
import android.widget.EditText
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.viewinterop.AndroidView
import androidx.tv.material3.MaterialTheme

/**
 * D-pad-native text field for Android TV: a [label] over a real [EditText] embedded via
 * [AndroidView].
 *
 * Compose's `value`/`onValueChange` TextFields do not reliably establish the IME editor
 * connection on Android TV: the on-screen keyboard is shown but key presses are never
 * committed to the field. A native EditText participates in the platform's D-pad focus and
 * IME flow directly (keyboard auto-show on focus, D-pad input, Back closes the keyboard),
 * which is reliable across TV vendors — the same approach the Jellyfin TV app uses.
 */
@Composable
fun TvTextField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    modifier: Modifier = Modifier,
    requestFocusOnInit: Boolean = false,
) {
    val density: Float = LocalDensity.current.density
    val cornerPx: Float = TvTokens.cornerRadius.value * density
    val padPx: Int = (12f * density).toInt()
    val thinStrokePx: Int = (1f * density).toInt()
    val focusStrokePx: Int = (TvTokens.focusBorderWidth.value * density).toInt()
    val fieldFill: Int = MaterialTheme.colorScheme.surface.toArgb()
    val strokeDefault: Int = MaterialTheme.colorScheme.border.toArgb()
    val strokeFocused: Int = TvTokens.accentColor.toArgb()
    val textColor: Int = MaterialTheme.colorScheme.onSurface.toArgb()
    val unfocusedBackground: GradientDrawable = GradientDrawable().apply {
        shape = GradientDrawable.RECTANGLE
        cornerRadius = cornerPx
        setColor(fieldFill)
        setStroke(thinStrokePx, strokeDefault)
    }
    val focusedBackground: GradientDrawable = GradientDrawable().apply {
        shape = GradientDrawable.RECTANGLE
        cornerRadius = cornerPx
        setColor(fieldFill)
        setStroke(focusStrokePx, strokeFocused)
    }

    Column(modifier = modifier.fillMaxWidth()) {
        Text(text = label, style = MaterialTheme.typography.titleSmall)
        AndroidView(
            factory = { context ->
                EditText(context).apply {
                    isFocusable = true
                    isFocusableInTouchMode = true
                    inputType = InputType.TYPE_CLASS_TEXT
                    maxLines = 1
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 16f)
                    setTextColor(textColor)
                    setMinHeight((56f * density).toInt())
                    setPadding(padPx, padPx, padPx, padPx)
                    background = unfocusedBackground
                    imeOptions = EditorInfo.IME_ACTION_DONE
                    addTextChangedListener(object : TextWatcher {
                        override fun afterTextChanged(s: Editable) {
                            onValueChange(s.toString())
                        }

                        override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) = Unit

                        override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) = Unit
                    })
                    setOnFocusChangeListener { view, hasFocus ->
                        view.background = if (hasFocus) focusedBackground else unfocusedBackground
                    }
                    setOnEditorActionListener { view, action, _ ->
                        if (action == EditorInfo.IME_ACTION_DONE) {
                            view.clearFocus()
                            true
                        } else {
                            false
                        }
                    }
                    if (requestFocusOnInit) post { requestFocus() }
                }
            },
            update = { view -> if (view.text.toString() != value) view.setText(value) },
            modifier = Modifier.fillMaxWidth(),
        )
    }
}
