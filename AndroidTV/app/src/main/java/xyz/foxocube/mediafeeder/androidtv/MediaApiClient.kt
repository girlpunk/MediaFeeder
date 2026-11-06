package xyz.foxocube.mediafeeder.androidtv

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response

/**
 * REST client for the MediaFeeder server API (builds on [DtoParser]).
 *
 * Every GET carries `Authorization: Bearer <server token>`; a missing/expired token
 * triggers one [TokenManager.refresh] attempt, and 401/403 responses surface as
 * [ApiOutcome.AuthRequired] so the UI can route to settings.
 */
class MediaApiClient(
    private val store: AuthStore,
    private val tokenManager: TokenManager,
    private val httpClient: OkHttpClient,
) {
    /** Outcome of an API call: [Success], a terminal [Failure], or [AuthRequired]. */
    sealed interface ApiOutcome<out T> {
        data class Success<T>(val value: T) : ApiOutcome<T>

        data class Failure(val message: String) : ApiOutcome<Nothing>

        data object AuthRequired : ApiOutcome<Nothing>
    }

    /** GET /api/Subscriptions. */
    suspend fun getSubscriptions(): ApiOutcome<List<Subscription>> {
        val base: String? = baseUrl()
        if (base == null) return ApiOutcome.Failure(FAILURE_NO_SERVER)
        return get("$base/api/Subscriptions", DtoParser::parseSubscriptions)
    }

    /** GET /api/Subscriptions/{id}/videos. */
    suspend fun getVideos(subscriptionId: Int): ApiOutcome<List<Video>> {
        val base: String? = baseUrl()
        if (base == null) return ApiOutcome.Failure(FAILURE_NO_SERVER)
        return get("$base/api/Subscriptions/$subscriptionId/videos", DtoParser::parseVideos)
    }

    /** Thumb endpoint URL for a subscription, or null when the subscription has no thumb. */
    fun subscriptionThumbnailUrl(subscription: Subscription): String? {
        val base: String? = baseUrl() ?: return null
        if (subscription.thumb == null) return null
        return "$base/api/Subscription/${subscription.id}/thumbnail"
    }

    /** Thumb endpoint URL for a video, or null when the video has no thumb. */
    fun videoThumbnailUrl(video: Video): String? {
        val base: String? = baseUrl() ?: return null
        if (video.thumb == null) return null
        return "$base/api/Video/${video.id}/thumbnail"
    }

    /** Current valid server token for authenticated image requests (no refresh attempt). */
    fun currentBearer(): String? = tokenManager.getValidToken()

    private fun baseUrl(): String? = store.baseUrl?.takeIf { it.isNotBlank() }

    private suspend fun <T> get(url: String, parse: (String) -> T): ApiOutcome<T> {
        val bearer: String? = validBearer()
        if (bearer == null) return ApiOutcome.AuthRequired
        val request: Request = Request.Builder().url(url).header("Authorization", "Bearer $bearer").build()
        return withContext(Dispatchers.IO) {
            try {
                httpClient.newCall(request).execute().use { response ->
                    when {
                        response.code == HTTP_UNAUTHORIZED || response.code == HTTP_FORBIDDEN ->
                            ApiOutcome.AuthRequired
                        response.isSuccessful -> readSuccess(response, parse)
                        else -> ApiOutcome.Failure("Server replied with HTTP ${response.code}")
                    }
                }
            } catch (e: Exception) {
                ApiOutcome.Failure("Could not reach the server: ${e.message}")
            }
        }
    }

    private fun <T> readSuccess(response: Response, parse: (String) -> T): ApiOutcome<T> {
        val body: String = response.body?.string() ?: ""
        return try {
            ApiOutcome.Success(parse(body))
        } catch (e: Exception) {
            ApiOutcome.Failure("Could not read the server response: ${e.message}")
        }
    }

    private suspend fun validBearer(): String? =
        tokenManager.getValidToken() ?: tokenManager.refresh().let { if (it) tokenManager.getValidToken() else null }

    private companion object {
        const val FAILURE_NO_SERVER: String = "No server configured"
        const val HTTP_UNAUTHORIZED: Int = 401
        const val HTTP_FORBIDDEN: Int = 403
    }
}
