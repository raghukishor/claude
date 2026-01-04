# RBAC Authorization System - Design Document

## 1. Executive Summary

This document outlines the design of a Role-Based Access Control (RBAC) authorization system modeled after Azure RBAC. The system consists of three main components:
- **Control Plane**: Manages RBAC policies through REST APIs
- **Data Plane**: High-performance CheckAccess API for authorization decisions
- **Data Sync Service**: CDC-based synchronization between control plane and data plane storage

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Control Plane                            │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Policy Management APIs                                   │   │
│  │  - Role Definitions                                       │   │
│  │  - Role Assignments                                       │   │
│  │  - Scope Management                                       │   │
│  └──────────────────────────────────────────────────────────┘   │
│                            ↓                                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Cosmos DB (Primary Storage)                             │   │
│  │  - Role Definitions Collection                           │   │
│  │  - Role Assignments Collection                           │   │
│  │  - Change Feed Enabled                                   │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                    Change Feed (CDC)
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                      Data Sync Service                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Change Feed Processor                                    │   │
│  │  - Captures changes from Control Plane Cosmos DB         │   │
│  │  - Transforms data for data plane                        │   │
│  │  - Handles deletes, updates, inserts                     │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                          Data Plane                              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  CheckAccess API                                          │   │
│  │  - High-performance authorization decisions              │   │
│  │  - Read-only operations                                  │   │
│  │  - Optimized for low latency                            │   │
│  └──────────────────────────────────────────────────────────┘   │
│                            ↓                                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Cosmos DB (Read-Optimized Replica)                      │   │
│  │  - Denormalized for fast lookups                         │   │
│  │  - Principal → Assignments Index                         │   │
│  │  - Scope → Assignments Index                             │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Design Principles

1. **Separation of Concerns**: Control plane handles policy management; data plane handles authorization decisions
2. **Independent Scaling**: Data plane can scale independently based on authorization request load
3. **High Availability**: Each component can be deployed redundantly
4. **Eventual Consistency**: Data plane may lag behind control plane by a few seconds
5. **Performance**: Data plane optimized for sub-100ms response times

## 3. Data Models

### 3.1 Azure RBAC Model Components

The system follows Azure RBAC's core concepts:
- **Scope**: The boundary where access applies (e.g., /subscriptions/{id}, /resourceGroups/{id}, /resources/{id})
- **Role Definition**: Set of permissions (actions, notActions, dataActions, notDataActions)
- **Principal**: Who gets access (user, group, service principal)
- **Role Assignment**: Links principal + role + scope

### 3.2 Control Plane Data Models

#### Role Definition
```json
{
  "id": "guid",
  "name": "Contributor",
  "type": "CustomRole | BuiltInRole",
  "description": "Can manage resources but not grant access",
  "permissions": [
    {
      "actions": [
        "*"
      ],
      "notActions": [
        "Microsoft.Authorization/*/Delete",
        "Microsoft.Authorization/*/Write",
        "Microsoft.Authorization/elevateAccess/Action"
      ],
      "dataActions": [
        "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/*"
      ],
      "notDataActions": []
    }
  ],
  "assignableScopes": [
    "/subscriptions/{subscription-id}",
    "/subscriptions/{subscription-id}/resourceGroups/{resource-group}"
  ],
  "createdOn": "2025-01-01T00:00:00Z",
  "updatedOn": "2025-01-01T00:00:00Z",
  "createdBy": "user@example.com",
  "updatedBy": "user@example.com",
  "_etag": "string",
  "ttl": -1
}
```

**Partition Key**: `/id`
**Container**: `RoleDefinitions`

#### Role Assignment
```json
{
  "id": "guid",
  "name": "assignment-name",
  "type": "Microsoft.Authorization/roleAssignments",
  "scope": "/subscriptions/{sub-id}/resourceGroups/{rg-name}",
  "roleDefinitionId": "/subscriptions/{sub-id}/providers/Microsoft.Authorization/roleDefinitions/{role-id}",
  "principalId": "guid",
  "principalType": "User | Group | ServicePrincipal",
  "condition": "optional-condition-expression",
  "conditionVersion": "2.0",
  "createdOn": "2025-01-01T00:00:00Z",
  "updatedOn": "2025-01-01T00:00:00Z",
  "createdBy": "user@example.com",
  "_etag": "string",
  "ttl": -1
}
```

