package xyz.foxocube.mediafeeder.androidtv

import kotlinx.serialization.builtins.ListSerializer
import kotlinx.serialization.json.Json

/**
 * Parses MediaFeeder REST JSON payloads into [Subscription] / [Video] models.
 *
 * Uses kotlinx.serialization with `ignoreUnknownKeys = true` so full server payloads with
 * extra fields also parse; absent optional fields map to null.
 */
object DtoParser {
    private val format: Json = Json { ignoreUnknownKeys = true }

    fun parseSubscription(json: String): Subscription = format.decodeFromString(Subscription.serializer(), json)

    fun parseSubscriptions(json: String): List<Subscription> =
        format.decodeFromString(ListSerializer(Subscription.serializer()), json)

    fun parseVideo(json: String): Video = format.decodeFromString(Video.serializer(), json)

    fun parseVideos(json: String): List<Video> =
        format.decodeFromString(ListSerializer(Video.serializer()), json)
}
