# Tenant-specific AI employee tool permissions

## API

Owners and Admins can replace an employee's tool allow-list:

`PUT /api/v1/agents/employees/{agentKey}/permissions`

Request:

```json
{
  "allowedTools": ["GetInventorySummary", "GetLowStock"]
}
```

The API accepts exact `AiToolName` enum names that are also present in the runtime tool registry. Unknown, misspelled, and unregistered tools are rejected. The list is replaced atomically for that tenant/employee profile.

## Enforcement

The effective tenant employee profile carries the policy into `AgentToolExecutionRequest`. The executor checks the policy before invoking a tool, and repeats the check on fallback/retry execution. Existing user permission checks still run independently.

Effective access is the intersection:

`Agent allow-list AND current user's permission`

A tool is executable only when both checks pass.

## Policy semantics

- `AllowedToolsJson = null`: legacy behavior; no employee-specific restriction has been configured.
- `AllowedToolsJson = []`: explicitly deny every tool.
- Non-empty list: only the listed registered tools may execute.
- Corrupt stored JSON is treated as an empty list (fail closed).

Existing tenant profiles remain backward compatible because the new database column is nullable. A tenant's policy is stored on its tenant-owned `AgentProfile` override and does not mutate the shared system catalog.

When an explicit policy exists, agent chat avoids the legacy Copilot/model tool dispatch paths and routes tool execution through the policy-aware employee executor. Conversational turns with no detected tools are answered without invoking the legacy tool pipeline.

## Database

Migration: `20261117120000_AddAgentProfileToolPermissions`

Apply the normal EF Core migration process before deploying the API.

## Not included yet

The backend contract and enforcement are implemented first. The Angular permission editor should be added only after the backend PR is reviewed, merged, and its migration/build/tests have passed.


## Registered tool catalog endpoint

The authenticated `GET /api/v1/agents/tools` endpoint returns the actual dependency-injected `IAiToolRegistry.AllTools` catalog. Each item contains the stable enum-backed `name` and user-facing `description`. It intentionally does not expose internal permission requirements. The frontend should use this endpoint rather than maintain a duplicate hardcoded list.
