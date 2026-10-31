# BusinessOS API image.
# Build from the BusinessOS repository root:
#   docker build -t businessos-api .
#
# Connection strings and signing keys are supplied at runtime.
# This image does not apply EF Core migrations on startup.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY BusinessOS.API/BusinessOS.API.csproj BusinessOS.API/
COPY BusinessOS.Application/BusinessOS.Application.csproj BusinessOS.Application/
COPY BusinessOS.Domain/BusinessOS.Domain.csproj BusinessOS.Domain/
COPY BusinessOS.Infrastructure/BusinessOS.Infrastructure.csproj BusinessOS.Infrastructure/

RUN dotnet restore BusinessOS.API/BusinessOS.API.csproj

COPY . .
# Infrastructure copies appsettings.json to its output for local tooling.
# That file duplicates the API settings on publish (NETSDK1152). The API project
# owns runtime configuration, so drop the library copy inside the image only.
RUN rm -f BusinessOS.Infrastructure/appsettings.json \
 && dotnet publish BusinessOS.API/BusinessOS.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# The published settings files contain the local SQL Express connection used by
# `dotnet run` on Windows. Remove that value from the image so a container cannot
# fall back to the development database. Runtime configuration must supply it.
RUN find /app/publish -name 'appsettings*.json' -print0 \
    | xargs -0 sed -i 's|Server=localhost\\\\SQLEXPRESS;Database=BusinessOS;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=60||g'

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/Logs \
    && chown -R app:app /app

COPY --from=build --chown=app:app /app/publish .

USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=0 \
    Database__ApplyMigrationsOnStartup=false

EXPOSE 8080

STOPSIGNAL SIGTERM

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "BusinessOS.API.dll"]
