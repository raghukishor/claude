# RBAC Authorization System Design

## 1. Overview

This document describes the design of a Role-Based Access Control (RBAC) authorization system modeled after Azure RBAC. The system enables fine-grained access control through role assignments that bind security principals to permissions at specific scopes.

### 1.1 Goals

- **Scalability**: Data plane must handle 100K+ authorization checks per second
- **Low Latency**: P99 latency < 50ms for CheckAccess calls
- **Availability**: 99.99% uptime for authorization decisions
- **Consistency**: Eventual consistency with < 10 second propagation delay
- **Flexibility**: Support for custom roles, conditions, and hierarchical scopes

### 1.2 Non-Goals

- Real-time synchronization (eventual consistency is acceptable)
- Multi-tenant isolation at storage level (handled at application layer)
- Identity management (principals come from external identity providers)

---

## 2. Core Concepts

### 2.1 Security Principal

An entity that can be granted access:

| Type | Description | Example |
|------|-------------|---------|
| User | Individual human identity | `user:alice@contoso.com` |
| Group | Collection of users | `group:engineering-team` |
| ServicePrincipal | Application identity | `sp:api-service-prod` |
| ManagedIdentity | Azure-managed identity | `mi:vm-identity-123` |

### 2.2 Scope

A scope defines the boundary where access applies. Scopes form a hierarchy:

```
/tenants/{tenantId}
└── /subscriptions/{subscriptionId}
    └── /resourceGroups/{resourceGroupName}
        └── /providers/{resourceProvider}/{resourceType}/{resourceName}
```

**Key Property**: Permissions granted at a parent scope are inherited by all child scopes.

### 2.3 Role Definition

A role definition is a collection of permissions:

```
Role: "Storage Data Reader"
├── Actions (control plane operations)
│   ├── Microsoft.Storage/storageAccounts/read
│   └── Microsoft.Storage/storageAccounts/listKeys/action
├── NotActions (excluded control plane operations)
│   └── (none)
├── DataActions (data plane operations)
│   └── Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read
└── NotDataActions (excluded data plane operations)
    └── (none)
```

**Permission Matching**: Uses wildcard patterns. `Microsoft.Storage/*/read` matches any read operation under Storage.

### 2.4 Role Assignment

A role assignment binds the three elements together:

```
WHO:   Principal (user:alice@contoso.com)
WHAT:  Role Definition (Storage Data Reader)
WHERE: Scope (/subscriptions/sub-123/resourceGroups/rg-prod)
```

Optional: **Conditions** add attribute-based constraints (e.g., "only if blob tag 'project' equals 'alpha'").

---

## 3. System Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                           CONTROL PLANE                                 │
│                                                                         │
│   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐                │
│   │   Role      │    │   Role      │    │   Scope     │                │
│   │ Definition  │    │ Assignment  │    │  Registry   │                │
│   │    API      │    │    API      │    │    API      │                │
│   └──────┬──────┘    └──────┬──────┘    └──────┬──────┘                │
│          │                  │                  │                        │
│          └──────────────────┼──────────────────┘                        │
│                             ▼                                           │
│                  ┌─────────────────────┐                               │
│                  │    CosmosDB         │                               │
│                  │  (Primary Store)    │                               │
│                  │                     │                               │
│                  │  • RoleDefinitions  │                               │
│                  │  • RoleAssignments  │                               │
│                  │  • Scopes           │                               │
│                  │  [Change Feed ON]   │                               │
│                  └──────────┬──────────┘                               │
└─────────────────────────────┼──────────────────────────────────────────┘
                              │
                              │ Change Feed (CDC)
                              ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        DATA SYNC SERVICE                                │
│                                                                         │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                   Change Feed Processors                         │  │
│   │                                                                  │  │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │  │
│   │  │ RoleDef      │  │ Assignment   │  │ Scope        │          │  │
│   │  │ Processor    │  │ Processor    │  │ Processor    │          │  │
│   │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │  │
│   └─────────┼─────────────────┼─────────────────┼────────────────────┘  │
│             │                 │                 │                       │
│             └─────────────────┼─────────────────┘                       │
│                               ▼                                         │
│                  ┌─────────────────────┐                               │
│                  │  Denormalization    │                               │
│                  │      Engine         │                               │
│                  └──────────┬──────────┘                               │
│                             │                                          │
│                             ▼                                          │
│                  ┌─────────────────────┐                               │
│                  │   Batch Writer      │                               │
│                  └──────────┬──────────┘                               │
└─────────────────────────────┼──────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────────┐
│                           DATA PLANE                                    │
│                                                                         │
│                  ┌─────────────────────┐                               │
│                  │    CosmosDB         │                               │
│                  │  (Read-Optimized)   │                               │
│                  │                     │                               │
│                  │ • EffectiveAccess   │◄───┐                          │
│                  │ • RoleDefinitions   │    │                          │
│                  └──────────┬──────────┘    │                          │
│                             │               │                          │
│                             ▼               │                          │
│   ┌─────────────────────────────────────────┼───────────────────────┐  │
│   │              CheckAccess API            │                        │  │
│   │                                         │                        │  │
│   │  ┌──────────────┐    ┌──────────────┐  │  ┌──────────────┐     │  │
│   │  │   Request    │───▶│  Permission  │──┴─▶│   Response   │     │  │
│   │  │   Handler    │    │  Evaluator   │     │   Builder    │     │  │
│   │  └──────────────┘    └──────────────┘     └──────────────┘     │  │
│   └─────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Data Models

### 4.1 Control Plane Models

#### RoleDefinition

```json
{
  "id": "rd-550e8400-e29b-41d4-a716-446655440000",
  "name": "Storage Blob Reader",
  "description": "Read access to storage blobs",
  "type": "BuiltIn",
  "permissions": [
    {
      "actions": ["Microsoft.Storage/storageAccounts/read"],
      "notActions": [],
      "dataActions": [
        "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read"
      ],
      "notDataActions": []
    }
  ],
  "assignableScopes": ["/"],

  "_version": {
    "sequenceNumber": 5,
    "timestamp": "2025-01-15T10:30:00.123456Z",
    "changeId": "chg-a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  },

  "_lifecycle": {
    "state": "Active",
    "isDeleted": false,
    "deletedAt": null,
    "ttl": -1
  },

  "_metadata": {
    "createdBy": "system",
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedBy": "admin@contoso.com",
    "updatedAt": "2025-01-15T10:30:00Z"
  },

  "_etag": "\"00000000-0000-0000-0000-000000000001\""
}
```

**Storage**: Container `RoleDefinitions`, Partition Key: `/id`

**Version Tracking Fields**:

| Field | Purpose |
|-------|---------|
| `_version.sequenceNumber` | Monotonically increasing version, incremented on every write |
| `_version.timestamp` | High-precision timestamp of last modification |
| `_version.changeId` | Unique identifier for this specific change (idempotency key) |
| `_lifecycle.state` | Document state: `Active`, `Deleted`, `PendingDelete` |
| `_lifecycle.isDeleted` | Soft delete flag for change feed visibility |
| `_lifecycle.ttl` | TTL in seconds (-1 = never expire, set during deletion) |
| `_etag` | CosmosDB optimistic concurrency token |

