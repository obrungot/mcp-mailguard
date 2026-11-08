# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage: restore + publish a self-contained, single-file binary for Linux.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project (bin/obj/.env/appsettings.Local.json excluded via .dockerignore).
COPY McpMailGuard/ ./McpMailGuard/

RUN dotnet restore ./McpMailGuard/McpMailGuard.csproj -r linux-x64

RUN dotnet publish ./McpMailGuard/McpMailGuard.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /publish \
    --no-restore

# ---------------------------------------------------------------------------
# Runtime stage: minimal Ubuntu-based image containing only the app.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

COPY --from=build /publish /app

# Non-root user. /data holds each account's saved sign-in and token cache; it is mounted as a
# volume and is readable only by this user.
RUN useradd --create-home --uid 10001 appuser \
    && mkdir -p /data \
    && chown appuser:appuser /data \
    && chmod 700 /data

# Each account's token cache is stored in /data/accounts/<name>/.
ENV MailGuard__DataDirectory=/data \
    Urls=http://+:8080 \
    ASPNETCORE_HTTP_PORTS= \
    DOTNET_ENVIRONMENT=Production

USER appuser
VOLUME ["/data"]
EXPOSE 8080

# Default: serve MCP over Streamable HTTP at http://<host>:8080/mcp.
# Sign in once with:  docker compose run --rm mailguard login <account>
ENTRYPOINT ["/app/McpMailGuard"]