**Partition Key**: `/scope`
**Container**: `RoleAssignments`

### 3.3 Data Plane Data Models

The data plane uses denormalized models optimized for quick lookups:

#### Principal Access Cache
```json
{
  "id": "{principalId}#{scope}",
  "principalId": "guid",
  "principalType": "User | Group | ServicePrincipal",
  "scope": "/subscriptions/{sub-id}/resourceGroups/{rg-name}",
  "assignments": [
    {
      "roleDefinitionId": "guid",
      "roleName": "Contributor",
      "permissions": {
        "actions": ["*"],
        "notActions": ["Microsoft.Authorization/*/Delete"],
        "dataActions": ["Microsoft.Storage/*"],
        "notDataActions": []
      },
      "condition": "optional-condition",
      "assignmentId": "guid"
    }
  ],
  "inheritedAssignments": [
    {
      "roleDefinitionId": "guid",
      "roleName": "Reader",
      "scope": "/subscriptions/{sub-id}",
      "permissions": {
        "actions": ["*/read"],
        "notActions": [],
        "dataActions": [],
        "notDataActions": []
      }
    }
  ],
  "lastSyncedAt": "2025-01-01T00:00:00Z",
  "version": 123,
  "_etag": "string",
  "ttl": -1
}
```

**Partition Key**: `/principalId`
**Container**: `PrincipalAccessCache`

#### Scope Access Index
```json
{
  "id": "{scope}#{roleDefinitionId}",
  "scope": "/subscriptions/{sub-id}/resourceGroups/{rg-name}",
  "roleDefinitionId": "guid",
  "principals": [
    {
      "principalId": "guid",
      "principalType": "User",
      "assignmentId": "guid",
      "condition": "optional-condition"
    }
  ],
  "lastSyncedAt": "2025-01-01T00:00:00Z",
  "_etag": "string",
  "ttl": -1
}
```

**Partition Key**: `/scope`
**Container**: `ScopeAccessIndex`

## 4. Control Plane APIs

### 4.1 Role Definition Management

#### Create/Update Role Definition
```http
PUT /providers/Microsoft.Authorization/roleDefinitions/{roleDefinitionId}
Content-Type: application/json

{
  "properties": {
    "roleName": "Custom Role Name",
    "description": "Description of the role",
    "type": "CustomRole",
    "permissions": [...],
    "assignableScopes": [...]
  }
}

Response: 200 OK | 201 Created
{
  "id": "/providers/Microsoft.Authorization/roleDefinitions/{roleDefinitionId}",
  "name": "{roleDefinitionId}",
  "type": "Microsoft.Authorization/roleDefinitions",
  "properties": {...}
}
```

#### Get Role Definition
```http
GET /providers/Microsoft.Authorization/roleDefinitions/{roleDefinitionId}

Response: 200 OK
{
  "id": "/providers/Microsoft.Authorization/roleDefinitions/{roleDefinitionId}",
  "properties": {...}
}
```

#### List Role Definitions
```http
GET /{scope}/providers/Microsoft.Authorization/roleDefinitions

Response: 200 OK
{
  "value": [...],
  "nextLink": "optional-continuation-token"
}
```

#### Delete Role Definition
```http
DELETE /providers/Microsoft.Authorization/roleDefinitions/{roleDefinitionId}

Response: 204 No Content
```

### 4.2 Role Assignment Management

#### Create Role Assignment
```http
PUT /{scope}/providers/Microsoft.Authorization/roleAssignments/{roleAssignmentId}
Content-Type: application/json

{
  "properties": {
    "roleDefinitionId": "/providers/Microsoft.Authorization/roleDefinitions/{roleDefId}",
    "principalId": "{principalId}",
    "principalType": "User",
    "condition": "@Resource[Microsoft.Storage/storageAccounts/blobServices/containers:name] StringEquals 'foo'",
    "conditionVersion": "2.0"
  }
}

Response: 201 Created
{
  "id": "/{scope}/providers/Microsoft.Authorization/roleAssignments/{assignmentId}",
  "name": "{assignmentId}",
  "type": "Microsoft.Authorization/roleAssignments",
  "properties": {...}
}
```