#### RoleAssignment

```json
{
  "id": "ra-123e4567-e89b-12d3-a456-426614174000",
  "principalId": "user-abc-123",
  "principalType": "User",
  "roleDefinitionId": "rd-550e8400-e29b-41d4-a716-446655440000",
  "scope": "/subscriptions/sub-001/resourceGroups/rg-prod",
  "condition": {
    "expression": "@Resource[Microsoft.Storage/storageAccounts/blobServices/containers:name] StringEquals 'public'",
    "version": "2.0"
  },

  "_version": {
    "sequenceNumber": 1,
    "timestamp": "2025-01-15T10:30:00.456789Z",
    "changeId": "chg-b2c3d4e5-f6a7-8901-bcde-f23456789012"
  },

  "_lifecycle": {
    "state": "Active",
    "isDeleted": false,
    "deletedAt": null,
    "ttl": -1
  },

  "_references": {
    "roleDefinitionVersion": 5,
    "roleDefinitionChangeId": "chg-a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  },

  "_metadata": {
    "createdBy": "admin@contoso.com",
    "createdAt": "2025-01-15T10:30:00Z",
    "updatedBy": "admin@contoso.com",
    "updatedAt": "2025-01-15T10:30:00Z",
    "description": "Grant blob read access to production resources"
  },

  "_etag": "\"00000000-0000-0000-0000-000000000002\""
}
```

**Storage**: Container `RoleAssignments`, Partition Key: `/scope`

**Reference Tracking**: The `_references` block tracks the version of dependent entities at the time of creation/update. This enables the sync service to detect when a role assignment needs reprocessing due to changes in the underlying role definition.

#### Scope (Optional - for scope registry)

```json
{
  "id": "/subscriptions/sub-001/resourceGroups/rg-prod",
  "parentScope": "/subscriptions/sub-001",
  "resourceType": "resourceGroup",
  "displayName": "Production Resource Group",
  "metadata": {
    "createdAt": "2025-01-01T00:00:00Z"
  }
}
```

**Storage**: Container `Scopes`, Partition Key: `/parentScope`

---

### 4.2 Data Plane Models

The data plane uses a denormalized model optimized for the CheckAccess query pattern: "Given a principal and an action at a scope, is access allowed?"

#### EffectiveAccess

This is the primary lookup table. One document per principal-scope combination.

```json
{
  "id": "ea-{principalId}-{scopeHash}",
  "principalId": "user-abc-123",
  "scope": "/subscriptions/sub-001/resourceGroups/rg-prod",
  "scopeHash": "a1b2c3d4",
  "scopeDepth": 2,

  "effectiveRoles": [
    {
      "roleDefinitionId": "rd-550e8400-e29b-41d4-a716-446655440000",
      "roleName": "Storage Blob Reader",
      "assignmentId": "ra-123e4567-e89b-12d3-a456-426614174000",
      "assignmentScope": "/subscriptions/sub-001/resourceGroups/rg-prod",
      "inherited": false,
      "condition": {
        "expression": "@Resource[...]:name] StringEquals 'public'",
        "version": "2.0"
      },
      "permissions": {
        "actions": ["Microsoft.Storage/storageAccounts/read"],
        "notActions": [],
        "dataActions": ["Microsoft.Storage/.../blobs/read"],
        "notDataActions": []
      },

      "_sourceVersions": {
        "assignmentSequence": 1,
        "assignmentChangeId": "chg-b2c3d4e5-f6a7-8901-bcde-f23456789012",
        "roleDefSequence": 5,
        "roleDefChangeId": "chg-a1b2c3d4-e5f6-7890-abcd-ef1234567890"
      }
    },
    {
      "roleDefinitionId": "rd-builtin-reader",
      "roleName": "Reader",
      "assignmentId": "ra-inherited-001",
      "assignmentScope": "/subscriptions/sub-001",
      "inherited": true,
      "condition": null,
      "permissions": {
        "actions": ["*/read"],
        "notActions": [],
        "dataActions": [],
        "notDataActions": []
      },

      "_sourceVersions": {
        "assignmentSequence": 3,
        "assignmentChangeId": "chg-c3d4e5f6-a7b8-9012-cdef-345678901234",
        "roleDefSequence": 1,
        "roleDefChangeId": "chg-d4e5f6a7-b8c9-0123-def0-456789012345"
      }
    }
  ],

  "groupMemberships": ["group-engineering", "group-all-employees"],

  "_sync": {
    "documentVersion": 7,
    "lastProcessedChangeIds": [
      "chg-b2c3d4e5-f6a7-8901-bcde-f23456789012",
      "chg-c3d4e5f6-a7b8-9012-cdef-345678901234"
    ],
    "lastSyncedAt": "2025-01-15T10:35:00.789Z",
    "syncBatchId": "batch-20250115-103500-worker3",
    "syncMode": "Incremental",
    "sourceCheckpoint": {
      "roleDefinitionsLsn": 42,
      "roleAssignmentsLsn": 108
    }
  },

  "_watermarks": {
    "highWatermark": "2025-01-15T10:35:00.789Z",
    "roleDefHighWatermark": "2025-01-15T10:30:00.123Z",
    "assignmentHighWatermark": "2025-01-15T10:35:00.456Z"
  },

  "_etag": "\"00000000-0000-0000-0000-000000000007\""
}
```

**Storage**: Container `EffectiveAccess`, Partition Key: `/principalId`

**Sync Tracking Fields**:

| Field | Purpose |
|-------|---------|
| `effectiveRoles[]._sourceVersions` | Tracks the exact version of source documents that produced this role entry |
| `_sync.documentVersion` | Local version counter for this document, incremented on every update |
| `_sync.lastProcessedChangeIds` | List of change IDs already applied (for deduplication) |
| `_sync.syncBatchId` | Identifies which sync batch last modified this document |
| `_sync.syncMode` | `Bootstrap` or `Incremental` - indicates how data was populated |
| `_sync.sourceCheckpoint` | LSN positions in source containers at time of sync |
| `_watermarks` | High-water timestamps for detecting stale data |

**Key Design Decisions**:

1. **Pre-computed inheritance**: Inherited roles from parent scopes are already resolved and included
2. **Embedded permissions**: Role definition permissions are copied into the document to avoid joins
3. **Group expansion**: Principal's group memberships are stored for group-based lookups
4. **Scope depth**: Enables efficient queries for "all access at or below this scope"
5. **Per-role versioning**: Each effective role tracks its source versions independently, enabling surgical updates
6. **Change ID tracking**: Enables idempotent replay detection at the document level

---

## 5. Control Plane APIs

### 5.1 Role Definition APIs

#### Create Role Definition

