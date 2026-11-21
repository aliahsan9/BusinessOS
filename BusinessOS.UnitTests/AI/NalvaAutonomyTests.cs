using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.UnitTests.Common;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class NalvaAutonomyTests
{
    [Fact]
    public void PlanPolicy_DropsToolsOutsideTheGrant()
    {
        var sales = NalvaAgentCatalog.Get("sales");
        var plan = NalvaPlanPolicy.Accept(sales, ["DeleteProduct", "CreateLead", "SendExternalEmail"], "{\"name\":\"Ada\"}");

        plan.Select(step => step.Name).Should().Equal("CreateLead", "SendExternalEmail");
        plan.Single(step => step.Name == "SendExternalEmail").RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void IntelligentReply_IgnoresTextAroundTheJsonArray()
    {
        var names = NalvaPlanPolicy.ReadToolNames("Use only these tools:\n[\"GetSalesPipeline\",\"DeleteProduct\"]");
        names.Should().Equal("GetSalesPipeline", "DeleteProduct");
        var accepted = NalvaPlanPolicy.Accept(NalvaAgentCatalog.Get("sales"), names, "{}");
        accepted.Select(step => step.Name).Should().Equal("GetSalesPipeline");
    }

    [Fact]
    public void Condition_MatchesSuppliedContextAndRejectsMissingValues()
    {
        var context = new JsonObject { ["marketingConsent"] = true, ["priority"] = "High", ["leadScore"] = 80 };
        NalvaConditionEvaluator.TryEvaluate("marketingConsent == true && leadScore >= 50", context, out var allowed, out var error).Should().BeTrue();
        allowed.Should().BeTrue();
        error.Should().BeNull();

        NalvaConditionEvaluator.TryEvaluate("priority == High", new JsonObject(), out var missing, out _).Should().BeTrue();
        missing.Should().BeFalse();
        NalvaConditionEvaluator.TryEvaluate("drop table", context, out _, out var invalid).Should().BeFalse();
        invalid.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Schedule_DoesNotRepeatTheSameWindow()
    {
        var now = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        NalvaSchedule.TryWindow(now, "UTC", 1440, null, null, null, out var first, out _).Should().BeTrue();
        NalvaSchedule.TryWindow(now.AddHours(2), "UTC", 1440, null, null, first, out _, out _).Should().BeFalse();
        NalvaSchedule.TryWindow(now.AddDays(1), "UTC", 1440, null, null, first, out var next, out _).Should().BeTrue();
        next.Should().NotBe(first);
    }

    [Fact]
    public void Schedule_OnceDoesNotRepeat()
    {
        var now = new DateTime(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc);
        var spec = new NalvaScheduleSpec("UTC", NalvaRecurrenceKinds.Once, null, null, null, null, null, null);
        NalvaSchedule.TryDue(spec, now, null, out var window, out _).Should().BeTrue();
        window.Should().Be("once");
        NalvaSchedule.TryDue(spec, now.AddDays(30), window, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void WorkflowGraph_GroupsOnlyReadOnlySteps()
    {
        var steps = new List<NalvaWorkflowStepWrite>
        {
            new() { Key = "a", AgentKey = "finance", Tools = ["GetFinancialSummary"], ParallelGroup = 1 },
            new() { Key = "b", AgentKey = "analyst", Tools = ["GetExpenseAnalytics"], ParallelGroup = 1 },
            new() { Key = "c", AgentKey = "finance", Tools = ["GenerateFinancialReport"], ParallelGroup = 1 }
        };

        var groups = NalvaWorkflowGraph.Group(steps);
        groups.Should().HaveCount(2);
        groups[0].Should().HaveCount(2);
        groups[1].Single().Key.Should().Be("c");
    }

    [Fact]
    public void BusinessTemplates_ArePresentAndHaveNoCompanyData()
    {
        var keys = NalvaWorkflowCatalog.All.Select(template => template.Key).ToArray();
        keys.Should().Contain(["customer-onboarding", "lead-acquisition", "inventory-automation", "financial-monitoring", "project-operations", "customer-support-automation", "overdue-payment-followup"]);
        var json = System.Text.Json.JsonSerializer.Serialize(NalvaWorkflowCatalog.All);
        json.Should().NotContain("apiKey").And.NotContain("sk_live").And.NotContain("password");
    }

    [Fact]
    public void IntegrationCatalog_DoesNotClaimLiveProviderVerification()
    {
        IntegrationProviderCatalog.All.Should().HaveCount(15);
        IntegrationProviderCatalog.All.Should().OnlyContain(provider => provider.ProviderVerified == false);
        IntegrationProviderCatalog.All.Should().OnlyContain(provider => provider.ReadOperations.Count + provider.WriteOperations.Count > 0);
        IntegrationProviderCatalog.TryGet("gmail", out var gmail).Should().BeTrue();
        gmail!.WriteOperations.Should().Contain("send");
        gmail.ReadOperations.Should().Contain("list");
        IntegrationProviderCatalog.TryGet("meta", out var meta).Should().BeTrue();
        meta!.WriteOperations.Should().Contain("publish");
    }

    [Fact]
    public async Task IntegrationRows_StayInsideTheCompany()
    {
        var database = Guid.NewGuid().ToString();
        var left = TenantDatabase.Open(name: database);
        var right = TenantDatabase.Open(name: database);
        left.Context.CompanyIntegrationConnections.Add(new CompanyIntegrationConnection
        {
            ProviderKey = "gmail",
            Status = "Connected",
            OwnerUserId = "owner",
            AccessTokenProtected = "protected-value"
        });
        await left.Context.SaveChangesAsync();

        right.Context.CompanyIntegrationConnections.Should().BeEmpty();
        var stored = left.Context.CompanyIntegrationConnections.Single();
        stored.AccessTokenProtected.Should().Be("protected-value");
    }

    [Fact]
    public void EventNames_RejectArbitraryAndSecretShapedNames()
    {
        NalvaEventNames.IsAllowed(NalvaEventNames.CustomerRegistered).Should().BeTrue();
        NalvaEventNames.IsAllowed("company.invoice_reminder").Should().BeTrue();
        NalvaEventNames.IsAllowed("DropTable").Should().BeFalse();
        NalvaEventNames.IsAllowed("../secret").Should().BeFalse();
    }
}
