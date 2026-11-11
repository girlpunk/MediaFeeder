_: {
  perSystem = {pkgs, ...}: let
    # The Android SDK is licensed under a non-free agreement; accepting the
    # license here removes the need for
    # NIXPKGS_ACCEPT_ANDROID_SDK_LICENSE=1 in the environment.
    androidEnv = pkgs.androidenv.override {licenseAccepted = true;};
    buildToolsVersion = "36.0.0";
    androidPackages = androidEnv.composeAndroidPackages {
      buildToolsVersions = [buildToolsVersion];
      platformVersions = ["36"];
      platformToolsVersion = "37.0.0";
      cmdLineToolsVersion = "20.0";
      includeEmulator = false;
      includeNDK = false;
      includeSystemImages = false;
      includeCmake = false;
    };
    androidSdk = androidPackages.androidsdk;
    # AGP 9.4.0 requires Gradle >= 9.6.0, but nixos-26.05 only ships up to
    # 9.4.1 (pkgs.gradle_9) — build the exact version the project's wrapper
    # uses. wrapGradle adds the mitm-cache setup hook and the `gradle` shim
    # the mitmCache mechanism relies on; a bare gradle distribution has no
    # hooks.
    gradle = pkgs.gradle-packages.wrapGradle {
      gradle-unwrapped = pkgs.gradle-packages.mkGradle {
        version = "9.6.0";
        hash = "sha256-u66y/vhxCBjPDiYSAdq5ZMVy+SuUKBLfDDYg1ipSmgE=";
        defaultJava = pkgs.jdk21;
      };
      inherit
        (pkgs)
        callPackage
        concatTextFile
        lib
        makeSetupHook
        mitm-cache
        replaceVars
        runCommand
        symlinkJoin
        ;
    };
  in {
    packages.mediafeeder-android-tv = pkgs.stdenvNoCC.mkDerivation (finalAttrs: rec {
      pname = "mediafeeder-android-tv";
      version = "debug";

      # Exclude generated build artifacts from the build sources.
      src = pkgs.lib.fileset.toSource {
        root = ./.;
        fileset =
          pkgs.lib.fileset.intersection
          (pkgs.lib.fileset.fromSource ./.)
          (pkgs.lib.fileset.fileFilter (
              name: name != "build" && name != ".gradle" && name != ".kotlin"
            )
            ./.);
      };

      # The wrapped gradle provides the gradle setup hook: it starts the
      # mitm-cache replay proxy and defines the `gradle` command that
      # drives the build and check phases (gradleBuildTask/gradleCheckTask).
      nativeBuildInputs = [pkgs.jdk21 androidSdk gradle];

      # All dependencies are served from the mitm cache. deps.json is
      # generated/refreshed (with network access) by running:
      #   USE_BWRAP=0 bash \
      #     "$(nix build .#mediafeeder-android-tv.mitmCache.updateScript --print-out-paths)"
      mitmCache = gradle.fetchDeps {
        pkg = finalAttrs.finalPackage;
        data = ./deps.json;
      };

      # this is required for using mitm-cache on Darwin
      __darwinAllowLocalNetworking = true;

      env.ANDROID_HOME = "${androidSdk}/libexec/android-sdk";
      env.ANDROID_SDK_ROOT = "${androidSdk}/libexec/android-sdk";

      # The build user's passwd home (/homeless-shelter) is unwritable;
      # point the JVM (user.home) and AGP at a writable home instead.
      preBuild = ''
        export HOME="$TMP/home"
        mkdir -p "$HOME/.android" "$HOME/.kotlin"
        export ANDROID_USER_HOME="$HOME/.android"
      '';

      # The aapt2 that AGP downloads from Maven is a dynamically linked,
      # generic-Linux binary (ELF interpreter /lib64/ld-linux-x86-64.so.2)
      # whose daemon cannot start inside the nix build sandbox. The
      # Android SDK's aapt2 is patched by nixpkgs to run from the store,
      # so tell AGP to use it instead of the maven download.
      gradleFlags = [
        "-Dfile.encoding=utf-8"
        "-Dorg.gradle.project.android.aapt2FromMavenOverride=${androidSdk}/libexec/android-sdk/build-tools/${buildToolsVersion}/aapt2"
      ];
      gradleBuildTask = ":app:assembleDebug";
      gradleCheckTask = ":app:testDebugUnitTest";
      doCheck = true;

      # The default update task (nixDownloadDeps) resolves every resolvable
      # configuration, including :app:debugAndroidTestCompileClasspath, which
      # AGP 9 cannot resolve in isolation (variant ambiguity). Record the
      # dependencies the real build tasks need instead.
      gradleUpdateTask = ":app:assembleDebug :app:testDebugUnitTest";

      installPhase = ''
        mkdir -p "$out"
        cp app/build/outputs/apk/debug/app-debug.apk "$out/app-debug.apk"
      '';

      meta = {
        description = "MediaFeeder Android TV app (debug build)";
        sourceProvenance = with pkgs.lib.sourceTypes; [
          fromSource
          binaryBytecode # mitm cache
        ];
      };
    });

    make-shells.android = {
      name = "MediaFeeder Android";

      packages = [
        pkgs.jdk21
        androidSdk
        pkgs.unzip

        (pkgs.android-studio.withSdk androidSdk)
        androidPackages.platform-tools
      ];

      env = {
        ANDROID_HOME = "${androidSdk}/libexec/android-sdk";
        ANDROID_SDK_ROOT = "${androidSdk}/libexec/android-sdk";
      };
    };

    treefmt.programs = {
      ktfmt.enable = true;
      ktlint.enable = true;
      detekt.enable = true;
    };
  };
}