```http
POST /api/v1/roleDefinitions
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Custom Blob Manager",
  "description": "Manage blobs in specific containers",
  "permissions": [
    {
      "actions": ["Microsoft.Storage/storageAccounts/read"],
      "dataActions": [
        "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/*"
      ],
      "notDataActions": [
        "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/delete"
      ]
    }
  ],
  "assignableScopes": ["/subscriptions/sub-001"]
}
```

**Response** `201 Created`:
```json
{
  "id": "rd-new-guid",
  "name": "Custom Blob Manager",
  ...
}
```

#### Get Role Definition

```http
GET /api/v1/roleDefinitions/{roleDefinitionId}
```

#### List Role Definitions

```http
GET /api/v1/roleDefinitions?assignableScope=/subscriptions/sub-001&type=Custom
```

#### Update Role Definition

```http
PUT /api/v1/roleDefinitions/{roleDefinitionId}
If-Match: "{etag}"
```

#### Delete Role Definition

```http
DELETE /api/v1/roleDefinitions/{roleDefinitionId}
```

Returns `409 Conflict` if role is currently assigned.

---

### 5.2 Role Assignment APIs

#### Create Role Assignment

```http
POST /api/v1/scopes/{scope}/roleAssignments
Content-Type: application/json

{
  "principalId": "user-abc-123",
  "principalType": "User",
  "roleDefinitionId": "rd-550e8400-e29b-41d4-a716-446655440000",
  "condition": {
    "expression": "@Resource[Microsoft.Storage/...]:name] StringEquals 'public'",
    "version": "2.0"
  }
}
```

**Validation Rules**:
- Principal must exist (optional - depends on integration)
- Role definition must exist
- Scope must be within role's assignable scopes
- Caller must have permission to assign roles at this scope
- No duplicate assignment (same principal + role + scope)

#### List Role Assignments

```http
GET /api/v1/scopes/{scope}/roleAssignments?principalId={principalId}&includeInherited=true
```

Query parameters:
- `principalId`: Filter by principal
- `roleDefinitionId`: Filter by role
- `includeInherited`: Include assignments from parent scopes

#### Delete Role Assignment

```http
DELETE /api/v1/scopes/{scope}/roleAssignments/{assignmentId}
```

---

### 5.3 API Response Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created |
| 204 | Deleted (no content) |
| 400 | Bad request (validation error) |
| 401 | Unauthorized |
| 403 | Forbidden (no permission) |
| 404 | Not found |
| 409 | Conflict (duplicate, in use) |
| 412 | Precondition failed (etag mismatch) |
| 429 | Rate limited |

---

## 6. Data Plane API

### 6.1 CheckAccess

The primary authorization endpoint.

```http
POST /api/v1/checkAccess
Content-Type: application/json

{
  "subject": {
    "principalId": "user-abc-123",
    "principalType": "User",
    "groups": ["group-engineering", "group-all-employees"]
  },
  "resource": {
    "scope": "/subscriptions/sub-001/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/mystorage",
    "type": "Microsoft.Storage/storageAccounts",
    "attributes": {
      "containerName": "public",
      "blobPath": "data/file.txt"
    }
  },
  "action": {
    "name": "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read",
    "type": "DataAction"
  }
}
```

**Response** `200 OK`:

```json
{
  "decision": "Allow",
  "decidedAt": "2025-01-15T10:35:00.123Z",
  "reason": {
    "code": "RoleAssignmentMatch",
    "message": "Access granted by role 'Storage Blob Reader' at scope '/subscriptions/sub-001/resourceGroups/rg-prod'"
  },
  "matchedAssignments": [
    {
      "assignmentId": "ra-123e4567-e89b-12d3-a456-426614174000",
      "roleDefinitionId": "rd-550e8400-e29b-41d4-a716-446655440000",
      "roleName": "Storage Blob Reader",
      "scope": "/subscriptions/sub-001/resourceGroups/rg-prod",
      "matchedPermission": "Microsoft.Storage/.../blobs/read"
    }
  ],
  "evaluationDetails": {
    "principalResolved": true,
    "groupsEvaluated": 2,
    "rolesEvaluated": 3,
    "conditionsEvaluated": 1,
    "durationMs": 12
  }
}
```

**Denial Response**:

```json
{
  "decision": "Deny",
  "decidedAt": "2025-01-15T10:35:00.456Z",
  "reason": {
    "code": "NoMatchingRoleAssignment",
    "message": "No role assignment grants the requested permission"
  },
  "matchedAssignments": [],
  "evaluationDetails": {
    "principalResolved": true,
    "groupsEvaluated": 2,
    "rolesEvaluated": 0,
    "conditionsEvaluated": 0,
    "durationMs": 8
  }
}
```

### 6.2 Batch CheckAccess

For multiple authorization checks in a single request:

```http
POST /api/v1/checkAccess/batch
Content-Type: application/json

{
  "requests": [
    {
      "id": "req-1",
      "subject": { ... },
      "resource": { ... },
      "action": { ... }
    },
    {
      "id": "req-2",
      "subject": { ... },
      "resource": { ... },
      "action": { ... }
    }
  ]
}
```

**Response**:

```json
{
  "responses": [
    { "id": "req-1", "decision": "Allow", ... },
    { "id": "req-2", "decision": "Deny", ... }
  ],
  "batchMetadata": {
    "totalRequests": 2,
    "allowed": 1,
    "denied": 1,
    "totalDurationMs": 25
  }
}
```

### 6.3 Decision Reasons

| Code | Description |
|------|-------------|
| `RoleAssignmentMatch` | Permission granted by a role assignment |
| `NoMatchingRoleAssignment` | No assignment grants the permission |
| `ActionExcluded` | Action in notActions/notDataActions |
| `ConditionNotSatisfied` | Role matched but condition evaluated false |
| `PrincipalNotFound` | Principal has no access records |
| `InvalidRequest` | Malformed request |

---

## 7. Authorization Decision Algorithm

```
FUNCTION CheckAccess(subject, resource, action):

    # Step 1: Resolve all principals to check
    principals = [subject.principalId] + subject.groups

    # Step 2: Find all relevant scopes (resource scope and ancestors)
    scopes = GetScopeHierarchy(resource.scope)
    # e.g., ["/subscriptions/sub-001/resourceGroups/rg-prod/...",
    #        "/subscriptions/sub-001/resourceGroups/rg-prod",
    #        "/subscriptions/sub-001",
    #        "/"]

    # Step 3: Gather all effective roles for these principals at these scopes
    effectiveRoles = []
    FOR EACH principal IN principals:
        FOR EACH scope IN scopes:
            roles = LookupEffectiveAccess(principal, scope)
            effectiveRoles.append(roles)

    # Step 4: Evaluate each role
    FOR EACH role IN effectiveRoles:

        # 4a: Check if action matches permissions
        IF action.type == "Action":
            permitted = MatchesWildcard(action.name, role.permissions.actions)
            excluded = MatchesWildcard(action.name, role.permissions.notActions)
        ELSE:  # DataAction
            permitted = MatchesWildcard(action.name, role.permissions.dataActions)
            excluded = MatchesWildcard(action.name, role.permissions.notDataActions)

        IF permitted AND NOT excluded:

            # 4b: Evaluate condition if present
            IF role.condition IS NOT NULL:
                IF EvaluateCondition(role.condition, resource.attributes):
                    RETURN Allow(role)
            ELSE:
                RETURN Allow(role)

    # Step 5: No matching permission found
    RETURN Deny("NoMatchingRoleAssignment")
```

