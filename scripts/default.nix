_: {
  perSystem = {
    pkgs,
    lib,
    ...
  }: let
    pbpy = pkgs.python3.withPackages (p:
      with p; [
        grpcio-tools
        mypy-protobuf
      ]);

    mfprotos = pkgs.python3Packages.buildPythonPackage {
      name = "mfprotos";
      src = ../MediaFeeder/Services;
      dontUnpack = true;
      format = "other";
      propagatedBuildInputs = [pkgs.protobuf];
      buildPhase = ''
        runHook preBuild
        ${pbpy}/bin/python3 -m grpc_tools.protoc \
          --plugin=${pbpy.pkgs.mypy-protobuf}/bin/protoc-gen-mypy \
          --proto_path=$src \
          --python_out=. \
          --grpc_python_out=. \
          --mypy_out=. \
          $src/Api.proto
        runHook postBuild
      '';
      installPhase = ''
        runHook preInstall
        mkdir -p $out/${pbpy.sitePackages}
        install -m644 -D *.py *.pyi $out/${pbpy.sitePackages}/
        runHook postInstall
      '';
    };

    python = pkgs.python3.withPackages (p:
      with p; [
        mfprotos
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
        types-requests
      ]);

    bridges = pkgs.stdenv.mkDerivation {
      name = "mediafeeder-bridges";
      src = ./.;
      propagatedBuildInputs = [python];

      nativeBuildInputs = [ pkgs.makeWrapper ];

      installPhase = ''
        runHook preInstall

        sp="$out/${python.sitePackages}"
        bin="$out/bin"
        mkdir -p "$sp" "$bin"
        install -m644 *.py "$sp"

        makeWrapper "${lib.getExe python}" $bin/mediafeeder-chromecast-bridge --add-flags "$sp/chromecast_bridge.py"
        makeWrapper "${lib.getExe python}" $bin/mediafeeder-get-stars         --add-flags "$sp/get_stars.py"
        makeWrapper "${lib.getExe python}" $bin/mediafeeder-lounge-bridge     --add-flags "$sp/lounge_bridge.py"

        runHook postInstall
      '';

      meta = {
        description = "MediaFeeder Bridges";
        homepage = "https://github.com/girlpunk/mediafeeder";
      };
    };
  in {
    make-shells.scripts = {
      name = "MediaFeeder Scripts";
      packages = with pkgs;
        [
          git
          grpc_cli
        ]
        ++ [python];
    };

    packages = {
      mediafeeder-bridges = bridges;
    };

    apps.mediafeeder-bridges = {
      program = bridges;
    };

    treefmt = {
      # Enable the Python formatters
      programs.ruff-format = {
        enable = true;
        lineLength = 180;
      };
      programs.ruff-check = {
        enable = true;
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
      settings = {
        hooks = {
          build-grpc = {
            enable = true;
            name = "Build GRPC Generate Files";
            entry = "${pkgs.python3}/bin/python -m grpc_tools.protoc --proto_path=. --python_out=. --grpc_python_out=. Api.proto";
            # language = "system";
            pass_filenames = false;
            stages = ["pre-commit"];

            before = ["mypy"];
          };

          mypy.enable = true;
          mypy.extraPackages = [python];
          mypy.args = ["--follow-untyped-imports"];

          ruff.enable = true;

          ruff-format.enable = true;
        };
      };
    };
  };
}