#### Get Role Assignment
```http
GET /{scope}/providers/Microsoft.Authorization/roleAssignments/{roleAssignmentId}

Response: 200 OK
```

#### List Role Assignments
```http
GET /{scope}/providers/Microsoft.Authorization/roleAssignments?$filter=principalId eq '{principalId}'

Response: 200 OK
{
  "value": [...],
  "nextLink": "optional-continuation-token"
}
```

#### Delete Role Assignment
```http
DELETE /{scope}/providers/Microsoft.Authorization/roleAssignments/{roleAssignmentId}

Response: 204 No Content
```

## 5. Data Plane API

### 5.1 CheckAccess API

#### Check Access Request
```http
POST /checkAccess
Content-Type: application/json

{
  "principal": {
    "id": "{principalId}",
    "type": "User",
    "groups": ["{groupId1}", "{groupId2}"]
  },
  "resource": {
    "scope": "/subscriptions/{sub-id}/resourceGroups/{rg-name}/providers/Microsoft.Storage/storageAccounts/{account}",
    "attributes": {
      "containerName": "foo",
      "accountType": "Standard_LRS"
    }
  },
  "action": "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write",
  "actionType": "dataAction"
}

Response: 200 OK
{
  "decision": "Allowed | Denied",
  "reason": "NoApplicableRoleAssignment | ActionNotPermitted | ConditionNotSatisfied | Success",
  "appliedRoles": [
    {
      "roleDefinitionId": "{roleDefId}",
      "roleName": "Storage Blob Data Contributor",
      "scope": "/subscriptions/{sub-id}/resourceGroups/{rg-name}",
      "assignmentId": "{assignmentId}"
    }
  ],
  "evaluationDetails": {
    "evaluatedAt": "2025-01-01T00:00:00Z",
    "evaluationDurationMs": 45,
    "cacheHit": true
  }
}
```

#### Batch Check Access
```http
POST /checkAccess/batch
Content-Type: application/json

{
  "requests": [
    {
      "requestId": "req-1",
      "principal": {...},
      "resource": {...},
      "action": "..."
    },
    {
      "requestId": "req-2",
      "principal": {...},
      "resource": {...},
      "action": "..."
    }
  ]
}

Response: 200 OK
{
  "responses": [
    {
      "requestId": "req-1",
      "decision": "Allowed",
      "reason": "Success",
      ...
    },
    {
      "requestId": "req-2",
      "decision": "Denied",
      "reason": "ActionNotPermitted",
      ...
    }
  ]
}
```

### 5.2 Authorization Decision Logic

```
1. Resolve principal's effective roles:
   - Direct role assignments to principal
   - Role assignments to principal's groups
   - Inherited assignments from parent scopes

2. For each role assignment:
   a. Check if scope matches or is parent of resource scope
   b. Check if action matches permissions:
      - Is in actions[] or matches wildcard
      - Is NOT in notActions[]
   c. If condition exists, evaluate condition expression

3. Decision:
   - If ANY role allows the action → ALLOWED
   - If NO role allows the action → DENIED
```

## 6. Data Synchronization Design

### 6.1 Data Sync Service Architecture

