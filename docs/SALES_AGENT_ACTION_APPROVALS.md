# Sales Agent Action Approval Workflow

## Scope

This is the first persistence/API slice for human-reviewed Sales Agent actions. It stores a proposed action, exposes it to the owning tenant, and records an approval or rejection decision.

**Approving a proposal does not execute it.** No email, WhatsApp message, order, invoice, stock update, or other business mutation is performed by these endpoints.

## Supported action type

- `SendCustomerFollowUp` — proposal only; payload is stored as JSON for review. Delivery integration is intentionally not connected.

Unknown action types are rejected. Payloads must be JSON objects no larger than 8,000 characters. Titles are limited to 120 characters, summaries to 1,000, comments to 500, and optional expiry to the next 30 days.

## API

All routes are under `/api/v1/agents` and require authentication.

- `POST /actions/proposals` — create a pending proposal.
- `GET /actions/proposals?pendingOnly=true&limit=50` — list the current tenant's proposals.
- `GET /actions/proposals/{proposalId}` — read a proposal within the current tenant.
- `POST /actions/proposals/{proposalId}/decision` — approve or reject.

Decision request:

```json
{
  "decision": "approved",
  "comment": "Reviewed by the sales manager."
}
```

Only Owner, Admin, and Manager roles may decide proposals. Tenant filtering is inherited from `AgentWorkflowRuns`; a proposal belonging to another tenant is not found.

## Persistence and audit

The first increment reuses `AgentWorkflowRun` and its existing `ProgressJson` column, avoiding a new table/migration. New lifecycle values are appended to the existing enum:

- `AwaitingApproval`
- `Approved`
- `Rejected`

Creation and decision events are written to the existing RBAC audit log. The proposal payload is retained as JSON and returned only to the authenticated owning tenant.

## Important follow-up before production

- Add database-backed expiry processing so stale pending proposals are visibly expired.
- Add expiry processing so stale pending proposals are visibly expired.
- Add a dedicated proposal entity/table if proposal volume or reporting needs outgrow workflow-run storage.
- Add UI for review, diff/preview, and decision history.
- Implement message delivery as a separate worker only after approval, with idempotency, provider status tracking, retries, and a second permission check at execution time.
- Run backend build, unit/integration tests, and relational SQL Server tests before merging.

## Concurrent reviewer protection

Decision updates use a database compare-and-set operation constrained to the `AwaitingApproval` state, inside the same transaction as the RBAC audit insert. If two reviewers submit at the same time, only the request that changes the pending row can commit; the other receives HTTP 409. An integration test exercises concurrent approve/reject requests. Run it against the production relational provider (SQL Server) before merge; EF InMemory does not provide equivalent relational concurrency semantics.
