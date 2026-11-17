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

- Add a scheduled expiry worker if expiry needs persisted lifecycle transitions and notifications; the API currently derives expiry from the proposal deadline when reading.
- Add optimistic concurrency using a provider-supported concurrency token and test it against SQL Server before enabling concurrent reviewer decisions.
- Add expiry processing so stale pending proposals are visibly expired.
- Add a dedicated proposal entity/table if proposal volume or reporting needs outgrow workflow-run storage.
- Add UI for review, diff/preview, and decision history.
- Implement message delivery as a separate worker only after approval, with idempotency, provider status tracking, retries, and a second permission check at execution time.
- Run backend build, unit/integration tests, and relational SQL Server tests before merging.


## Concurrent reviewer decisions

The decision endpoint uses a provider-aware compare-and-set transition constrained by proposal ID, tenant ID, and the expected `AwaitingApproval` state.

- Relational providers use a single conditional `UPDATE`; only one reviewer can transition a pending proposal.
- The status transition and RBAC audit insert share the same database transaction.
- The EF Core InMemory test provider uses a process-wide semaphore and reloads the tracked row before deciding, so concurrent integration requests still exercise a single-winner state transition.
- The integration suite includes a concurrent approve/reject test that expects one HTTP 200 and one HTTP 409.

The concurrent integration test has been added but not executed here. Run it against both the InMemory test provider and SQL Server before production rollout.

## Proposal expiry

The API derives `Expired` for a pending proposal whose `ExpiresAt` timestamp has passed. Expired proposals are excluded from the pending-only list and remain visible in the all-proposals list. The underlying workflow row is not mutated by a read; the decision endpoint independently rejects an expired proposal with HTTP 409. This avoids a background worker or migration in the first release.
