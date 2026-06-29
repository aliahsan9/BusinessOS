using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Customers.Commands.CreateCustomer;
using BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI;

public sealed class AiActionService : IAiActionService
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AiActionService> _logger;

    public AiActionService(
        IMediator mediator,
        IApplicationDbContext context,
        ILogger<AiActionService> logger)
    {
        _mediator = mediator;
        _context = context;
        _logger = logger;
    }

    public async Task<AiActionResultDto?> TryExecuteAsync(
        string message,
        AiPageContextDto page,
        CancellationToken cancellationToken = default)
    {
        var text = message.Trim();
        var lower = text.ToLowerInvariant();

        if (IsCreateCustomerCommand(lower))
            return await CreateCustomerAsync(text, cancellationToken);

        if (IsCreateProjectCommand(lower))
            return await CreateProjectAsync(page, cancellationToken);

        if (IsCreateTaskCommand(lower))
            return await CreateTaskAsync(text, page, cancellationToken);

        if (IsCreateInvoiceCommand(lower))
            return await CreateInvoiceAsync(page, cancellationToken);

        return null;
    }

    private static bool IsCreateCustomerCommand(string lower) =>
        Regex.IsMatch(lower, @"\b(create|add|new)\b.*\bcustomer\b");

    private static bool IsCreateProjectCommand(string lower) =>
        Regex.IsMatch(lower, @"\b(create|add|new)\b.*\b(project|order)\b");

    private static bool IsCreateTaskCommand(string lower) =>
        Regex.IsMatch(lower, @"\b(create|add|new)\b.*\btask\b");

    private static bool IsCreateInvoiceCommand(string lower) =>
        Regex.IsMatch(lower, @"\b(create|generate|new)\b.*\binvoice\b");

    private async Task<AiActionResultDto> CreateCustomerAsync(
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            var (firstName, lastName) = ParseName(message);
            var email = ParseEmail(message) ?? $"customer-{Guid.NewGuid():N[..8]}@businessos.local";
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];

            var command = new CreateCustomerCommand(
                FirstName: firstName,
                LastName: lastName,
                Email: email.Contains('@') ? email : $"customer-{uniqueSuffix}@businessos.local",
                PhoneNumber: ParsePhone(message) ?? "+0000000000",
                Address: "TBD",
                City: "TBD",
                Country: "TBD",
                PostalCode: "00000");

            var id = await _mediator.Send(command, cancellationToken);

            return new AiActionResultDto
            {
                Action = "CreateCustomer",
                Success = true,
                Message = $"Customer \"{firstName} {lastName}\" was created successfully.",
                EntityType = "Customer",
                EntityId = id,
                Route = $"/customers/{id}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI action CreateCustomer failed");
            return new AiActionResultDto
            {
                Action = "CreateCustomer",
                Success = false,
                Message = $"Could not create customer: {ex.Message}"
            };
        }
    }

    private async Task<AiActionResultDto> CreateProjectAsync(
        AiPageContextDto page,
        CancellationToken cancellationToken)
    {
        try
        {
            var customerId = page.CustomerId
                ?? await _context.Customers
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (customerId == Guid.Empty)
            {
                return new AiActionResultDto
                {
                    Action = "CreateProject",
                    Success = false,
                    Message = "No active customer found. Create a customer first or open a customer detail page."
                };
            }

            var productId = await _context.Products
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (productId == Guid.Empty)
            {
                return new AiActionResultDto
                {
                    Action = "CreateProject",
                    Success = false,
                    Message = "No active products found. Add a product before creating a project/order."
                };
            }

            var command = new CreateOrderCommand(
                CustomerId: customerId,
                Discount: 0,
                Tax: 0,
                Items: [new CreateOrderItemDto(productId, 1)]);

            var id = await _mediator.Send(command, cancellationToken);

            return new AiActionResultDto
            {
                Action = "CreateProject",
                Success = true,
                Message = "Project (order) was created successfully with one default line item.",
                EntityType = "Order",
                EntityId = id,
                Route = $"/orders/{id}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI action CreateProject failed");
            return new AiActionResultDto
            {
                Action = "CreateProject",
                Success = false,
                Message = $"Could not create project: {ex.Message}"
            };
        }
    }

    private async Task<AiActionResultDto> CreateTaskAsync(
        string message,
        AiPageContextDto page,
        CancellationToken cancellationToken)
    {
        try
        {
            var title = ParseTaskTitle(message) ?? "New Task";

            if (page.ProjectId is Guid projectId)
            {
                var projectExists = await _context.Projects
                    .AnyAsync(x => x.Id == projectId, cancellationToken);

                if (projectExists)
                {
                    var task = new WorkTask
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Title = title,
                        Status = WorkTaskStatus.Todo,
                        Priority = 2
                    };

                    _context.WorkTasks.Add(task);
                    await _context.SaveChangesAsync(cancellationToken);

                    return new AiActionResultDto
                    {
                        Action = "CreateTask",
                        Success = true,
                        Message = $"Task \"{title}\" was added to the project.",
                        EntityType = "WorkTask",
                        EntityId = task.Id
                    };
                }
            }

            if (page.OrderId is Guid orderId)
            {
                return new AiActionResultDto
                {
                    Action = "CreateTask",
                    Success = false,
                    Message = "To add tasks to this project (order), edit the order and add line items on the order detail page."
                };
            }

            var defaultProjectId = await _context.Projects
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultProjectId != Guid.Empty)
            {
                var task = new WorkTask
                {
                    Id = Guid.NewGuid(),
                    ProjectId = defaultProjectId,
                    Title = title,
                    Status = WorkTaskStatus.Todo,
                    Priority = 2
                };

                _context.WorkTasks.Add(task);
                await _context.SaveChangesAsync(cancellationToken);

                return new AiActionResultDto
                {
                    Action = "CreateTask",
                    Success = true,
                    Message = $"Task \"{title}\" was created on the most recent project.",
                    EntityType = "WorkTask",
                    EntityId = task.Id
                };
            }

            return new AiActionResultDto
            {
                Action = "CreateTask",
                Success = false,
                Message = "No project available for task creation. Create a project first or open a project detail page."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI action CreateTask failed");
            return new AiActionResultDto
            {
                Action = "CreateTask",
                Success = false,
                Message = $"Could not create task: {ex.Message}"
            };
        }
    }

    private async Task<AiActionResultDto> CreateInvoiceAsync(
        AiPageContextDto page,
        CancellationToken cancellationToken)
    {
        try
        {
            Guid? orderId = page.OrderId;

            if (orderId is null && page.CustomerId is Guid customerId)
            {
                orderId = await _context.Orders
                    .AsNoTracking()
                    .Where(x => x.CustomerId == customerId && x.Status == "Completed")
                    .OrderByDescending(x => x.OrderDate)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (orderId is null || orderId == Guid.Empty)
            {
                orderId = await _context.Orders
                    .AsNoTracking()
                    .Where(x => x.Status == "Completed")
                    .OrderByDescending(x => x.OrderDate)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (orderId is null || orderId == Guid.Empty)
            {
                return new AiActionResultDto
                {
                    Action = "CreateInvoice",
                    Success = false,
                    Message = "No completed order found. Complete an order before generating an invoice."
                };
            }

            var command = new CreateInvoiceFromOrderCommand(
                OrderId: orderId.Value,
                DueDate: DateTime.UtcNow.AddDays(30),
                Notes: "Created via BusinessOS AI assistant");

            var id = await _mediator.Send(command, cancellationToken);

            return new AiActionResultDto
            {
                Action = "CreateInvoice",
                Success = true,
                Message = "Invoice was created successfully from the completed order.",
                EntityType = "Invoice",
                EntityId = id,
                Route = $"/invoices/{id}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI action CreateInvoice failed");
            return new AiActionResultDto
            {
                Action = "CreateInvoice",
                Success = false,
                Message = $"Could not create invoice: {ex.Message}"
            };
        }
    }

    private static (string FirstName, string LastName) ParseName(string message)
    {
        var match = Regex.Match(
            message,
            @"(?:customer\s+(?:named\s+)?|called\s+)([A-Za-z]+)(?:\s+([A-Za-z]+))?",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var first = match.Groups[1].Value;
            var last = match.Groups[2].Success ? match.Groups[2].Value : "Customer";
            return (first, last);
        }

        return ("New", "Customer");
    }

    private static string? ParseEmail(string message)
    {
        var match = Regex.Match(message, @"[\w.+-]+@[\w.-]+\.\w+");
        return match.Success ? match.Value : null;
    }

    private static string? ParsePhone(string message)
    {
        var match = Regex.Match(message, @"\+?\d[\d\s\-()]{7,}\d");
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? ParseTaskTitle(string message)
    {
        var match = Regex.Match(
            message,
            @"(?:task\s+(?:named|called|titled)?\s*[""']?([^""'\n]+)[""']?)",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
