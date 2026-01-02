FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ["src/Rbac.Shared/Rbac.Shared.csproj", "src/Rbac.Shared/"]
COPY ["src/Rbac.DataPlane/Rbac.DataPlane.csproj", "src/Rbac.DataPlane/"]

# Restore dependencies
RUN dotnet restore "src/Rbac.DataPlane/Rbac.DataPlane.csproj"

# Copy source code
COPY ["src/Rbac.Shared/", "src/Rbac.Shared/"]
COPY ["src/Rbac.DataPlane/", "src/Rbac.DataPlane/"]

# Build
WORKDIR "/src/src/Rbac.DataPlane"
RUN dotnet build "Rbac.DataPlane.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Rbac.DataPlane.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Rbac.DataPlane.dll"]
