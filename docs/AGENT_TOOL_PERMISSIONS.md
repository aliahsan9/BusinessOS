# Tenant-specific AI employee tool permissions

## API

- `GET /api/v1/agents/employees/{agentKey}/permissions`
- `PUT /api/v1/agents/employees/{agentKey}/permissions`

Request:

```json
{
  "allowedTools": ["GetLowStock", "GetInventoryHealth"]
}
```

Only authenticated tenant Owners and Admins can read or update an employee's allowlist. The service creates a tenant-owned `AgentProfile` override on first update; it never edits the shared system catalog. An empty list means deny all tools. Unknown enum names are rejected.

## Enforcement

A tool executes only when both checks pass:

1. The signed-in user has the existing BusinessOS RBAC permission for the tool.
2. The selected AI employee's effective tenant profile contains that tool in `AllowedToolsJson`.

This policy is enforced in:
- The employee `AgentToolExecutor`, including its search-and-retry path.
- The model-driven read-only tool loop (catalog filtering and server-side callback validation).
- The legacy copilot route when an `AgentKey` is supplied, including direct dashboard-priority and RAG execution.

The employee identity is carried from the server-resolved chat route into execution. The model cannot choose a different agent key or tenant.

## Default policies

The migration backfills existing profiles with conservative role-oriented baselines. Adam and Emma start with specialist read tools. Navla retains common business reads and confirmed core business actions; destructive deletion and system-level settings tools are excluded. Tenant Owners/Admins can customize the allowlist.

The user RBAC check and existing confirmation flow remain mandatory. An employee allowlist does not grant a user additional permissions.

## Migration

`20261117150000_AddAgentToolPermissions` adds `AgentProfiles.AllowedToolsJson` and backfills the baseline lists. Apply the EF Core migration before deploying this version of the API.

## Verification status

Integration tests cover tenant isolation, empty-list behavior and invalid tool rejection. A unit test covers denial when the user has permission but the employee does not. Build, migration execution and tests have not yet been run in this environment.
