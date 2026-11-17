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


## Concurrent reviewer protection

Decision and execution transitions use a shared compare-and-set method. SQL Server uses a conditional `UPDATE` constrained by tenant, proposal ID, and expected status. The in-memory integration provider uses a serialized transition gate and persists the state before releasing it. A competing request receives HTTP 409 once the proposal has left its expected state. Run the concurrency integration test against SQL Server before production.

## Proposal expiry

The API derives `Expired` for a pending proposal whose `ExpiresAt` timestamp has passed. Expired proposals are excluded from the pending-only list and remain visible in the all-proposals list. The underlying workflow row is not mutated by a read; the decision endpoint independently rejects an expired proposal with HTTP 409. This avoids a background worker or migration in the first release.


## One-time email execution

The execution slice adds:

- `POST /actions/proposals/{proposalId}/execute`
- Only Owner, Admin, or Manager may request execution.
- Only an `Approved` `SendCustomerFollowUp` proposal can be executed.
- The customer is reloaded from the authenticated tenant and must still be active with a valid email address.
- A conditional state transition claims the proposal as `Executing` before contacting SMTP. The proposal ID is the one-time execution key; subsequent requests cannot claim it again.
- Delivery success becomes `Executed`; provider-disabled or failed delivery becomes `ExecutionFailed`.
- Execution start, success, and failure are written to the existing RBAC audit log.

SMTP is not an idempotent delivery provider. To avoid duplicate customer emails, automatic retries are intentionally disabled. If the process is interrupted after SMTP accepts a message but before the database records success, the proposal remains `Executing` for manual reconciliation. `Executed` means the configured SMTP provider accepted the message, not that the recipient opened or received it in their inbox.

This is an explicit authenticated execution request after approval; it is not yet an autonomous background worker. Tenant-specific AI employee tool allow-lists must be enforced before production use. Add provider-level idempotency where supported and a reconciliation dashboard for stuck executions.
