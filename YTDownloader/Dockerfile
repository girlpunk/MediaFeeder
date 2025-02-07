FROM jauderho/yt-dlp AS builder
LABEL authors="sam"

COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/

# Change the working directory to the `app` directory
WORKDIR /app

RUN apk add --no-cache \
        g++ \
        python3-dev \
        linux-headers

# Install dependencies
RUN --mount=type=cache,target=/root/.cache/uv \
    --mount=type=bind,source=uv.lock,target=uv.lock \
    --mount=type=bind,source=pyproject.toml,target=pyproject.toml \
    uv sync --frozen --no-install-project --no-editable

# Copy the project into the image
ADD . /app
ENV UV_LINK_MODE=copy

RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --frozen --compile-bytecode

RUN uv run -m grpc_tools.protoc -I. --python_out=. --pyi_out=. --grpc_python_out=. downloadServer.proto

FROM jauderho/yt-dlp
WORKDIR /app
COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/
COPY --from=builder --chown=app:app /app /app

USER app
ENV PYTHONUNBUFFERED=1

ENTRYPOINT ["uv", "run", "main.py"]
