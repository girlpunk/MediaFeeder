package xyz.foxocube.mediafeeder.androidtv

/** Navigation routes of the app: settings -> subscriptions -> videos -> player. */
object NavRoutes {
    const val SETTINGS: String = "settings"
    const val SUBSCRIPTIONS: String = "subscriptions"
    const val VIDEOS: String = "videos/{subscriptionId}"
    const val PLAYER: String = "player"

    const val VIDEOS_SUBSCRIPTION_ID: String = "subscriptionId"

    fun videos(subscriptionId: Int): String = "videos/$subscriptionId"
}
