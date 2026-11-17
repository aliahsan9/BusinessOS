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
    Expired = 8,
    Executing = 9,
    Executed = 10,
    ExecutionFailed = 11
}
