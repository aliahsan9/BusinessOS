# Tenant-scoped AI employee activation

## API

`PUT /api/v1/agents/employees/{agentKey}/activation`

Request:

```json
{ "isActive": false }
```

The endpoint is authenticated and permits only users with the tenant `Owner` or `Admin` role. The service requires a resolved tenant context and only creates or updates a profile whose `TenantId` matches that tenant.

## Persistence model

The system-wide `AgentProfile` row remains unchanged. On the first tenant-specific change, the service creates a tenant override by copying the system catalog metadata and changing only tenant-scoped activation state. Subsequent changes update that tenant override. The existing unique index on `(TenantId, Key)` protects one override per agent key per tenant.

The effective profile resolver now applies tenant overrides before filtering inactive agents. This is essential: an inactive tenant override must suppress the active system default for that tenant, without affecting other tenants.

## Safety rules

- The system catalog is not modified by tenant activation.
- Tenant A's override cannot change Tenant B's effective catalog.
- The default employee cannot be deactivated until a separate default-switch operation exists.
- Chat persona resolution continues to use active effective profiles only.
- The employee catalog includes inactive effective profiles so tenant admins can reactivate them.

## Verification status

Integration tests were added for cross-tenant isolation, reactivation, and protection of the default employee. Tests have not been executed in this environment; run the backend test suite and API build before merging.
