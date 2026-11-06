# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage: restore + publish a self-contained, single-file binary for Linux.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project (bin/obj/.env excluded via .dockerignore).
COPY McpMailGuard/ ./McpMailGuard/

RUN dotnet restore ./McpMailGuard/McpMailGuard.csproj

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

# Copy the published self-contained app (binary + appsettings.json).
COPY --from=build /publish /app

# Run as a non-root user.
RUN useradd --create-home --uid 10001 appuser
USER appuser

# The MCP server communicates over stdio.
ENTRYPOINT ["/app/McpMailGuard"]
