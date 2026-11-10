_: {
  perSystem = {pkgs, ...}: let
    # Same non-free Android SDK license acceptance as packages.mediafeeder-android-tv
    # in flake.nix (see the comment there).
    androidEnv = pkgs.androidenv.override {licenseAccepted = true;};
    androidPackages = androidEnv.composeAndroidPackages {
      buildToolsVersions = ["36.0.0"];
      platformVersions = ["36"];
      platformToolsVersion = "37.0.0";
      cmdLineToolsVersion = "20.0";
      includeEmulator = false;
      includeNDK = false;
      includeSystemImages = false;
      includeCmake = false;
    };
    androidSdk = androidPackages.androidsdk;
  in {
    make-shells.android = {
      name = "MediaFeeder Android";

      packages = [
        pkgs.jdk21
        androidSdk
        pkgs.unzip
      ];

      env = {
        ANDROID_HOME = "${androidSdk}/libexec/android-sdk";
        ANDROID_SDK_ROOT = "${androidSdk}/libexec/android-sdk";
      };
    };
  };
}
