FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ["src/Rbac.Shared/Rbac.Shared.csproj", "src/Rbac.Shared/"]
COPY ["src/Rbac.SyncService/Rbac.SyncService.csproj", "src/Rbac.SyncService/"]

# Restore dependencies
RUN dotnet restore "src/Rbac.SyncService/Rbac.SyncService.csproj"

# Copy source code
COPY ["src/Rbac.Shared/", "src/Rbac.Shared/"]
COPY ["src/Rbac.SyncService/", "src/Rbac.SyncService/"]

# Build
WORKDIR "/src/src/Rbac.SyncService"
RUN dotnet build "Rbac.SyncService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Rbac.SyncService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Rbac.SyncService.dll"]
