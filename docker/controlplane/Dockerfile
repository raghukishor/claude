FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ["src/Rbac.Shared/Rbac.Shared.csproj", "src/Rbac.Shared/"]
COPY ["src/Rbac.ControlPlane/Rbac.ControlPlane.csproj", "src/Rbac.ControlPlane/"]

# Restore dependencies
RUN dotnet restore "src/Rbac.ControlPlane/Rbac.ControlPlane.csproj"

# Copy source code
COPY ["src/Rbac.Shared/", "src/Rbac.Shared/"]
COPY ["src/Rbac.ControlPlane/", "src/Rbac.ControlPlane/"]

# Build
WORKDIR "/src/src/Rbac.ControlPlane"
RUN dotnet build "Rbac.ControlPlane.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Rbac.ControlPlane.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Rbac.ControlPlane.dll"]
