package xyz.foxocube.mediafeeder.androidtv

/** Navigation routes of the app: settings -> subscriptions -> videos -> player. */
object NavRoutes {
    const val SETTINGS: String = "settings"
    const val SUBSCRIPTIONS: String = "subscriptions"
    const val VIDEOS: String = "videos/{subscriptionId}"
    const val PLAYER: String = "player"
    const val VIDEO_DETAIL: String = "video/{videoId}"

    const val VIDEOS_SUBSCRIPTION_ID: String = "subscriptionId"
    const val VIDEO_ID: String = "videoId"

    fun videos(subscriptionId: Int): String = "videos/$subscriptionId"
    fun videoDetail(videoId: Int): String = "video/$videoId"
}
