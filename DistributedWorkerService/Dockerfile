# Use the official .NET 8 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["DistributedWorkerService.csproj", "."]
RUN dotnet restore "DistributedWorkerService.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "DistributedWorkerService.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "DistributedWorkerService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published app
COPY --from=publish /app/publish .

# Add health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet DistributedWorkerService.dll --health-check || exit 1

ENTRYPOINT ["dotnet", "DistributedWorkerService.dll"]