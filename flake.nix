{
  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-26.05";

    flake-parts.url = "github:hercules-ci/flake-parts";

    treefmt-nix = {
      url = "github:numtide/treefmt-nix";
      inputs.nixpkgs.follows = "nixpkgs";
    };

    make-shell = {
      url = "github:nicknovitski/make-shell";

      inputs.flake-compat.follows = "flake-compat";
    };

    git-hooks-nix = {
      url = "github:cachix/git-hooks.nix";

      inputs.nixpkgs.follows = "nixpkgs";
      inputs.flake-compat.follows = "flake-compat";
    };

    flake-compat.url = "github:NixOS/flake-compat";
  };

  outputs = inputs @ {
    flake-parts,
    nixpkgs,
    ...
  }:
  # https://flake.parts/module-arguments.html
    flake-parts.lib.mkFlake {inherit inputs;} ({...}: {
      imports = [
        inputs.make-shell.flakeModules.default
        inputs.treefmt-nix.flakeModule
        inputs.git-hooks-nix.flakeModule
        ./scripts
        ./AndroidTV
      ];
      #flake = {
      #  # Put your original flake attributes here.
      #};
      systems = [
        # systems for which you want to build the `perSystem` attributes
        "x86_64-linux"
        # ...
      ];
      perSystem = {
        pkgs,
        system,
        ...
      }: let
        # Keep in sync with the dev shell in AndroidTV/default.nix.
        # The Android SDK is licensed under a non-free agreement; accepting
        # the license here removes the need for
        # NIXPKGS_ACCEPT_ANDROID_SDK_LICENSE=1 in the environment.
        androidEnv = pkgs.androidenv.override {licenseAccepted = true;};
        buildToolsVersion = "36.0.0";
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
          inherit (pkgs) lib;
          inherit (pkgs) callPackage;
          inherit
            (pkgs)
            mitm-cache
            replaceVars
            symlinkJoin
            concatTextFile
            makeSetupHook
            runCommand
            ;
        };
      in {
        # Allow non-free packages (the Android SDK is one) without needing
        # NIXPKGS_ALLOW_UNFREE=1 in the environment. This module-arg
        # override replaces pkgs for all modules of this flake (packages,
        # dev shells, ...).
        _module.args.pkgs = import nixpkgs {
          inherit system;
          config.allowUnfree = true;
        };

        packages.mediafeeder-android-tv = pkgs.stdenvNoCC.mkDerivation (finalAttrs: rec {
          pname = "mediafeeder-android-tv";
          version = "debug";

          # Exclude generated build artifacts from the build sources.
          src = pkgs.lib.fileset.toSource {
            root = ./AndroidTV;
            fileset =
              pkgs.lib.fileset.intersection
              (pkgs.lib.fileset.fromSource ./AndroidTV)
              (pkgs.lib.fileset.fileFilter (
                  name: name != "build" && name != ".gradle" && name != ".kotlin"
                )
                ./AndroidTV);
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

        # Recommended: move all package definitions here.
        # e.g. (assuming you have a nixpkgs input)
        # packages.foo = pkgs.callPackage ./foo/package.nix { };
        # packages.bar = pkgs.callPackage ./bar/package.nix {
        #   foo = config.packages.foo;
        # };

        treefmt = {
          # Used to find the project root
          #projectRootFile = "flake.nix";

          # Enable the Nix formatter
          programs.alejandra.enable = true;
          programs.statix.enable = true;
        };

        pre-commit = {
          check.enable = true;
          settings = {
            enable = true;
            hooks = {
              check-added-large-files.enable = true;
              check-builtin-literals.enable = true;
              check-case-conflicts.enable = true;
              check-docstring-first.enable = true;
              check-executables-have-shebangs.enable = true;
              check-json.enable = true;
              check-merge-conflicts.enable = true;
              check-python.enable = true;
              check-shebang-scripts-are-executable.enable = true;
              check-symlinks.enable = true;
              check-vcs-permalinks.enable = true;
              #commitizen.enable = true;
              fix-byte-order-marker.enable = true;
              #fix-encoding-pragma.enable = true;

              #typos.enable = true;

              deadnix.enable = true;
              flake-checker.enable = true;
            };
          };
        };
      };
    });
}
