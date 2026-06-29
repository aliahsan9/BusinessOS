using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Notifications.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BusinessOS.UnitTests;

public static class TestHandlerDependencies
{
    public static IBusinessEventService CreateBusinessEvents() =>
        Mock.Of<IBusinessEventService>();

    public static IEntityAuditService CreateEntityAudit() =>
        Mock.Of<IEntityAuditService>();

    public static ILogger<T> CreateLogger<T>() =>
        Mock.Of<ILogger<T>>();
}