### 7.1 Wildcard Matching

```
FUNCTION MatchesWildcard(action, patterns):
    FOR EACH pattern IN patterns:
        IF pattern == "*":
            RETURN true
        IF pattern ends with "/*":
            prefix = pattern[:-2]
            IF action starts with prefix:
                RETURN true
        IF pattern ends with "*":
            prefix = pattern[:-1]
            IF action starts with prefix:
                RETURN true
        IF pattern == action:
            RETURN true
    RETURN false
```

Examples:
- `*` matches everything
- `Microsoft.Storage/*` matches `Microsoft.Storage/storageAccounts/read`
- `*/read` matches `Microsoft.Compute/virtualMachines/read`
- `Microsoft.Storage/storageAccounts/*/read` matches `Microsoft.Storage/storageAccounts/blobServices/read`

---

## 8. Data Synchronization Service

### 8.1 Architecture Overview

The sync service reads from CosmosDB Change Feed and writes denormalized data to the data plane store.

```
┌─────────────────────────────────────────────────────────────────┐
│                    Data Sync Service                             │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                  Lease Manager                              │ │
│  │  • Distributes partitions across workers                   │ │
│  │  • Tracks checkpoints per partition                        │ │
│  │  • Handles worker failures and rebalancing                 │ │
│  └────────────────────────────────────────────────────────────┘ │
│                              │                                   │
│              ┌───────────────┼───────────────┐                  │
│              ▼               ▼               ▼                  │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐│
│  │ Worker 1         │ │ Worker 2         │ │ Worker N         ││
│  │ Partitions: 0-3  │ │ Partitions: 4-7  │ │ Partitions: 8-11 ││
│  └────────┬─────────┘ └────────┬─────────┘ └────────┬─────────┘│
│           │                    │                    │           │
│           └────────────────────┼────────────────────┘           │
│                                ▼                                │
│  ┌────────────────────────────────────────────────────────────┐│
│  │                 Change Processor Pipeline                   ││
│  │                                                             ││
│  │  [Read Changes] → [Classify] → [Transform] → [Write Batch] ││
│  │                                                             ││
│  └────────────────────────────────────────────────────────────┘│
│                                │                                │
│                                ▼                                │
│  ┌────────────────────────────────────────────────────────────┐│
│  │                   Error Handler                             ││
│  │  • Retry transient failures (3x with backoff)              ││
│  │  • Dead letter queue for persistent failures               ││
│  │  • Alert on error rate threshold                           ││
│  └────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### 8.2 Change Classification

Each change from the control plane is classified:

| Source Container | Change Type | Processing Action |
|-----------------|-------------|-------------------|
| RoleDefinitions | Insert/Update | Copy to data plane + update all affected EffectiveAccess docs |
| RoleDefinitions | Delete | Remove from data plane + update all affected EffectiveAccess docs |
| RoleAssignments | Insert | Create/update EffectiveAccess for principal at scope + propagate to child scopes |
| RoleAssignments | Update | Update EffectiveAccess docs, re-evaluate conditions |
| RoleAssignments | Delete | Remove from EffectiveAccess docs + clean up child scope propagation |

### 8.3 Processing Pipeline

#### Step 1: Read Changes

```csharp
public async Task ProcessChangesAsync(
    IReadOnlyCollection<Document> changes,
    string leaseToken,
    CancellationToken cancellation)
{
    var batch = new List<SyncOperation>();

    foreach (var change in changes)
    {
        var operation = ClassifyChange(change);
        batch.Add(operation);
    }

    await ProcessBatchAsync(batch);
    await CheckpointAsync(leaseToken);
}
```

#### Step 2: Transform for Role Assignment Changes

When a role assignment changes, we need to:

1. **Resolve the role definition** - Get full permission details
2. **Identify affected scopes** - The assignment scope and all child scopes
3. **Identify affected principals** - Direct principal + groups containing this principal
4. **Compute effective access** - Merge with existing assignments

```csharp
public async Task<List<EffectiveAccessUpdate>> TransformAssignmentChange(
    RoleAssignment assignment,
    ChangeType changeType)
{
    var updates = new List<EffectiveAccessUpdate>();

    // Get role definition details
    var roleDef = await _roleDefCache.GetAsync(assignment.RoleDefinitionId);

    // Get all scopes at or below assignment scope
    var affectedScopes = await _scopeRegistry.GetScopeAndDescendants(assignment.Scope);

    foreach (var scope in affectedScopes)
    {
        var update = new EffectiveAccessUpdate
        {
            PrincipalId = assignment.PrincipalId,
            Scope = scope,
            Operation = changeType == ChangeType.Delete
                ? UpdateOperation.RemoveRole
                : UpdateOperation.AddOrUpdateRole,
            RoleData = new EffectiveRole
            {
                RoleDefinitionId = roleDef.Id,
                RoleName = roleDef.Name,
                AssignmentId = assignment.Id,
                AssignmentScope = assignment.Scope,
                Inherited = scope != assignment.Scope,
                Condition = assignment.Condition,
                Permissions = roleDef.Permissions
            }
        };

        updates.Add(update);
    }

    return updates;
}
```

#### Step 3: Write to Data Plane

```csharp
public async Task ApplyUpdatesAsync(List<EffectiveAccessUpdate> updates)
{
    // Group by principal for efficient batch operations
    var byPrincipal = updates.GroupBy(u => u.PrincipalId);

    foreach (var group in byPrincipal)
    {
        var principalId = group.Key;

        foreach (var update in group)
        {
            var docId = ComputeDocumentId(principalId, update.Scope);

            // Read current document (or create new)
            var doc = await _dataPlaneDb.ReadOrCreateAsync<EffectiveAccess>(
                docId,
                principalId);

            // Apply update
            if (update.Operation == UpdateOperation.AddOrUpdateRole)
            {
                doc.EffectiveRoles.RemoveAll(r => r.AssignmentId == update.RoleData.AssignmentId);
                doc.EffectiveRoles.Add(update.RoleData);
            }
            else // RemoveRole
            {
                doc.EffectiveRoles.RemoveAll(r => r.AssignmentId == update.RoleData.AssignmentId);
            }

            // Update metadata
            doc.SyncMetadata.LastSyncedAt = DateTime.UtcNow;
            doc.SyncMetadata.SourceVersion++;

            // Write back
            await _dataPlaneDb.UpsertAsync(doc, principalId);
        }
    }
}
```

### 8.4 Handling Deletes

CosmosDB Change Feed doesn't directly surface deletes. Options:

**Option A: Soft Deletes (Recommended)**
- Add `isDeleted: true` and `deletedAt` timestamp to deleted documents
- Set TTL for automatic cleanup after sync processes the delete
- Sync service treats `isDeleted: true` as a delete operation

```json
{
  "id": "ra-123",
  "isDeleted": true,
  "deletedAt": "2025-01-15T10:00:00Z",
  "ttl": 86400,
  ...
}
```

**Option B: Delete Log**
- Maintain separate container for delete events
- Sync service processes delete log alongside change feed

### 8.5 Consistency Guarantees

The system provides **eventual consistency** with the following guarantees:

| Guarantee | Description |
|-----------|-------------|
| **Ordering** | Changes to a single partition are processed in order |
| **At-least-once** | Every change is processed at least once (idempotent operations handle duplicates) |
| **Checkpoint recovery** | On failure, processing resumes from last checkpoint |
| **Bounded lag** | Under normal load, sync lag < 10 seconds |

### 8.6 Monitoring and Alerting

**Key Metrics**:

```
sync_lag_seconds          # Time between control plane write and data plane availability
sync_throughput_per_sec   # Changes processed per second
sync_error_count          # Failed sync operations
sync_dlq_depth            # Items in dead letter queue
checkpoint_age_seconds    # Time since last successful checkpoint
```

**Alerts**:

| Metric | Threshold | Severity |
|--------|-----------|----------|
| sync_lag_seconds | > 30s | Warning |
| sync_lag_seconds | > 120s | Critical |
| sync_error_rate | > 1% | Warning |
| sync_dlq_depth | > 100 | Critical |

---

## 9. Bootstrapping, Idempotency, and Replay Handling

This section addresses the critical concerns of initializing the data plane, handling duplicate event processing, and ensuring system consistency.

### 9.1 Data Plane Bootstrapping

#### 9.1.1 Bootstrap Scenarios

| Scenario | Trigger | Approach |
|----------|---------|----------|
| Initial deployment | New system, empty data plane | Full bootstrap from control plane |
| Data corruption | Detected inconsistencies | Selective or full rebuild |
| Disaster recovery | Data plane lost | Full bootstrap from control plane |
| Schema migration | Data model changes | Rolling rebuild with dual-write |
| New region | Adding read replica region | Full bootstrap to new region |

#### 9.1.2 Full Bootstrap Process

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        BOOTSTRAP ORCHESTRATOR                            │
│                                                                          │
│  Phase 1: Preparation                                                    │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ 1. Stop incremental sync processors                                 │ │
│  │ 2. Record current change feed position (LSN) for each container    │ │
│  │ 3. Create bootstrap checkpoint record                               │ │
│  │ 4. Optionally: Create new data plane containers (for zero-downtime) │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                   ↓                                      │
│  Phase 2: Data Export                                                    │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ 1. Export all RoleDefinitions (point-in-time snapshot)             │ │
│  │ 2. Export all RoleAssignments (point-in-time snapshot)             │ │
│  │ 3. Store exports with LSN watermarks                                │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                   ↓                                      │
│  Phase 3: Transformation & Load                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ 1. Process role definitions → copy to data plane                   │ │
│  │ 2. Process role assignments → compute EffectiveAccess documents    │ │
│  │ 3. Mark all documents with syncMode: "Bootstrap"                   │ │
│  │ 4. Record processed changeIds for idempotency                      │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                   ↓                                      │
│  Phase 4: Catch-up & Cutover                                            │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ 1. Start change feed from recorded LSN position                    │ │
│  │ 2. Process all changes since bootstrap started                     │ │
│  │ 3. When caught up: switch traffic to new containers (if applicable)│ │
│  │ 4. Mark bootstrap complete, switch to incremental mode             │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

#### 9.1.3 Bootstrap Checkpoint Document

Stored in a dedicated `SyncState` container in the data plane:

```json
{
  "id": "bootstrap-state",
  "partitionKey": "global",

  "status": "InProgress",
  "startedAt": "2025-01-15T08:00:00Z",
  "completedAt": null,

  "sourceSnapshot": {
    "roleDefinitionsLsn": 1000,
    "roleDefinitionsCount": 150,
    "roleAssignmentsLsn": 5000,
    "roleAssignmentsCount": 25000,
    "snapshotTimestamp": "2025-01-15T08:00:00Z"
  },

  "progress": {
    "roleDefinitionsProcessed": 150,
    "roleAssignmentsProcessed": 18500,
    "effectiveAccessDocumentsCreated": 45000,
    "lastProcessedAssignmentId": "ra-xyz-789",
    "percentComplete": 74
  },

  "catchUp": {
    "changeFeedStartLsn": {
      "roleDefinitions": 1000,
      "roleAssignments": 5000
    },
    "currentLsn": {
      "roleDefinitions": 1050,
      "roleAssignments": 5200
    },
    "changesPending": 250
  },

  "workers": [
    { "id": "worker-1", "partition": "0-3", "status": "Running", "lastHeartbeat": "..." },
    { "id": "worker-2", "partition": "4-7", "status": "Running", "lastHeartbeat": "..." }
  ]
}
```

#### 9.1.4 Zero-Downtime Bootstrap

For systems that cannot tolerate downtime during bootstrap:

```
Time →
──────────────────────────────────────────────────────────────────────────

        │← Bootstrap Phase →│← Catch-up →│← Cutover →│
        │                   │            │           │
