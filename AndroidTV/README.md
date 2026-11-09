# MediaFeeder for Android TV

Android TV client for the self-hosted MediaFeeder server. Browse your subscriptions, list
their videos, and play downloaded ones — directly from a remote URL or streamed through
the server.

- Package: `xyz.foxocube.mediafeeder.androidtv` · label: **MediaFeeder**
- minSdk 23 (Android 6.0) · targetSdk/compileSdk 36 (Android 15; tested on Android TV 14)
- Compose for TV + navigation-compose + media3 ExoPlayer + OkHttp

## Building

Requires [Nix](https://nix.dev). The build is a native Nix package (derivation in
`flake.nix`, dev shell in `AndroidTV/default.nix`; JDK 21 + Android SDK 36), and runs
the unit tests together with `:app:assembleDebug`:

```sh
NIXPKGS_ALLOW_UNFREE=1 NIXPKGS_ACCEPT_ANDROID_SDK_LICENSE=1 \
  nix build --impure --no-sandbox .#android-app
```

(From the repository root. `--impure` lets the flake import `AndroidTV/default.nix`;
`--no-sandbox` because Gradle downloads its distribution and the maven dependencies
at build time — drop it if your nix does not enforce the sandbox.)
The debug-signed APK ends up at
`$(nix build --impure --no-sandbox .#android-app --print-out-path)/app-debug.apk`.

For interactive development (single Gradle tasks, `adb`, …) use the `android` dev shell:

```sh
NIXPKGS_ALLOW_UNFREE=1 NIXPKGS_ACCEPT_ANDROID_SDK_LICENSE=1 \
  nix develop --impure .#android \
  -c sh -c 'cd AndroidTV && ./gradlew :app:assembleDebug'
```

## Sideload onto the TV

1. Keep **Unknown sources** enabled for your file manager / adb on the TV.
2. **Over adb** (easiest):

   ```sh
   adb connect <tv-ip>:5555
   adb install -r AndroidTV/app/build/outputs/apk/debug/app-debug.apk
   ```

3. **Over USB / network share**: copy `app-debug.apk` to the TV (or a share the TV can
   mount), open it with a file manager, and confirm the install prompts.
4. Launch **MediaFeeder** from the TV launcher.

## First-run settings

The settings screen collects the four values below (persisted in app preferences):

| Field | What to enter |
|---|---|
| **Server base URL** | Your MediaFeeder server base, e.g. `http://mediafeeder.lan:5051` |
| **OIDC URL** | Your Authentik base, e.g. `https://auth.example.com` |
| **Device client ID** | Client ID of a public OIDC app used for the TV (device code flow), matching the `Auth:ClientId` the server accepts |
| **Server client ID** | Client ID of the confidential server app that can mint the MediaFeeder server token (`client_credentials` with a jwt-bearer client assertion) |

Then tap **Sign in**: the app shows a verification code, you approve the device code on
your phone (Authentik), and the app exchanges the device token for the server token —
mirroring `scripts/auth.py` from the main repository. The refresh token is stored and used
for the server token; the API is called with the server token while it is valid.

## Playback

- Video with a **remote** `downloadedPath` (`http://`/`https://`) → played directly from
  that URL.
- Video with a **local** path → streamed from the server at
  `/api/Video/{id}/play` (server checks ownership and streams with range support).
- Video **not downloaded yet** → a "Not downloaded" card with **Retry** (the endpoint
  answers HTTP 412 until the download exists).

## Tests

Unit tests (pure JVM, no emulator) cover the auth flow, token management, API client,
DTO parsing and playback URL resolution:

```sh
NIXPKGS_ALLOW_UNFREE=1 NIXPKGS_ACCEPT_ANDROID_SDK_LICENSE=1 \
  nix develop --impure .#android \
  -c sh -c 'cd AndroidTV && ./gradlew :app:testDebugUnitTest'
```
