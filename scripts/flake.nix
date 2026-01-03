{
  nixConfig = {
    extra-substituters = "https://cachix.cachix.org";
    extra-trusted-public-keys = "cachix.cachix.org-1:eWNHQldwUO7G2VkjpnjDbWwy4KQ/HNxht7H4SSoMckM=";
  };

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/release-25.05";

    flake-parts.url = "github:hercules-ci/flake-parts";

    treefmt-nix = {
      url = "github:numtide/treefmt-nix";
      inputs.nixpkgs.follows = "nixpkgs";
    };

    make-shell.url = "github:nicknovitski/make-shell";

    git-hooks-nix = {
      url = "github:cachix/git-hooks.nix";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = inputs @ {flake-parts, ...}:
  # https://flake.parts/module-arguments.html
    flake-parts.lib.mkFlake {inherit inputs;} (top @ {
      config,
      withSystem,
      moduleWithSystem,
      ...
    }: {
      imports = [
        inputs.make-shell.flakeModules.default
        inputs.treefmt-nix.flakeModule
        inputs.git-hooks-nix.flakeModule
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
        config,
        pkgs,
        ...
      }: let
        all_dependencies = with pkgs;
          [
            grpc_cli
            python3
            #(pkgs.callPackage wakepy {})
          ]
          ++ (with pkgs.python3.pkgs; [
            aiofiles
            beautifulsoup4
            fasteners
            grpcio
            grpcio-tools
            ipython
            lxml
            pyatv
            pychromecast
            pyyaml
            pyytlounge
            requests
            types-protobuf
            types-pyyaml
          ]);
      in {
        # Recommended: move all package definitions here.
        # e.g. (assuming you have a nixpkgs input)
        # packages.foo = pkgs.callPackage ./foo/package.nix { };
        # packages.bar = pkgs.callPackage ./bar/package.nix {
        #   foo = config.packages.foo;
        # };

        make-shells.default = {
          name = "MediaFeeder Scripts";
          #commands = [
          #  {
          #    name = "grpc-rebuild";
          #    help = "Re-generate python GRPC bindings";
          #    command = "${pkgs.python3}/bin/python -m grpc_tools.protoc --proto_path=../MediaFeeder/Services --python_out=. --grpc_python_out=. ../MediaFeeder/Services/Api.proto";
          #  }
          #];
          packages = with pkgs;
            [
              git
              #grpc-rebuild
            ]
            ++ all_dependencies;
        };

        treefmt = {
          # Used to find the project root
          #projectRootFile = "flake.nix";

          # Enable the Nix formatter
          programs.alejandra.enable = true;
          programs.statix.enable = true;

          # Enable the Python formatters
          programs.ruff-format = {
            enable = true;
            lineLength = 180;
          };
          programs.ruff-check = {
            enable = true;
            # lint.ignore = ["D401", "E501"];
            extendSelect = [
              "A"
              "ANN"
              "ARG"
              "ASYNC"
              "B"
              "BLE"
              "C"
              "C4"
              "C90"
              "COM"
              "D"
              "DOC"
              "DTZ"
              "E"
              "EM"
              "EXE"
              "F"
              "F"
              "FA"
              "FBT"
              "FIX"
              "FLY"
              "FURB"
              "G"
              "I"
              "ICN"
              "INP"
              "INT"
              "ISC"
              "LOG"
              "N"
              "PERF"
              "PGH"
              "PIE"
              "PL"
              "PTH"
              "PYI"
              "Q"
              "Q"
              "RET"
              "RSE"
              "RUF"
              "S"
              "SIM"
              "SLF"
              "T10"
              "T20"
              "TC"
              "TD"
              "TID"
              "TRY"
              "UP"
              "W"
              "W"
              "YTT"
            ];
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
              mypy.enable = true;

              #typos.enable = true;

              deadnix.enable = true;
              flake-checker.enable = true;
            };
          };
        };
      };
    });
}
#        wakepy = _:
#          pkgs.python313Packages.buildPythonPackage rec {
#            pname = "wakepy";
#            version = "0.10.2";
#            format = "pyproject";
#
#            #nativeBuildInputs = with pkgs.python313Packages; [
#            #];
#
#            propagatedBuildInputs = with pkgs.python313Packages; [
#              pythonRelaxDepsHook
#              setuptools
#              setuptools-scm
#            ];
#
#            pythonRelaxDeps = [
#              "setuptools"
#              "setuptools_scm"
#            ];
#
#            src = pkgs.fetchPypi {
#              inherit pname version;
#              hash = "sha256-kluImlLWj9k+8VOTCBWVr2xW18s/Cl/vl+E+LL4YdYc=";
#            };
#          };