```
┌─────────────────────────────────────────────────────────┐
│              Data Sync Service                          │
│                                                         │
│  ┌───────────────────────────────────────────────────┐ │
│  │  Change Feed Processor (Role Definitions)         │ │
│  │  - Lease Container: sync-leases-roledefs          │ │
│  │  - Checkpoint: Every 100 items or 5 seconds       │ │
│  └───────────────────────────────────────────────────┘ │
│                      ↓                                  │
│  ┌───────────────────────────────────────────────────┐ │
│  │  Transformation Layer                             │ │
│  │  - No transformation needed (copy as-is)          │ │
│  └───────────────────────────────────────────────────┘ │
│                      ↓                                  │
│  ┌───────────────────────────────────────────────────┐ │
│  │  Data Plane Writer                                │ │
│  │  - Upsert to data plane Cosmos DB                 │ │
│  └───────────────────────────────────────────────────┘ │
│                                                         │
│  ┌───────────────────────────────────────────────────┐ │
│  │  Change Feed Processor (Role Assignments)         │ │
│  │  - Lease Container: sync-leases-roleassignments   │ │
│  │  - Checkpoint: Every 100 items or 5 seconds       │ │
│  └───────────────────────────────────────────────────┘ │
│                      ↓                                  │
│  ┌───────────────────────────────────────────────────┐ │
│  │  Denormalization Engine                           │ │
│  │  - Resolve role definition details                │ │
│  │  - Compute inherited assignments (scope tree)     │ │
│  │  - Build principal → assignments mapping          │ │
│  │  - Build scope → assignments mapping              │ │
│  └───────────────────────────────────────────────────┘ │
│                      ↓                                  │
│  ┌───────────────────────────────────────────────────┐ │
│  │  Data Plane Writer                                │ │
│  │  - Update PrincipalAccessCache                    │ │
│  │  - Update ScopeAccessIndex                        │ │
│  │  - Handle cascade updates                         │ │
│  └───────────────────────────────────────────────────┘ │
│                                                         │
│  ┌───────────────────────────────────────────────────┐ │
│  │  Monitoring & Health                              │ │
│  │  - Lag monitoring (time behind control plane)     │ │
│  │  - Error handling & dead letter queue             │ │
│  │  - Metrics: items/sec, errors, lag time           │ │
│  └───────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### 6.2 Change Feed Processing Details

#### Configuration
```json
{
  "controlPlane": {
    "cosmosEndpoint": "https://control-plane-cosmos.documents.azure.com:443/",
    "database": "authz",
    "containers": {
      "roleDefinitions": "RoleDefinitions",
      "roleAssignments": "RoleAssignments"
    }
  },
  "dataPlane": {
    "cosmosEndpoint": "https://data-plane-cosmos.documents.azure.com:443/",
    "database": "authz-dataplane",
    "containers": {
      "roleDefinitions": "RoleDefinitions",
      "principalAccessCache": "PrincipalAccessCache",
      "scopeAccessIndex": "ScopeAccessIndex"
    }
  },
  "changeFeedProcessor": {
    "maxItemsPerBatch": 100,
    "checkpointFrequency": "5s",
    "startTime": "Beginning | Now | DateTime",
    "instanceName": "sync-worker-1",
    "leaseContainer": "sync-leases"
  }
}
```

#### Processing Logic - Role Definitions

```csharp
async Task ProcessRoleDefinitionChanges(
    IReadOnlyCollection<RoleDefinition> changes,
    CancellationToken cancellationToken)
{
    foreach (var change in changes)
    {
        if (IsDelete(change))
        {
            // Delete from data plane
            await dataPlaneContainer.DeleteItemAsync<RoleDefinition>(
                change.Id,
                new PartitionKey(change.Id));

            // Invalidate all assignments using this role
            await InvalidateAssignmentsForRole(change.Id);
        }
        else
        {
            // Upsert to data plane (no transformation)
            await dataPlaneContainer.UpsertItemAsync(
                change,
                new PartitionKey(change.Id));

            // Refresh assignments using this role
            await RefreshAssignmentsForRole(change.Id);
        }
    }
}
```

#### Processing Logic - Role Assignments

```csharp
async Task ProcessRoleAssignmentChanges(
    IReadOnlyCollection<RoleAssignment> changes,
    CancellationToken cancellationToken)
{
    foreach (var assignment in changes)
    {
        if (IsDelete(assignment))
        {
            await HandleAssignmentDelete(assignment);
        }
        else
        {
            await HandleAssignmentUpsert(assignment);
        }
    }
}

