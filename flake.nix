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

  outputs = inputs @ {flake-parts, ...}:
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
      perSystem = {pkgs, ...}: let
        scripts = ./scripts;

        # Keep in sync with the dev shell in AndroidTV/default.nix.
        androidPackages = pkgs.androidenv.composeAndroidPackages {
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
        packages.android-app = pkgs.stdenvNoCC.mkDerivation rec {
          pname = "mediafeeder-android-tv";
          version = "debug";

          src = pkgs.lib.fileset.toSource {
            root = ./AndroidTV;
            # Keep the Gradle project files, drop build outputs and local caches.
            fileset = pkgs.lib.fileset.intersection
              (pkgs.lib.fileset.fromSource ./AndroidTV)
              (pkgs.lib.fileset.fileFilter
                (name: name != "build" && name != ".gradle" && name != ".kotlin")
                ./AndroidTV);
          };

          nativeBuildInputs = [ pkgs.jdk21 androidSdk ];

          env.ANDROID_HOME = "${androidSdk}/libexec/android-sdk";
          env.ANDROID_SDK_ROOT = "${androidSdk}/libexec/android-sdk";

          # Gradle downloads its distribution and the maven dependencies from
          # the network at build time, so the build must run unsandboxed.
          # Results are still cached in the store / binary caches.
          __noChroot = true;

          # buildCommand replaces the standard phase sequence entirely, so the
          # whole thing (copy, test, build, collect) runs here.
          buildCommand = ''
            cp -a $src/. $TMP/app
            # The store-derived copy is read-only; gradle needs to write .gradle/.
            chmod -R u+w $TMP/app
            cd $TMP/app
            export GRADLE_USER_HOME="$TMP/gradle-user-home"
            # The build user's passwd home is unwritable; point gradle/JVM and
            # AGP at a writable home instead (no daemon: its lifecycle does not
            # work in the build environment).
            export HOME="$TMP/home"
            mkdir -p "$HOME/.android"
            export ANDROID_USER_HOME="$HOME/.android"
            ./gradlew --no-daemon -Duser.home="$HOME" \
              :app:testDebugUnitTest :app:assembleDebug --console=plain
            mkdir -p "$out"
            cp app/build/outputs/apk/debug/app-debug.apk "$out/"
          '';
        };

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
