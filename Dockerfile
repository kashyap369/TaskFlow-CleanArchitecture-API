FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TaskFlow.slnx ./
COPY TaskFlow.Api/TaskFlow.Api.csproj TaskFlow.Api/
COPY TaskFlow.Application/TaskFlow.Application.csproj TaskFlow.Application/
COPY TaskFlow.Domain/TaskFlow.Domain.csproj TaskFlow.Domain/
COPY TaskFlow.Infra/TaskFlow.Infra.csproj TaskFlow.Infra/
RUN dotnet restore TaskFlow.slnx

COPY . .
RUN dotnet publish TaskFlow.Api/TaskFlow.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