async Task HandleAssignmentUpsert(RoleAssignment assignment)
{
    // 1. Fetch role definition details
    var roleDef = await FetchRoleDefinition(assignment.RoleDefinitionId);

    // 2. Compute scope hierarchy (for inheritance)
    var scopes = ComputeScopeHierarchy(assignment.Scope);
    // e.g., /subscriptions/123/resourceGroups/rg1/providers/Microsoft.Storage/accounts/acc1
    // yields: [/subscriptions/123, /subscriptions/123/resourceGroups/rg1, ...]

    // 3. Update PrincipalAccessCache
    var principalCacheId = $"{assignment.PrincipalId}#{assignment.Scope}";
    var principalCache = await GetOrCreatePrincipalCache(
        assignment.PrincipalId,
        assignment.Scope);

    // Add/update assignment in cache
    principalCache.Assignments.RemoveAll(a => a.AssignmentId == assignment.Id);
    principalCache.Assignments.Add(new CachedAssignment
    {
        RoleDefinitionId = roleDef.Id,
        RoleName = roleDef.Name,
        Permissions = roleDef.Permissions,
        Condition = assignment.Condition,
        AssignmentId = assignment.Id
    });

    principalCache.LastSyncedAt = DateTime.UtcNow;
    principalCache.Version++;

    await dataPlaneContainer.UpsertItemAsync(
        principalCache,
        new PartitionKey(assignment.PrincipalId));

    // 4. Update inherited assignments for child scopes
    await UpdateInheritedAssignments(assignment, roleDef);

    // 5. Update ScopeAccessIndex
    var scopeIndexId = $"{assignment.Scope}#{assignment.RoleDefinitionId}";
    var scopeIndex = await GetOrCreateScopeIndex(
        assignment.Scope,
        assignment.RoleDefinitionId);

    scopeIndex.Principals.RemoveAll(p => p.PrincipalId == assignment.PrincipalId);
    scopeIndex.Principals.Add(new IndexedPrincipal
    {
        PrincipalId = assignment.PrincipalId,
        PrincipalType = assignment.PrincipalType,
        AssignmentId = assignment.Id,
        Condition = assignment.Condition
    });

    scopeIndex.LastSyncedAt = DateTime.UtcNow;

    await dataPlaneContainer.UpsertItemAsync(
        scopeIndex,
        new PartitionKey(assignment.Scope));
}

async Task HandleAssignmentDelete(RoleAssignment assignment)
{
    // 1. Remove from PrincipalAccessCache
    var principalCacheId = $"{assignment.PrincipalId}#{assignment.Scope}";
    var principalCache = await GetPrincipalCache(
        assignment.PrincipalId,
        assignment.Scope);

    if (principalCache != null)
    {
        principalCache.Assignments.RemoveAll(a => a.AssignmentId == assignment.Id);
        principalCache.LastSyncedAt = DateTime.UtcNow;
        principalCache.Version++;

        if (principalCache.Assignments.Count == 0 &&
            principalCache.InheritedAssignments.Count == 0)
        {
            // Delete cache entry if no assignments left
            await dataPlaneContainer.DeleteItemAsync<PrincipalAccessCache>(
                principalCacheId,
                new PartitionKey(assignment.PrincipalId));
        }
        else
        {
            await dataPlaneContainer.UpsertItemAsync(
                principalCache,
                new PartitionKey(assignment.PrincipalId));
        }
    }

    // 2. Remove from ScopeAccessIndex
    var scopeIndexId = $"{assignment.Scope}#{assignment.RoleDefinitionId}";
    var scopeIndex = await GetScopeIndex(assignment.Scope, assignment.RoleDefinitionId);

    if (scopeIndex != null)
    {
        scopeIndex.Principals.RemoveAll(p => p.PrincipalId == assignment.PrincipalId);
        scopeIndex.LastSyncedAt = DateTime.UtcNow;

        if (scopeIndex.Principals.Count == 0)
        {
            await dataPlaneContainer.DeleteItemAsync<ScopeAccessIndex>(
                scopeIndexId,
                new PartitionKey(assignment.Scope));
        }
        else
        {
            await dataPlaneContainer.UpsertItemAsync(
                scopeIndex,
                new PartitionKey(assignment.Scope));
        }
    }

    // 3. Update inherited assignments for child scopes
    await RemoveInheritedAssignments(assignment);
}
```

### 6.3 Scope Hierarchy and Inheritance

Azure RBAC supports inheritance where assignments at parent scopes apply to child scopes:

```
/subscriptions/sub-123                          ← Assignment here applies to everything below
  /resourceGroups/rg-1                          ← Assignment here applies to resources in rg-1
    /providers/Microsoft.Storage/accounts/acc1  ← Most specific scope
      /blobServices/default
        /containers/container1