Old     ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■□  (decommission)
Containers   (serving reads)                        │
                                                    │
New     ○○○○○○○○○○○○○○○○○○○○○○○○○○○■■■■■■■■■■■■■■■■■■■■■■■■■■
Containers   (building)        (catching up) (serving reads)

Legend: ■ = Active   ○ = Building   □ = Draining
```

**Steps:**
1. Create new containers with versioned names (e.g., `EffectiveAccess_v2`)
2. Bootstrap populates new containers while old containers serve traffic
3. During catch-up, both receive writes (dual-write period)
4. Atomic cutover: update container reference in configuration
5. Drain and delete old containers after verification

### 9.2 Idempotency Design

The sync service must handle duplicate events safely. This is critical because:
- Change feed provides **at-least-once** delivery
- Worker failures cause checkpoint rollback and replay
- Network issues may cause retries

#### 9.2.1 Idempotency Mechanisms

**Layer 1: Change ID Deduplication**

Every change has a unique `changeId`. Before processing:

```csharp
public async Task<bool> ShouldProcessChange(string changeId, EffectiveAccess doc)
{
    // Check if this change was already processed
    if (doc._sync.lastProcessedChangeIds.Contains(changeId))
    {
        _metrics.RecordDuplicateSkipped(changeId);
        return false;  // Skip - already processed
    }
    return true;
}
```

**Layer 2: Version Comparison**

Compare source version with what's already in the document:

```csharp
public bool IsNewerVersion(
    SourceVersions incoming,
    SourceVersions existing)
{
    // Sequence numbers are monotonically increasing
    if (incoming.assignmentSequence <= existing.assignmentSequence &&
        incoming.roleDefSequence <= existing.roleDefSequence)
    {
        return false;  // Not newer, skip update
    }
    return true;
}
```

**Layer 3: Conditional Writes with ETags**

Use optimistic concurrency to prevent lost updates:

```csharp
public async Task ApplyUpdate(EffectiveAccess doc, EffectiveRole update)
{
    var retries = 0;
    while (retries < 3)
    {
        try
        {
            // Apply update
            ApplyRoleUpdate(doc, update);
            doc._sync.documentVersion++;
            doc._sync.lastSyncedAt = DateTime.UtcNow;

            // Conditional write with etag
            await _container.ReplaceItemAsync(
                doc,
                doc.id,
                new PartitionKey(doc.principalId),
                new ItemRequestOptions { IfMatchEtag = doc._etag }
            );
            return;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // Concurrent modification - reload and retry
            doc = await _container.ReadItemAsync<EffectiveAccess>(doc.id, ...);
            retries++;
        }
    }
    throw new ConcurrentModificationException("Max retries exceeded");
}
```

#### 9.2.2 Idempotent Operations Matrix

| Operation | Idempotency Approach | Safe to Replay? |
|-----------|---------------------|-----------------|
| Add role to EffectiveAccess | Upsert by assignmentId + version check | ✓ Yes |
| Update role in EffectiveAccess | Version comparison + conditional write | ✓ Yes |
| Remove role from EffectiveAccess | Check existence + version before remove | ✓ Yes |
| Create new EffectiveAccess doc | Check changeId + version on create | ✓ Yes |
| Delete EffectiveAccess doc | Soft delete with version check | ✓ Yes |

#### 9.2.3 Change ID Lifecycle

```
Control Plane                         Sync Service                       Data Plane
     │                                     │                                  │
     │  Create RoleAssignment              │                                  │
     │  Generate changeId: chg-abc-123     │                                  │
     │  _version.changeId = chg-abc-123    │                                  │
     │─────────────────────────────────────►                                  │
     │                                     │                                  │
     │                        Change Feed  │                                  │
     │                        delivers     │                                  │
     │                        chg-abc-123  │                                  │
     │                                     │                                  │
     │                                     │  Check: Is chg-abc-123 in        │
     │                                     │  lastProcessedChangeIds?         │
     │                                     │──────────────────────────────────►
     │                                     │                                  │
     │                                     │  No → Process change             │
     │                                     │  Add chg-abc-123 to              │
     │                                     │  lastProcessedChangeIds          │
     │                                     │  Write with ETag                 │
     │                                     │──────────────────────────────────►
     │                                     │                                  │
     │                        (Worker      │                                  │
     │                         crashes)    │                                  │
     │                                     │                                  │
     │                        Change Feed  │                                  │
     │                        replays      │                                  │
     │                        chg-abc-123  │                                  │
     │                                     │                                  │
     │                                     │  Check: Is chg-abc-123 in        │
     │                                     │  lastProcessedChangeIds?         │
     │                                     │──────────────────────────────────►
     │                                     │                                  │
     │                                     │  Yes → Skip (duplicate)          │
     │                                     │                                  │
