# RBAC Authorization System

A Role-Based Access Control (RBAC) authorization system inspired by Azure RBAC, built with .NET 10 and CosmosDB.

## Architecture

The system consists of three microservices:

| Service | Port | Description |
|---------|------|-------------|
| **Control Plane** | 5001 | Manages RBAC policies (role definitions, role assignments) |
| **Data Plane** | 5002 | Handles authorization checks (CheckAccess API) |
| **Sync Service** | - | Background worker that syncs data between planes using CosmosDB Change Feed |

## Prerequisites

### 1. Docker Desktop
- Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) for your platform
- Ensure Docker is running before starting the services

### 2. Azure CosmosDB Emulator
The system requires the CosmosDB Emulator running on your host machine.

**Windows (Native Installation):**
1. Download and install the [Azure CosmosDB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator)
2. Start the emulator from the Start menu
3. The emulator runs at `https://localhost:8081`

**macOS/Linux (Docker):**
```bash
# Pull and run the CosmosDB Emulator
docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 \
  -m 3g --cpus=2.0 \
  --name=cosmosdb-emulator \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true \
  -e AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=127.0.0.1 \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

**Verify Emulator is Running:**
- Open https://localhost:8081/_explorer/index.html in your browser
- Accept the self-signed certificate warning

## Running with Docker

### Quick Start

```bash
# Navigate to the docker directory
cd docker

# Build and start all services
docker-compose up --build
```

### Services Will Be Available At:
- **Control Plane API**: http://localhost:5001
- **Data Plane API**: http://localhost:5002

### Stop Services
```bash
docker-compose down
```

### View Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f controlplane
docker-compose logs -f dataplane
docker-compose logs -f syncservice
```

## API Endpoints

### Control Plane (http://localhost:5001)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/roleDefinitions` | Create a role definition |
| GET | `/api/v1/roleDefinitions/{id}` | Get a role definition |
| GET | `/api/v1/roleDefinitions` | List all role definitions |
| PUT | `/api/v1/roleDefinitions/{id}` | Update a role definition |
| DELETE | `/api/v1/roleDefinitions/{id}` | Delete a role definition |
| POST | `/api/v1/scopes/{scope}/roleAssignments` | Create a role assignment |
| GET | `/api/v1/scopes/{scope}/roleAssignments/{id}` | Get a role assignment |
| GET | `/api/v1/scopes/{scope}/roleAssignments` | List role assignments |
| DELETE | `/api/v1/scopes/{scope}/roleAssignments/{id}` | Delete a role assignment |

### Data Plane (http://localhost:5002)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/checkAccess` | Check if a principal has access |
| POST | `/api/v1/checkAccess/batch` | Batch access check |

## Example Usage

### 1. Create a Role Definition

```bash
curl -X POST http://localhost:5001/api/v1/roleDefinitions \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Reader",
    "description": "Can read all resources",
    "permissions": [{
      "actions": ["*/read"],
      "notActions": []
    }],
    "assignableScopes": ["/"]
  }'
```

### 2. Create a Role Assignment

```bash
curl -X POST "http://localhost:5001/api/v1/scopes/%2Fsubscriptions%2Fsub-001/roleAssignments" \
  -H "Content-Type: application/json" \
  -d '{
    "principalId": "user-123",
    "principalType": "User",
    "roleDefinitionId": "<role-definition-id-from-step-1>"
  }'
```

### 3. Check Access

```bash
curl -X POST http://localhost:5002/api/v1/checkAccess \
  -H "Content-Type: application/json" \
  -d '{
    "subject": {
      "principalId": "user-123",
      "principalType": "User"
    },
    "resource": {
      "scope": "/subscriptions/sub-001/resourceGroups/rg-001",
      "type": "Microsoft.Compute/virtualMachines"
    },
    "action": {
      "name": "Microsoft.Compute/virtualMachines/read"
    }
  }'
```

## Development

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Rbac.Shared.Tests
dotnet test tests/Rbac.ControlPlane.Tests
dotnet test tests/Rbac.DataPlane.Tests
dotnet test tests/Rbac.SyncService.Tests
```

### Running Without Docker

```bash
# Start Control Plane
cd src/Rbac.ControlPlane
dotnet run

# Start Data Plane (in another terminal)
cd src/Rbac.DataPlane
dotnet run

# Start Sync Service (in another terminal)
cd src/Rbac.SyncService
dotnet run
```

## Configuration

Environment variables can be configured in `docker-compose.yml` or passed at runtime:

| Variable | Description | Default |
|----------|-------------|---------|
| `CosmosDb__Endpoint` | CosmosDB endpoint URL | `https://host.docker.internal:8081` |
| `CosmosDb__Key` | CosmosDB access key | Emulator default key |
| `CosmosDb__DatabaseName` | Database name | `RbacControlPlane` / `RbacDataPlane` |
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development` |

## Troubleshooting

### Docker cannot connect to CosmosDB Emulator

1. Ensure the emulator is running on your host machine
2. On Windows, `host.docker.internal` should resolve to the host
3. On macOS/Linux, you may need to use `--network host` or configure the emulator's IP

### Certificate Errors

The CosmosDB Emulator uses a self-signed certificate. The services are configured to accept this in Development mode. If you see certificate errors:

1. Export the emulator certificate and trust it on your system
2. Or ensure `ASPNETCORE_ENVIRONMENT=Development` is set

### Services Start Before Databases Exist

The services will create the required databases and containers on startup if they don't exist.
