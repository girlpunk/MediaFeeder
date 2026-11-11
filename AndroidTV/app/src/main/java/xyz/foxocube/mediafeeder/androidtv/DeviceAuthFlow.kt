package xyz.foxocube.mediafeeder.androidtv

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import okhttp3.OkHttpClient
import okhttp3.Request

/** The device-code challenge to relay to the user on their phone. */
data class DeviceCodeInfo(
    val deviceCode: String,
    val userCode: String,
    val verificationUri: String,
    val intervalSeconds: Long,
    val expiresInSeconds: Long? = null,
)

/** Outcome of one device-code flow step. */
sealed interface DeviceAuthOutcome<out T> {
    data class Success<T>(val value: T) : DeviceAuthOutcome<T>

    data class Failure(val message: String) : DeviceAuthOutcome<Nothing>
}

/**
 * OIDC device-code login flow (mirrors scripts/auth.py), built on the request builders of
 * [AuthClient] and a real [OkHttpClient].
 *
 * Steps: [requestUserCode] -> [pollUntilSettled] until the user approves -> [completeLogin]
 * exchanges the device access token for the server token and persists both token pairs.
 */
class DeviceAuthFlow(
    private val authClient: AuthClient,
    private val httpClient: OkHttpClient,
    private val store: AuthStore,
    private val clock: () -> Long = { System.currentTimeMillis() },
    private val delayMillis: suspend (Long) -> Unit = ::delay,
) {
    private val json: Json = Json { ignoreUnknownKeys = true }

    /** Request the device-code challenge (device authorization endpoint). */
    suspend fun requestUserCode(): DeviceAuthOutcome<DeviceCodeInfo> {
        val jsonBody: String? = postForm(authClient.initialDeviceCodeRequest())
        if (jsonBody == null) return DeviceAuthOutcome.Failure(NO_SERVER_RESPONSE)
        return try {
            val dto: DeviceCodeDto = json.decodeFromString(DeviceCodeDto.serializer(), jsonBody)
            DeviceAuthOutcome.Success(dto.toInfo())
        } catch (e: Exception) {
            DeviceAuthOutcome.Failure("Could not read the device code from the server: ${e.message}")
        }
    }

    /** One poll of the token endpoint; [PollResult.Pending] and [PollResult.SlowDown] mean "poll again after the interval". */
    suspend fun pollOnce(deviceCode: String): PollResult {
        val body: String = pollResponseBody(authClient.deviceCodePollRequest(deviceCode))
        if (body.isEmpty()) return PollResult.Error(POLL_ERROR_NO_RESPONSE, "No response from the token endpoint")
        return try {
            authClient.parsePollResponse(body, clock())
        } catch (e: Exception) {
            PollResult.Error(POLL_ERROR_PARSE, e.message)
        }
    }

    /**
     * Poll until the user approves, the device code expires, or a terminal error arrives:
     * at least [MIN_POLL_INTERVAL_SECONDS] between checks, longer when the server's interval
     * is larger, and further spaced out on slow_down, per RFC 8628.
     */
    suspend fun pollUntilSettled(info: DeviceCodeInfo): DeviceAuthOutcome<TokenResponse> {
        var intervalSeconds: Long = maxOf(MIN_POLL_INTERVAL_SECONDS, info.intervalSeconds)
        val expirySeconds: Long = info.expiresInSeconds ?: DEFAULT_DEVICE_CODE_EXPIRY_SECONDS
        val deadlineEpochMillis: Long = clock() + expirySeconds * 1000L
        while (true) {
            if (clock() >= deadlineEpochMillis) return DeviceAuthOutcome.Failure(DEVICE_CODE_EXPIRED)
            delayMillis(intervalSeconds * 1000L)
            when (val result: PollResult = pollOnce(info.deviceCode)) {
                is PollResult.Success -> return DeviceAuthOutcome.Success(result.token)

                is PollResult.Pending -> Unit

                is PollResult.SlowDown -> intervalSeconds += SLOW_DOWN_STEP_SECONDS

                is PollResult.Error ->
                    return DeviceAuthOutcome.Failure(
                        if (result.description == null) "Sign-in failed: ${result.code}"
                        else "Sign-in failed: ${result.code} (${result.description})",
                    )
            }
        }
    }

    /**
     * Exchange the device access token for the server token (client_credentials with a
     * jwt-bearer client assertion) and persist: device access/refresh tokens plus the
     * server token and its expiry.
     */
    suspend fun completeLogin(deviceToken: TokenResponse): DeviceAuthOutcome<TokenResponse> {
        // Persist up front: the device token stays valid even when the exchange fails,
        // keeping a later refresh-based re-exchange possible.
        persistDeviceToken(deviceToken)
        val jsonBody: String? = postForm(authClient.serverTokenRequest(deviceToken.accessToken))
        if (jsonBody == null) return DeviceAuthOutcome.Failure(NO_SERVER_RESPONSE)
        return try {
            val serverToken: TokenResponse = authClient.parseTokenResponse(jsonBody, clock())
            store.serverToken = serverToken.accessToken
            store.expiresAtEpochMillis = serverToken.expiresAtEpochMillis
            DeviceAuthOutcome.Success(serverToken)
        } catch (e: Exception) {
            DeviceAuthOutcome.Failure("Could not fetch the server token: ${e.message}")
        }
    }

    private fun persistDeviceToken(deviceToken: TokenResponse) {
        store.accessToken = deviceToken.accessToken
        store.refreshToken = deviceToken.refreshToken
    }

    private suspend fun postForm(request: Request): String? =
        withContext(Dispatchers.IO) {
            try {
                httpClient.newCall(request).execute().use { response ->
                    if (response.isSuccessful) response.body.string() else null
                }
            } catch (e: Exception) {
                null
            }
        }

    /**
     * The token endpoint answers 400 while the user has not approved yet (RFC 8628), so the
     * body is parsed regardless of the status code; empty string means no response at all.
     */
    private suspend fun pollResponseBody(request: Request): String =
        withContext(Dispatchers.IO) {
            try {
                httpClient.newCall(request).execute().use { response -> response.body.string() }
            } catch (e: Exception) {
                ""
            }
        }

    private fun DeviceCodeDto.toInfo(): DeviceCodeInfo =
        DeviceCodeInfo(deviceCode, userCode, verificationUri, interval ?: DEFAULT_INTERVAL_SECONDS, expiresIn)

    private companion object {
        const val NO_SERVER_RESPONSE: String = "No response from the server"
        const val POLL_ERROR_NO_RESPONSE: String = "no_server_response"
        const val POLL_ERROR_PARSE: String = "parse_error"
        const val DEVICE_CODE_EXPIRED: String = "The device code has expired. Please sign in again."
        // RFC 8628: minimum interval 10s (app floor), default interval 5s, default expiry 600s, slow_down step 5s.
        const val MIN_POLL_INTERVAL_SECONDS: Long = 10L
        const val SLOW_DOWN_STEP_SECONDS: Long = 5L
        const val DEFAULT_INTERVAL_SECONDS: Long = 5L
        const val DEFAULT_DEVICE_CODE_EXPIRY_SECONDS: Long = 600L
    }
}

/** Raw device-authorization-endpoint response (kotlinx-serialization DTO). */
@Serializable
private data class DeviceCodeDto(
    @SerialName("device_code") val deviceCode: String,
    @SerialName("user_code") val userCode: String,
    @SerialName("verification_uri") val verificationUri: String,
    @SerialName("interval") val interval: Long? = null,
    @SerialName("expires_in") val expiresIn: Long? = null,
)