```

### 9.3 Handling Change Event Replays

#### 9.3.1 Replay Scenarios

| Scenario | Cause | Impact | Mitigation |
|----------|-------|--------|------------|
| Worker restart | Process crash, deployment | Events since last checkpoint replayed | Idempotent operations |
| Checkpoint failure | Storage unavailable | Larger replay window | Frequent checkpoints, idempotency |
| Partition rebalance | Worker added/removed | Brief replay during handoff | Lease-based coordination |
| Manual replay | Bug fix, data correction | Intentional full replay | Version-aware processing |

#### 9.3.2 Replay Detection Algorithm

```csharp
public class ReplayDetector
{
    public ReplayDecision ShouldProcess(
        ChangeEvent change,
        EffectiveAccess existingDoc)
    {
        // Case 1: Document doesn't exist - always process
        if (existingDoc == null)
        {
            return ReplayDecision.Process;
        }

        // Case 2: Exact changeId match - definite duplicate
        if (existingDoc._sync.lastProcessedChangeIds.Contains(change.ChangeId))
        {
            return ReplayDecision.Skip("Duplicate changeId");
        }

        // Case 3: Check version vectors for the specific role
        var existingRole = existingDoc.effectiveRoles
            .FirstOrDefault(r => r.assignmentId == change.AssignmentId);

        if (existingRole != null)
        {
            // Compare sequence numbers
            if (change.SequenceNumber <= existingRole._sourceVersions.assignmentSequence)
            {
                return ReplayDecision.Skip("Stale version");
            }

            // Compare timestamps as tiebreaker
            if (change.SequenceNumber == existingRole._sourceVersions.assignmentSequence &&
                change.Timestamp <= existingRole._sourceVersions.timestamp)
            {
                return ReplayDecision.Skip("Same version, older timestamp");
            }
        }

        // Case 4: Check watermarks for obviously stale events
        if (change.Timestamp < existingDoc._watermarks.assignmentHighWatermark &&
            change.SequenceNumber < GetSequenceFromWatermark(existingDoc))
        {
            return ReplayDecision.Skip("Below watermark");
        }

        // Process the change
        return ReplayDecision.Process;
    }
}
```

#### 9.3.3 Handling Out-of-Order Events

Change feed guarantees ordering within a partition, but when processing multiple containers or handling retries, events may arrive out of order.

**Strategy: Last-Writer-Wins with Version Vectors**

```csharp
public void MergeRole(EffectiveAccess doc, EffectiveRole incomingRole)
{
    var existingRole = doc.effectiveRoles
        .FirstOrDefault(r => r.assignmentId == incomingRole.assignmentId);

    if (existingRole == null)
    {
        // New role - just add
        doc.effectiveRoles.Add(incomingRole);
        return;
    }

    // Compare version vectors
    var comparison = CompareVersionVectors(
        existingRole._sourceVersions,
        incomingRole._sourceVersions);

    switch (comparison)
    {
        case VersionComparison.IncomingNewer:
            // Replace existing with incoming
            doc.effectiveRoles.Remove(existingRole);
            doc.effectiveRoles.Add(incomingRole);
            break;

        case VersionComparison.ExistingNewer:
            // Keep existing, discard incoming (out of order)
            _metrics.RecordOutOfOrderEvent(incomingRole.assignmentId);
            break;

        case VersionComparison.Concurrent:
            // Conflict! Use deterministic resolution
            var winner = ResolveConcurrentModification(existingRole, incomingRole);
            doc.effectiveRoles.Remove(existingRole);
            doc.effectiveRoles.Add(winner);
            _metrics.RecordConflictResolution(incomingRole.assignmentId);
            break;
    }
}

