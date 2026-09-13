FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TaskFlow.Api/TaskFlow.Api.csproj TaskFlow.Api/
COPY TaskFlow.Application/TaskFlow.Application.csproj TaskFlow.Application/
COPY TaskFlow.Domain/TaskFlow.Domain.csproj TaskFlow.Domain/
COPY TaskFlow.Infra/TaskFlow.Infra.csproj TaskFlow.Infra/
RUN dotnet restore TaskFlow.Api/TaskFlow.Api.csproj

COPY . .
RUN dotnet publish TaskFlow.Api/TaskFlow.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The Infisical CLI, which the entrypoint uses to fetch configuration from the vault at start.
# artifacts-cli.infisical.com replaces the old Cloudsmith repository, which stops serving
# 2026-09-16. curl and gnupg are needed only to add the repository and are removed again.
#
# The version is PINNED, and must stay pinned. CLI 0.43.0 moved secret fetching to the
# server's /api/v4/secrets route; this self-hosted vault does not serve v4 and answers 404,
# so `infisical run` fetches nothing and the API starts with an empty configuration. 0.42.6
# is the last release that uses /api/v3/secrets/raw, which the vault does serve. An unpinned
# install is exactly what broke production on 2026-09-13: the rebuild silently took 0.43.132.
# Raise this pin only after the vault is upgraded and /api/v4/secrets answers.
ARG INFISICAL_CLI_VERSION=0.42.6
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
 && curl -1sLf 'https://artifacts-cli.infisical.com/setup.deb.sh' | bash \
 && apt-get update \
 && apt-get install -y --no-install-recommends "infisical=${INFISICAL_CLI_VERSION}" \
 && infisical --version \
 && apt-get purge -y --auto-remove curl gnupg \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY docker-entrypoint.sh report-config.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/docker-entrypoint.sh /usr/local/bin/report-config.sh

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    INFISICAL_DISABLE_UPDATE_CHECK=true

EXPOSE 8080
USER $APP_UID

# The entrypoint execs this command, with the vault's secrets in the environment when a machine
# identity is configured and without them when it is not.
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["dotnet", "TaskFlow.Api.dll"]
