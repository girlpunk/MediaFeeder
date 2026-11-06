package xyz.foxocube.mediafeeder.androidtv

/**
 * Payloads carried between screens by the argument-free routes (subscriptions/videos/player)
 * — a [Subscription]/[Video] does not fit in a URI. The source screen sets the field right
 * before navigating; the destination screen reads it on entry.
 */
object NavigationSelection {
    var subscription: Subscription? = null

    var video: Video? = null
}
