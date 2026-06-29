using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BusinessOS.UnitTests;

public static class TestHandlerDependencies
{
    public static IBusinessEventService CreateBusinessEvents() =>
        Mock.Of<IBusinessEventService>();

    public static ILogger<T> CreateLogger<T>() =>
        Mock.Of<ILogger<T>>();
}
