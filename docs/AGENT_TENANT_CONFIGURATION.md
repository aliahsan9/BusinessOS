# Tenant-specific AI employee configuration

## Endpoint

`PUT /api/v1/agents/employees/{agentKey}/configuration`

Example request:

```json
{
  "displayName": "Adam Retail",
  "roleTitle": "Warehouse Lead",
  "specialty": "Retail stock, reorder planning, and supplier follow-up",
  "defaultLanguage": "ur-Latn"
}
```

The endpoint requires an authenticated tenant Owner or Admin. The service resolves the current tenant from the authenticated user context and only creates or updates the matching tenant-owned `AgentProfile` override.

## Editable fields

- Display name: required, maximum 100 characters.
- Role title: required, maximum 150 characters.
- Specialty: required, maximum 300 characters.
- Default language: one of `en`, `ur`, `ur-Latn`, or `ar`; values are normalized before persistence.

## Explicitly protected fields

The request does not expose the agent key, system persona prompt, active state, default state, or tool permissions. On first customization, the service copies the system profile's prompt and protected metadata into a tenant override. It then changes only the four editable fields. This avoids allowing a tenant user to replace the safety instructions or grant themselves new capabilities.

System profiles are read-only through this API. Tenant overrides do not affect other tenants.

## Follow-up work

- Add a dedicated, constrained custom-instructions field with prompt-injection boundaries and a migration.
- Implement module/tool permissions as server-enforced policy, not prompt text.
- Add audit history for configuration changes.
- Add optimistic concurrency/version handling if multiple admins edit the same agent concurrently.

## Verification

Integration tests cover tenant isolation and unsupported language rejection. Tests and build have not yet been run in this environment.
