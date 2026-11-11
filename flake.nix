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
      perSystem = {system, ...}: {
        # Package definitions live in the per-feature modules (see
        # scripts/default.nix and AndroidTV/default.nix).

        # Allow non-free packages (the Android SDK is one) without needing
        # NIXPKGS_ALLOW_UNFREE=1 in the environment. This module-arg
        # override replaces pkgs for all modules of this flake (packages,
        # dev shells, ...).
        _module.args.pkgs = import nixpkgs {
          inherit system;
          config.allowUnfree = true;
        };

        treefmt = {
          # Used to find the project root
          projectRootFile = "flake.nix";

          programs = {
            # Enable the Nix formatter
            alejandra.enable = true;
            statix.enable = true;
            deadnix.enable = true;

            # Workflows
            actionlint.enable = true;

            # Bash
            beautysh.enable = true;

            # Protobuf
            buf.enable = true;

            # Docker
            dockerfmt.enable = true;
            dockfmt.enable = true;
          };
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
