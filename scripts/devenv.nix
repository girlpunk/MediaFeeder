{
  pkgs,
  lib,
  config,
  inputs,
  ...
}: {
  # https://devenv.sh/basics/
  env.GREET = "devenv";

  # https://devenv.sh/packages/
  packages = with pkgs;
    [
      git
      grpc_cli
      hello
    ]
    ++ (with pkgs.python3.pkgs; [
      aiofiles
      beautifulsoup4
      grpcio
      grpcio-tools
      ipython
      lxml
      pyatv
      pychromecast
      pydantic-settings
      pyyaml
      pyytlounge
      requests
      types-protobuf
    ]);

  # https://devenv.sh/scripts/
  tasks."grpc:rebuild" = {
    exec = ''
      ${pkgs.python3}/bin/python -m grpc_tools.protoc --proto_path=../MediaFeeder/Services --python_out=. --grpc_python_out=. ../MediaFeeder/Services/Api.proto
    '';
    description = "Rebuild GRPC";
  };

  # https://devenv.sh/languages/
  languages.python = {
    enable = true;
    venv = {
      enable = true;
      requirements = ''
        types-PyYAML
      '';
    };
  };

  cachix.enable = true;

  git-hooks.hooks = {
    alejandra.enable = true;
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
    ruff.enable = true;

    ruff-format.enable = true;
    #typos.enable = true;

    deadnix.enable = true;
    flake-checker.enable = true;
    statix.enable = true;
  };

  files.".ruff.toml".toml = {
    line-length = 180;
    lint.select = [
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

    lint.ignore = ["D401"];
  };

  enterShell = ''
    hello
    git --version
  '';

  # https://devenv.sh/tasks/
  # tasks = {
  #   "myproj:setup".exec = "mytool build";
  #   "devenv:enterShell".after = [ "myproj:setup" ];
  # };

  # https://devenv.sh/tests/
  enterTest = ''
    echo "Running tests"
    git --version | grep --color=auto "${pkgs.git.version}"
  '';

  # https://devenv.sh/git-hooks/
  # git-hooks.hooks.shellcheck.enable = true;

  # See full reference at https://devenv.sh/reference/options/
}