```

**Inheritance Processing:**

```csharp
List<string> ComputeScopeHierarchy(string scope)
{
    // Example: /subscriptions/123/resourceGroups/rg1/providers/Microsoft.Storage/accounts/acc1
    var parts = scope.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var hierarchy = new List<string>();

    for (int i = 0; i < parts.Length; i += 2)
    {
        var currentScope = "/" + string.Join("/", parts.Take(i + 2));
        hierarchy.Add(currentScope);
    }

    return hierarchy;
    // Returns: [
    //   /subscriptions/123,
    //   /subscriptions/123/resourceGroups/rg1,
    //   /subscriptions/123/resourceGroups/rg1/providers/Microsoft.Storage,
    //   /subscriptions/123/resourceGroups/rg1/providers/Microsoft.Storage/accounts/acc1
    // ]
}

async Task UpdateInheritedAssignments(RoleAssignment assignment, RoleDefinition roleDef)
{
    // For a new assignment at scope S, update all principals who have access to child scopes
    var childScopes = await FindChildScopes(assignment.Scope);

    foreach (var childScope in childScopes)
    {
        // Update PrincipalAccessCache for this child scope
        var principalCacheId = $"{assignment.PrincipalId}#{childScope}";
        var cache = await GetOrCreatePrincipalCache(assignment.PrincipalId, childScope);

        // Add to inherited assignments
        cache.InheritedAssignments.RemoveAll(a =>
            a.AssignmentId == assignment.Id);
        cache.InheritedAssignments.Add(new InheritedAssignment
        {
            RoleDefinitionId = roleDef.Id,
            RoleName = roleDef.Name,
            Scope = assignment.Scope,
            Permissions = roleDef.Permissions,
            AssignmentId = assignment.Id
        });

        cache.LastSyncedAt = DateTime.UtcNow;
        cache.Version++;

        await dataPlaneContainer.UpsertItemAsync(cache,
            new PartitionKey(assignment.PrincipalId));
    }
}
```

### 6.4 Error Handling and Retry Strategy

```csharp
public class ChangeFeedErrorHandler
{
    private readonly IDeadLetterQueue deadLetterQueue;

    public async Task<bool> HandleError(
        Exception ex,
        object change,
        int retryCount)
    {
        if (retryCount < 3)
        {
            // Transient errors: retry with exponential backoff
            if (IsTransientError(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                return true; // Retry
            }
        }

        // Non-transient or max retries exceeded
        await deadLetterQueue.EnqueueAsync(new DeadLetterItem
        {
            Change = change,
            Error = ex.ToString(),
            Timestamp = DateTime.UtcNow,
            RetryCount = retryCount
        });

        // Log alert
        logger.LogError(ex, "Failed to process change after {RetryCount} retries", retryCount);

        return false; // Don't retry, move to next item
    }

    bool IsTransientError(Exception ex)
    {
        return ex is CosmosException cosmosEx &&
               (cosmosEx.StatusCode == HttpStatusCode.TooManyRequests ||
                cosmosEx.StatusCode == HttpStatusCode.ServiceUnavailable ||
                cosmosEx.StatusCode == HttpStatusCode.RequestTimeout);
    }
}
```

### 6.5 Monitoring and Metrics

**Key Metrics:**
- **Sync Lag**: Time difference between control plane write and data plane availability
- **Throughput**: Items processed per second
- **Error Rate**: Failed items / total items
- **Cache Invalidation Rate**: How often caches are invalidated due to upstream changes

```csharp
public class SyncMetrics
{
    public TimeSpan CurrentLag { get; set; }
    public long ItemsProcessedPerSecond { get; set; }
    public double ErrorRate { get; set; }
    public int ActiveLeases { get; set; }
    public DateTime LastCheckpointTime { get; set; }

