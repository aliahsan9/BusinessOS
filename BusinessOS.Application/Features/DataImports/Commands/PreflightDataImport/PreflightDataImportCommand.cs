using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.DataImports;
using BusinessOS.Application.Features.DataImports.Models;
using BusinessOS.Application.Features.DataImports.Services;
using BusinessOS.Domain.Enums;
using MediatR;

namespace BusinessOS.Application.Features.DataImports.Commands.PreflightDataImport;

public sealed record PreflightDataImportCommand(ImportPreflightRequest Request)
    : IRequest<ImportPreflightResponse>;

public sealed class PreflightDataImportCommandHandler
    : IRequestHandler<PreflightDataImportCommand, ImportPreflightResponse>
{
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IBillingService _billing;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserService _user;

    public PreflightDataImportCommandHandler(
        ISubscriptionEntitlementService entitlements,
        IBillingService billing,
        ITenantProvider tenantProvider,
        ICurrentUserService user)
    {
        _entitlements = entitlements;
        _billing = billing;
        _tenantProvider = tenantProvider;
        _user = user;
    }

    public async Task<ImportPreflightResponse> Handle(
        PreflightDataImportCommand command,
        CancellationToken cancellationToken)
    {
        var entityType = DataImportEntityTypes.NormalizeOrThrow(command.Request.EntityType);
        DataImportAccess.EnsureCanAccess(entityType, _user, write: true);

        var plan = await _billing.GetCurrentPlanAsync(cancellationToken);
        var status = Enum.TryParse<SubscriptionStatus>(plan.Status, true, out var parsed)
            ? parsed
            : SubscriptionStatus.Free;

        if (status is SubscriptionStatus.Expired or SubscriptionStatus.Cancelled)
        {
            return new ImportPreflightResponse(
                entityType,
                DataImportEntityTypes.QuotaResourceName(entityType),
                0,
                0,
                0,
                command.Request.CreateCount,
                command.Request.TotalRows,
                command.Request.CreateCount,
                false,
                status.ToString(),
                plan.PlanName,
                $"Subscription is {status}. Renew or upgrade before importing new records.",
                "Open Subscription to renew or upgrade.");
        }

        var capacity = await ImportPlanCapacityEvaluator.EvaluateAsync(
            entityType,
            command.Request.CreateCount,
            _entitlements,
            _tenantProvider.TenantId,
            cancellationToken);

        return ImportPlanCapacityEvaluator.ToPreflight(
            entityType,
            capacity,
            command.Request.TotalRows,
            plan.PlanName,
            status);
    }
}
