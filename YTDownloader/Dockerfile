FROM ghcr.io/linuxcontainers/alpine:latest
LABEL authors="sam"

COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/

# Change the working directory to the `app` directory
WORKDIR /app

RUN set -x \
 && apk update \
 && apk upgrade -a \
 && apk add --no-cache \
        ca-certificates \
        ffmpeg \
        python3 \
        py3-mutagen \
        g++ \
        python3-dev \
        py3-grpcio-pyc \
        linux-headers

ENV UV_LINK_MODE=copy

# Install dependencies
RUN --mount=type=cache,target=/root/.cache/uv \
    --mount=type=bind,source=uv.lock,target=uv.lock \
    --mount=type=bind,source=pyproject.toml,target=pyproject.toml \
    uv sync --frozen --no-install-project --no-editable

# Copy the project into the image
COPY . /app

RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --compile-bytecode

ENV SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt

RUN uv run -m grpc_tools.protoc -I. --python_out=. --pyi_out=. --grpc_python_out=. downloadServer.proto

ENV PYTHONUNBUFFERED=1

ENTRYPOINT ["uv", "run", "main.py"]