    // Per-container metrics
    public Dictionary<string, ContainerSyncMetrics> ContainerMetrics { get; set; }
}

public class ContainerSyncMetrics
{
    public long TotalItemsProcessed { get; set; }
    public long TotalErrors { get; set; }
    public TimeSpan AverageLag { get; set; }
    public DateTime LastProcessedTimestamp { get; set; }
}
```

## 7. Storage Design

### 7.1 Control Plane Cosmos DB

**Database**: `authz`

**Containers:**

| Container | Partition Key | Indexing Policy | Throughput |
|-----------|---------------|-----------------|------------|
| RoleDefinitions | `/id` | Default (index all) | 400 RU/s autoscale |
| RoleAssignments | `/scope` | Index: principalId, roleDefinitionId | 1000 RU/s autoscale |

**Change Feed Configuration:**
- Enabled on both containers
- Retention: 7 days (for disaster recovery)
- Mode: Latest version

### 7.2 Data Plane Cosmos DB

**Database**: `authz-dataplane`

**Containers:**

| Container | Partition Key | Indexing Policy | Throughput |
|-----------|---------------|-----------------|------------|
| RoleDefinitions | `/id` | Default | 400 RU/s autoscale |
| PrincipalAccessCache | `/principalId` | Index: scope, roleDefinitionId, lastSyncedAt | 10,000 RU/s autoscale |
| ScopeAccessIndex | `/scope` | Index: roleDefinitionId, principals/principalId | 5,000 RU/s autoscale |

**TTL Configuration:**
- All containers: TTL enabled (default: -1, never expire)
- Option to set TTL on specific items for cache eviction

### 7.3 Index Policies

#### PrincipalAccessCache
```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    {
      "path": "/principalId/?"
    },
    {
      "path": "/scope/?"
    },
    {
      "path": "/assignments/*/roleDefinitionId/?"
    },
    {
      "path": "/lastSyncedAt/?"
    }
  ],
  "excludedPaths": [
    {
      "path": "/assignments/*/permissions/*"
    }
  ]
}
```

#### ScopeAccessIndex
```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    {
      "path": "/scope/?"
    },
    {
      "path": "/roleDefinitionId/?"
    },
    {
      "path": "/principals/*/principalId/?"
    }
  ],
  "excludedPaths": []
}
```

## 8. Performance Considerations

### 8.1 Data Plane Optimization

**Query Patterns:**

1. **Principal-based lookup** (most common):
   ```sql
   SELECT * FROM c
   WHERE c.principalId = @principalId
   ```
   - Partition key query → low latency (~5-10ms)
   - May need to query multiple partitions for group memberships

2. **Scope-based lookup**:
   ```sql
   SELECT * FROM c
   WHERE c.scope = @scope
   ```
   - Cross-partition query if checking multiple scopes
   - Consider caching scope hierarchies

**Caching Strategy:**
- In-memory cache at API layer for frequently accessed principals
- Cache TTL: 60 seconds (balance freshness vs performance)
- Cache invalidation on sync events (via event notification)

### 8.2 Sync Performance

**Throughput Targets:**
- Process 1000+ changes per second
- Sync lag: < 5 seconds (p99)

**Optimization Techniques:**
- Parallel processing across partitions
- Batch upserts to data plane (up to 100 items)
- Skip no-op updates (compare _etag)

## 9. Security Considerations

### 9.1 Authentication & Authorization

**Control Plane:**
- Requires admin-level authentication (AAD, OAuth 2.0)
- RBAC on the RBAC system itself (meta-authorization)
- Audit logging for all changes

**Data Plane:**
- Service-to-service authentication
- Rate limiting per caller
- No sensitive data exposure in responses

### 9.2 Data Protection

- Encryption at rest (Cosmos DB default)
- Encryption in transit (TLS 1.2+)
- No PII in role definitions/assignments (use IDs only)

### 9.3 Condition Evaluation Security

- Sandbox condition evaluation to prevent code injection
- Limit expression complexity (max depth, operations)
- Timeout condition evaluation (max 100ms)

## 10. Disaster Recovery

### 10.1 Backup Strategy

**Control Plane:**
- Cosmos DB continuous backup (30 days)
- Point-in-time restore capability

**Data Plane:**
- Can be rebuilt from control plane via full sync
- Continuous backup for faster recovery

### 10.2 Full Resync Process

```csharp
async Task PerformFullResync()
{
    // 1. Clear data plane containers (or create new ones)
    await TruncateDataPlaneContainers();

    // 2. Copy all role definitions
    await CopyAllRoleDefinitions();

    // 3. Process all role assignments
    var assignments = await FetchAllRoleAssignments();

    foreach (var assignment in assignments)
    {
        await HandleAssignmentUpsert(assignment);
    }

    // 4. Verify sync completion
    await VerifySyncIntegrity();
}
```

## 11. Deployment Architecture

### 11.1 Component Deployment

```
Azure Region 1 (Primary)
├── Control Plane
│   ├── App Service (2+ instances)
│   ├── Cosmos DB (multi-region write enabled)
│   └── Application Insights
├── Data Sync Service
│   ├── Container Apps / AKS (3+ instances)
│   ├── Dead Letter Queue (Service Bus)
│   └── Application Insights
└── Data Plane
    ├── App Service / Container Apps (10+ instances, autoscale)
    ├── Cosmos DB (read replicas)
    ├── Redis Cache (optional)
    └── Application Insights