private EffectiveRole ResolveConcurrentModification(
    EffectiveRole a,
    EffectiveRole b)
{
    // Deterministic resolution: higher changeId wins
    // (GUIDs are lexicographically comparable)
    return string.Compare(
        a._sourceVersions.assignmentChangeId,
        b._sourceVersions.assignmentChangeId,
        StringComparison.Ordinal) > 0 ? a : b;
}
```

### 9.4 Sync State Machine

```
                                    ┌─────────────────┐
                                    │                 │
                                    │   Uninitialized │
                                    │                 │
                                    └────────┬────────┘
                                             │
                                             │ StartBootstrap()
                                             ▼
┌──────────────────┐              ┌─────────────────┐
│                  │              │                 │
│  BootstrapFailed │◄─────────────│  Bootstrapping  │
│                  │    Error     │                 │
└──────────────────┘              └────────┬────────┘
         │                                 │
         │ RetryBootstrap()                │ BootstrapComplete()
         │                                 ▼
         │                        ┌─────────────────┐
         │                        │                 │
         └───────────────────────►│   CatchingUp    │
                                  │                 │
                                  └────────┬────────┘
                                           │
                                           │ CaughtUp()
                                           ▼
                                  ┌─────────────────┐
                                  │                 │◄──────────┐
                                  │  Incremental    │           │
                                  │   Syncing       │───────────┘
                                  │                 │  ProcessChange()
                                  └────────┬────────┘
                                           │
                                           │ SyncError()
                                           ▼
                                  ┌─────────────────┐
                                  │                 │
                                  │    Degraded     │──────────┐
                                  │                 │          │
                                  └────────┬────────┘          │
                                           │                   │
                                           │ Recovered()       │ TooManyErrors()
                                           ▼                   ▼
                                  ┌─────────────────┐  ┌──────────────────┐
                                  │  Incremental    │  │                  │
                                  │   Syncing       │  │   RequiresRebuild│
                                  └─────────────────┘  │                  │
                                                       └──────────────────┘
```

### 9.5 Consistency Verification

Periodic verification ensures data plane accuracy:

```csharp
public class ConsistencyVerifier
{
    public async Task<VerificationResult> VerifyConsistency(
        string principalId,
        string scope)
    {
        // 1. Compute expected state from control plane
        var expectedRoles = await ComputeExpectedEffectiveAccess(principalId, scope);

        // 2. Read actual state from data plane
        var actualDoc = await _dataPlane.ReadEffectiveAccess(principalId, scope);

        // 3. Compare
        var differences = CompareEffectiveRoles(expectedRoles, actualDoc?.effectiveRoles);

        if (differences.Any())
        {
            return new VerificationResult
            {
                IsConsistent = false,
                PrincipalId = principalId,
                Scope = scope,
                Differences = differences,
                RecommendedAction = DetermineRepairAction(differences)
            };
        }

        return VerificationResult.Consistent(principalId, scope);
    }

    public async Task RepairInconsistency(VerificationResult result)
    {
        switch (result.RecommendedAction)
        {
            case RepairAction.Resync:
                // Re-process all assignments for this principal/scope
                await ResyncPrincipalScope(result.PrincipalId, result.Scope);
                break;

            case RepairAction.Rebuild:
                // Delete and rebuild document from scratch
                await RebuildEffectiveAccess(result.PrincipalId, result.Scope);
                break;

            case RepairAction.Manual:
                // Alert operators
                await AlertForManualIntervention(result);
                break;
        }
    }
}
```

**Verification Schedule:**
- Continuous: Sample 0.1% of documents randomly
- Daily: Full scan of recently modified documents
- On-demand: Triggered by user complaints or anomaly detection

### 9.6 Change ID Garbage Collection

The `lastProcessedChangeIds` array would grow unbounded without cleanup:

```csharp
public void PruneProcessedChangeIds(EffectiveAccess doc)
{
    const int MaxChangeIdsToRetain = 100;
    const int PruneThreshold = 150;

    if (doc._sync.lastProcessedChangeIds.Count > PruneThreshold)
    {
        // Keep only the most recent change IDs
        // Older changes are unlikely to replay after checkpoint advances
        doc._sync.lastProcessedChangeIds = doc._sync.lastProcessedChangeIds
            .OrderByDescending(id => ExtractTimestamp(id))
            .Take(MaxChangeIdsToRetain)
            .ToList();
    }
}

