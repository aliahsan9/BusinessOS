using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Agents;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class AgentWorkflowServiceTenantIsolationTests
{
    [Fact]
    public async Task WorkflowReadsAndUpdates_AreRestrictedToTheAuthenticatedTenant()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (contextA, _, _) = TenantDatabase.Open(tenantA, databaseName);
        var (contextB, _, _) = TenantDatabase.Open(tenantB, databaseName);
        await using var _ = contextA;
        await using var __ = contextB;

        var serviceA = CreateService(contextA, tenantA, "user-a");
        var created = await serviceA.CreateFromPlanAsync(
            new AgentWorkflowPlanDto
            {
                AgentKey = "adam",
                Title = "Tenant A workflow",
                Intent = BusinessOS.Application.Features.AI.Enums.AiCopilotIntent.Workflow,
                Steps =
                [
                    new AgentPlannedStepDto
                    {
                        StepKey = "read_inventory",
                        Title = "Read inventory",
                        SortOrder = 0
                    }
                ]
            },
            "user-a");

        var serviceB = CreateService(contextB, tenantB, "user-b");

        (await serviceB.GetAsync(created.Id)).Should().BeNull();
        (await serviceB.GetForUserAsync(created.Id, "user-a")).Should().BeNull();
        (await serviceB.ListRecentAsync("user-b")).Should().BeEmpty();

        var act = () => serviceB.StartAsync(created.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found*");

        (await serviceA.GetForUserAsync(created.Id, "user-a")).Should().NotBeNull();
        (await serviceA.ListRecentAsync("user-a")).Should().ContainSingle();
    }

    [Fact]
    public async Task WorkflowCannotBeCreatedOrListedOnBehalfOfAnotherUser()
    {
        var (context, tenantId, _) = TenantDatabase.Open();
        await using var _ = context;
        var service = CreateService(context, tenantId, "authenticated-user");
        var plan = new AgentWorkflowPlanDto
        {
            AgentKey = "adam",
            Title = "Unauthorized workflow",
            Intent = BusinessOS.Application.Features.AI.Enums.AiCopilotIntent.Workflow
        };

        var create = () => service.CreateFromPlanAsync(plan, "different-user");
        await create.Should().ThrowAsync<UnauthorizedAccessException>();

        var list = () => service.ListRecentAsync("different-user");
        await list.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static AgentWorkflowService CreateService(
        BusinessOS.Infrastructure.Data.BusinessOSDbContext context,
        Guid tenantId,
        string userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns(userId);

        return new AgentWorkflowService(
            context,
            currentUser.Object,
            NullLogger<AgentWorkflowService>.Instance);
    }
}