Azure Region 2 (Secondary)
├── Data Plane (read-only)
│   ├── App Service (5+ instances)
│   └── Cosmos DB (read replica)
└── Data Sync Service (standby)
```

### 11.2 Scaling Strategy

**Control Plane:**
- Scale: 2-5 instances (relatively low traffic)
- Cosmos DB: 400-2000 RU/s

**Data Sync:**
- Scale: Number of instances = Number of lease partitions
- Recommended: 3-10 instances for parallel processing

**Data Plane:**
- Scale: Auto-scale based on request rate (target: 70% CPU)
- Cosmos DB: 10,000+ RU/s (based on CheckAccess load)

## 12. API Rate Limiting

### 12.1 Control Plane
- 100 requests/minute per client (writes)
- 1000 requests/minute per client (reads)

### 12.2 Data Plane
- 10,000 requests/second per instance
- No per-client limit (trusted internal service)

## 13. Future Enhancements

1. **Attribute-Based Access Control (ABAC)**
   - Extend beyond role-based to attribute-based
   - Dynamic policy evaluation

2. **Policy Analytics**
   - Access patterns analysis
   - Unused role detection
   - Privilege escalation detection

3. **Multi-region Active-Active**
   - Support writes to both control planes
   - Conflict resolution strategy

4. **GraphQL API**
   - Alternative to REST for complex queries
   - Efficient data fetching

5. **Real-time Sync**
   - Use Event Grid/Service Bus for near-instant sync
   - Reduce lag to < 1 second

## 14. Appendix

### 14.1 Sample Permission Evaluation

```
Principal: user@example.com (groups: [engineering-group])
Resource: /subscriptions/123/resourceGroups/rg1/providers/Microsoft.Storage/accounts/acc1
Action: Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write
ActionType: dataAction

Assignments:
1. Scope: /subscriptions/123
   Role: Reader
   Permissions: { actions: ["*/read"], dataActions: [] }
   → Does not grant the requested action

2. Scope: /subscriptions/123/resourceGroups/rg1
   Role: Storage Blob Data Contributor
   Permissions: {
     actions: [],
     dataActions: ["Microsoft.Storage/storageAccounts/blobServices/containers/blobs/*"]
   }
   Condition: @Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'acc1'
   → Grants the action IF condition passes

Evaluation:
- Check action match: "Microsoft.Storage/.../blobs/write" matches "Microsoft.Storage/.../blobs/*" ✓
- Check condition: containerName from request attributes = 'acc1' ✓
- Decision: ALLOWED
```

### 14.2 Glossary

- **CDC**: Change Data Capture
- **RU**: Request Unit (Cosmos DB throughput measure)
- **TTL**: Time To Live
- **AAD**: Azure Active Directory
- **ABAC**: Attribute-Based Access Control
- **RBAC**: Role-Based Access Control

---

**Document Version**: 1.0
**Last Updated**: 2025-01-01
**Author**: System Architecture Team