// Change ID format includes timestamp: chg-{timestamp}-{random}
// Example: chg-20250115103000-a1b2c3d4
private DateTime ExtractTimestamp(string changeId)
{
    var parts = changeId.Split('-');
    return DateTime.ParseExact(parts[1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
}
```

---

## 10. Storage Design

### 10.1 Control Plane CosmosDB

**Account Configuration**:
- API: Core (SQL)
- Consistency: Session
- Multi-region: Yes (for HA)
- Backup: Continuous

**Containers**:

| Container | Partition Key | RU/s | Purpose |
|-----------|---------------|------|---------|
| RoleDefinitions | /id | 400 (autoscale) | Store role definitions |
| RoleAssignments | /scope | 1000 (autoscale) | Store role assignments |
| Scopes | /parentScope | 400 (autoscale) | Scope hierarchy (optional) |
| SyncLeases | /id | 400 | Change feed processor leases |

**Indexing Policy for RoleAssignments**:

```json
{
  "indexingMode": "consistent",
  "includedPaths": [
    { "path": "/scope/*" },
    { "path": "/principalId/*" },
    { "path": "/roleDefinitionId/*" },
    { "path": "/metadata/createdAt/*" }
  ],
  "excludedPaths": [
    { "path": "/condition/*" },
    { "path": "/*" }
  ]
}
```

### 10.2 Data Plane CosmosDB

**Account Configuration**:
- API: Core (SQL)
- Consistency: Eventual (for max read performance)
- Multi-region: Yes (read replicas in each region)
- Backup: Continuous

**Containers**:

| Container | Partition Key | RU/s | Purpose |
|-----------|---------------|------|---------|
| EffectiveAccess | /principalId | 10000 (autoscale) | Pre-computed access lookup |
| RoleDefinitions | /id | 1000 (autoscale) | Copy of role definitions |
| SyncState | /partitionKey | 400 | Bootstrap state, sync checkpoints, worker leases |

**Indexing Policy for EffectiveAccess**:

```json
{
  "indexingMode": "consistent",
  "includedPaths": [
    { "path": "/principalId/*" },
    { "path": "/scope/*" },
    { "path": "/scopeDepth/*" }
  ],
  "excludedPaths": [
    { "path": "/effectiveRoles/*" },
    { "path": "/groupMemberships/*" },
    { "path": "/*" }
  ]
}
```

**Rationale**: The primary query pattern is point reads by `principalId`. We exclude `effectiveRoles` from indexing because we never query by role contents - we always fetch the full document.

---

## 11. Scalability Considerations

### 11.1 Data Plane Scaling

**Horizontal Scaling**:
- Stateless API instances behind load balancer
- Scale based on CPU/request rate
- Target: 10K requests/second per instance

**Data Scaling**:
- CosmosDB partitions by `principalId`
- Hot principals (service accounts with many permissions) may need special handling
- Consider caching layer for extremely hot principals

### 11.2 Sync Service Scaling

**Worker Scaling**:
- Number of workers = Number of physical partitions / partitions per worker
- CosmosDB supports up to 10,000 partitions
- Each worker can handle ~4 partitions efficiently

**Throughput**:
- Target: Process 10K changes/second
- Bottleneck is typically write throughput to data plane

### 11.3 Handling Large Tenants

For tenants with millions of principals or assignments:

1. **Partition by tenant**: Use tenant ID in partition key
2. **Lazy evaluation**: Don't pre-compute for rarely-accessed principals
3. **Tiered storage**: Hot principals in CosmosDB, cold in cheaper storage

---

## 12. Security Considerations

### 12.1 Control Plane Security

- **Authentication**: Azure AD / OAuth 2.0
- **Authorization**: The RBAC system authorizes itself (bootstrap with initial admin)
- **Audit logging**: Log all mutations with caller identity, timestamp, changes
- **Input validation**: Validate all inputs, especially condition expressions

### 12.2 Data Plane Security

- **Authentication**: Service-to-service (managed identity, API keys)
- **No direct user access**: Only trusted services call CheckAccess
- **No sensitive data in responses**: Don't leak permission details to unauthorized callers
- **Rate limiting**: Protect against abuse

### 12.3 Condition Expression Security

Condition expressions must be sandboxed:

```csharp
public class ConditionEvaluator
{
    private static readonly TimeSpan MaxEvaluationTime = TimeSpan.FromMilliseconds(100);
    private static readonly int MaxExpressionDepth = 10;

    public bool Evaluate(ConditionExpression condition, ResourceAttributes attributes)
    {
        // Validate expression complexity
        if (condition.Depth > MaxExpressionDepth)
            throw new ConditionTooComplexException();

        // Evaluate with timeout
        using var cts = new CancellationTokenSource(MaxEvaluationTime);
        return EvaluateWithCancellation(condition, attributes, cts.Token);
    }
}
```

---

## 13. Failure Modes and Recovery

### 13.1 Control Plane Unavailable

**Impact**: No new role assignments/definitions can be created
**Mitigation**: Multi-region deployment with failover
**Data Plane Impact**: None - continues serving with existing data

### 13.2 Sync Service Failure

**Impact**: Data plane becomes stale
**Detection**: Monitor sync lag metric
**Recovery**:
1. Auto-restart failed workers
2. Leases automatically redistribute
3. Processing resumes from last checkpoint

### 13.3 Data Plane Unavailable

**Impact**: Authorization checks fail
**Mitigation**:
- Multi-region read replicas
- Client-side caching with short TTL
- Fail-open vs fail-closed policy (configurable)

### 13.4 Data Corruption

**Recovery**:
1. Stop sync service
2. Clear data plane containers
3. Run full resync from control plane
4. Resume change feed processing

---

## 14. API Contracts (OpenAPI Summary)

### Control Plane

```yaml
paths:
  /api/v1/roleDefinitions:
    get:
      summary: List role definitions
      parameters:
        - name: assignableScope
        - name: type
    post:
      summary: Create role definition

  /api/v1/roleDefinitions/{id}:
    get:
      summary: Get role definition
    put:
      summary: Update role definition
    delete:
      summary: Delete role definition

  /api/v1/scopes/{scope}/roleAssignments:
    get:
      summary: List role assignments at scope
      parameters:
        - name: principalId
        - name: includeInherited
    post:
      summary: Create role assignment

  /api/v1/scopes/{scope}/roleAssignments/{id}:
    get:
      summary: Get role assignment
    delete:
      summary: Delete role assignment
```

### Data Plane

```yaml
paths:
  /api/v1/checkAccess:
    post:
      summary: Check if access is allowed
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CheckAccessRequest'
      responses:
        200:
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CheckAccessResponse'

  /api/v1/checkAccess/batch:
    post:
      summary: Batch check access
```

---

## 15. Implementation Roadmap

### Phase 1: Core Functionality
- Control plane APIs (CRUD for roles and assignments)
- Basic CheckAccess API
- Simple sync service (no inheritance, no conditions)

### Phase 2: Advanced Features
- Scope hierarchy and inheritance
- Condition expression support
- Batch CheckAccess

### Phase 3: Scale and Reliability
- Multi-region deployment
- Advanced monitoring and alerting
- Performance optimization

### Phase 4: Enterprise Features
- Audit logging and compliance
- Just-in-time access
- Access reviews integration

---

## 16. Appendix

### A. Sample Condition Expressions

```
# Allow only if blob container name is "public"
@Resource[Microsoft.Storage/storageAccounts/blobServices/containers:name] StringEquals 'public'

# Allow only if resource has tag "environment" = "dev"
@Resource[tags:environment] StringEquals 'dev'

# Allow only during business hours (advanced)
@DateTime[UtcNow:Hour] GreaterThanOrEquals 9 AND @DateTime[UtcNow:Hour] LessThan 17
```

### B. Scope Hierarchy Parsing

```csharp
public static List<string> GetScopeHierarchy(string scope)
{
    // Input: /subscriptions/sub-001/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/mystorage
    // Output: [
    //   "/subscriptions/sub-001/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/mystorage",
    //   "/subscriptions/sub-001/resourceGroups/rg-prod",
    //   "/subscriptions/sub-001",
    //   "/"
    // ]

    var hierarchy = new List<string> { scope };
    var segments = scope.Split('/', StringSplitOptions.RemoveEmptyEntries);

    // Walk up the hierarchy
    for (int i = segments.Length - 2; i >= 0; i -= 2)
    {
        var parentScope = "/" + string.Join("/", segments.Take(i + 1));
        hierarchy.Add(parentScope);
    }

    hierarchy.Add("/"); // Root scope
    return hierarchy;
}
```

### C. Glossary

| Term | Definition |
|------|------------|
| **Principal** | Entity that can be granted access (user, group, service) |
| **Scope** | Boundary where access applies |
| **Role Definition** | Collection of permissions |
| **Role Assignment** | Binding of principal + role + scope |
| **Condition** | Attribute-based constraint on role assignment |
| **Change Feed** | CosmosDB feature for CDC (Change Data Capture) |
| **Effective Access** | Pre-computed access for a principal at a scope |
| **LSN** | Logical Sequence Number - CosmosDB's position marker in change feed |
| **Change ID** | Unique identifier for a specific change event, used for idempotency |
| **Sequence Number** | Monotonically increasing version counter for a document |
| **ETag** | Entity tag for optimistic concurrency control |
| **Bootstrap** | Initial population of data plane from control plane snapshot |
| **Watermark** | High-water mark timestamp indicating latest processed change |
| **Idempotency** | Property ensuring operations can be safely retried |
| **CDC** | Change Data Capture - capturing and propagating data changes |

---

*Document Version: 1.0*
*Created: 2025-01-15*
