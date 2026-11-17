namespace BusinessOS.Domain.Enums;

public enum AgentWorkflowStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    AwaitingApproval = 5,
    Approved = 6,
    Rejected = 7,
    Executing = 8,
    Executed = 9,
    ExecutionFailed = 10
}
