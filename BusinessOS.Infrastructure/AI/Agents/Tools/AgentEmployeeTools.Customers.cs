using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Customers.Commands.CreateCustomer;
using BusinessOS.Application.Features.Customers.Commands.DeleteCustomer;
using BusinessOS.Application.Features.Customers.Commands.UpdateCustomer;
using BusinessOS.Application.Features.Finance.Services;
using BusinessOS.Application.Features.Inventory.Commands.AdjustStock;
using BusinessOS.Application.Features.Inventory.Commands.IncreaseStock;
using BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;
using BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Application.Features.Products.Commands.DeleteProduct;
using BusinessOS.Application.Features.Products.Commands.UpdateProduct;
using BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.ReceivePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrderStatus;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Application.Features.Settings.DTOs;
using BusinessOS.Application.Features.Settings.Services;
using BusinessOS.Application.Features.Suppliers.Commands.CreateSupplier;
using BusinessOS.Application.Features.Suppliers.Commands.DeleteSupplier;
using BusinessOS.Application.Features.Suppliers.Commands.UpdateSupplier;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Tools;

internal static class AgentToolArgHelpers
{
    public static string? Str(JsonElement? args, string name)
    {
        if (args is null || args.Value.ValueKind != JsonValueKind.Object)
            return null;
        if (!args.Value.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    public static Guid? GuidVal(JsonElement? args, string name)
    {
        var s = Str(args, name);
        return Guid.TryParse(s, out var g) ? g : null;
    }

    public static decimal? Dec(JsonElement? args, string name)
    {
        if (args is null || !args.Value.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d))
            return d;
        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var ds))
            return ds;
        return null;
    }

    public static int? Int(JsonElement? args, string name)
    {
        var d = Dec(args, name);
        return d is null ? null : (int)d.Value;
    }

    public static bool? Bool(JsonElement? args, string name)
    {
        if (args is null || !args.Value.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(p.GetString(), out var b) => b,
            _ => null
        };
    }

    public static (string First, string Last) SplitName(string? full, string? first, string? last)
    {
        if (!string.IsNullOrWhiteSpace(first))
            return (first.Trim(), string.IsNullOrWhiteSpace(last) ? "." : last.Trim());
        if (string.IsNullOrWhiteSpace(full))
            return ("Customer", "Unknown");
        var parts = full.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return (parts[0], ".");
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }
}

public sealed class StructuredCreateCustomerTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<StructuredCreateCustomerTool> _logger;

    public StructuredCreateCustomerTool(IMediator mediator, ILogger<StructuredCreateCustomerTool> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreateCustomer;
    public override string Description => "Create a customer from structured fields extracted from natural language.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.CustomerCreate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CreateCustomer);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate
        && ContainsAny(message, "customer", "client", "گاہک", "کسٹمر");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context,
        JsonElement args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (first, last) = AgentToolArgHelpers.SplitName(
                AgentToolArgHelpers.Str(args, "fullName"),
                AgentToolArgHelpers.Str(args, "firstName"),
                AgentToolArgHelpers.Str(args, "lastName"));

            var email = AgentToolArgHelpers.Str(args, "email")
                ?? $"customer-{Guid.NewGuid().ToString("N")[..8]}@businessos.local";
            var phone = AgentToolArgHelpers.Str(args, "phone") ?? "+0000000000";
            var address = AgentToolArgHelpers.Str(args, "address") ?? "N/A";
            var city = AgentToolArgHelpers.Str(args, "city") ?? "N/A";
            var country = AgentToolArgHelpers.Str(args, "country") ?? "Pakistan";
            var postal = AgentToolArgHelpers.Str(args, "postalCode") ?? "00000";

            var id = await _mediator.Send(new CreateCustomerCommand(
                first, last, email, phone, address, city, country, postal,
                AgentToolArgHelpers.Str(args, "company"),
                AgentToolArgHelpers.Str(args, "notes")), cancellationToken);

            if (context.ExecutionState is not null)
            {
                context.ExecutionState.CustomerId = id;
                context.ExecutionState.CustomerName = $"{first} {last}".Trim();
            }

            var display = $"{first} {last}".Trim();
            var isUrdu = string.Equals(context.Language, "ur", StringComparison.OrdinalIgnoreCase);
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = true,
                Summary = isUrdu
                    ? $"{display} کامیابی سے شامل کر دیا گیا ہے"
                    : $"Customer \"{display}\" has been created successfully.",
                ActionResult = new AiActionResultDto
                {
                    Action = "CreateCustomer",
                    Success = true,
                    Message = isUrdu
                        ? $"{display} کامیابی سے شامل کر دیا گیا ہے"
                        : $"Customer \"{display}\" has been created successfully.",
                    EntityType = "Customer",
                    EntityId = id,
                    Route = $"/customers/{id}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Structured CreateCustomer failed");
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = ex.Message
            };
        }
    }
}

public sealed class SearchCustomerTool : AiToolBase
{
    private readonly IApplicationDbContext _db;

    public SearchCustomerTool(IApplicationDbContext db) => _db = db;

    public override AiToolName ToolName => AiToolName.SearchCustomer;
    public override string Description => "Search customers by name, phone, or email.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.CustomerView];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.SearchCustomer);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionRead or AiCopilotIntent.FollowUp
        && ContainsAny(message, "search customer", "find customer", "گاہک تلاش", "کسٹمر تلاش");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context,
        JsonElement args,
        CancellationToken cancellationToken = default)
    {
        var query = AgentToolArgHelpers.Str(args, "query")
            ?? AgentToolArgHelpers.Str(args, "phone")
            ?? AgentToolArgHelpers.Str(args, "email")
            ?? context.Message;

        var q = query.Trim().ToLowerInvariant();
        var matches = await _db.Customers.AsNoTracking()
            .Where(c =>
                c.FirstName.ToLower().Contains(q)
                || c.LastName.ToLower().Contains(q)
                || (c.Email != null && c.Email.ToLower().Contains(q))
                || (c.PhoneNumber != null && c.PhoneNumber.Contains(q)))
            .OrderBy(c => c.FirstName)
            .Take(10)
            .Select(c => new { c.Id, Name = c.FirstName + " " + c.LastName, c.Email, c.PhoneNumber })
            .ToListAsync(cancellationToken);

        if (matches.Count == 1 && context.ExecutionState is not null)
        {
            context.ExecutionState.CustomerId = matches[0].Id;
            context.ExecutionState.CustomerName = matches[0].Name;
        }

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = matches.Count > 0,
            Summary = matches.Count == 0
                ? $"No customers found for \"{query}\"."
                : $"Found {matches.Count} customer(s): "
                  + string.Join("; ", matches.Select(m => $"{m.Name} ({m.PhoneNumber})")),
            Data = matches,
            ActionResult = matches.Count == 1
                ? new AiActionResultDto
                {
                    Action = "SearchCustomer",
                    Success = true,
                    Message = $"Found customer \"{matches[0].Name}\".",
                    EntityType = "Customer",
                    EntityId = matches[0].Id,
                    Route = $"/customers/{matches[0].Id}"
                }
                : null
        };
    }
}

public sealed class UpdateCustomerTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public UpdateCustomerTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.UpdateCustomer;
    public override string Description => "Update an existing customer.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.CustomerUpdate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.UpdateCustomer);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "update customer", "edit customer", "change customer", "کسٹمر اپڈیٹ");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context,
        JsonElement args,
        CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "customerId")
            ?? context.ExecutionState?.CustomerId
            ?? context.Page.CustomerId;

        if (id is null)
        {
            var name = AgentToolArgHelpers.Str(args, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var q = name.ToLowerInvariant();
                id = await _db.Customers.AsNoTracking()
                    .Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(q))
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (id is null)
            return Fail("Customer not found. Please provide a customer name or ID.");

        var existing = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null)
            return Fail("Customer not found.");

        var (first, last) = AgentToolArgHelpers.SplitName(
            AgentToolArgHelpers.Str(args, "fullName"),
            AgentToolArgHelpers.Str(args, "firstName") ?? existing.FirstName,
            AgentToolArgHelpers.Str(args, "lastName") ?? existing.LastName);

        await _mediator.Send(new UpdateCustomerCommand(
            id.Value,
            first,
            last,
            AgentToolArgHelpers.Str(args, "email") ?? existing.Email,
            AgentToolArgHelpers.Str(args, "phone") ?? existing.PhoneNumber,
            AgentToolArgHelpers.Str(args, "address") ?? existing.Address,
            AgentToolArgHelpers.Str(args, "city") ?? existing.City,
            AgentToolArgHelpers.Str(args, "country") ?? existing.Country,
            AgentToolArgHelpers.Str(args, "postalCode") ?? existing.PostalCode,
            AgentToolArgHelpers.Bool(args, "isActive") ?? existing.IsActive,
            AgentToolArgHelpers.Str(args, "company") ?? existing.Company,
            AgentToolArgHelpers.Str(args, "notes") ?? existing.Notes), cancellationToken);

        return Ok($"Customer \"{first} {last}\" was updated.", "Customer", id.Value, $"/customers/{id}");
    }

    private AiToolResult Fail(string msg) => new() { ToolName = ToolName.ToString(), Success = false, Summary = msg };
    private AiToolResult Ok(string msg, string type, Guid id, string route) => new()
    {
        ToolName = ToolName.ToString(),
        Success = true,
        Summary = msg,
        ActionResult = new AiActionResultDto
        {
            Action = ToolName.ToString(),
            Success = true,
            Message = msg,
            EntityType = type,
            EntityId = id,
            Route = route
        }
    };
}

public sealed class DeleteCustomerTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public DeleteCustomerTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.DeleteCustomer;
    public override string Description => "Delete a customer.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.CustomerDelete];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.DeleteCustomer);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "delete customer", "remove customer", "کسٹمر حذف");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context,
        JsonElement args,
        CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "customerId")
            ?? context.ExecutionState?.CustomerId
            ?? context.Page.CustomerId;

        if (id is null)
        {
            var name = AgentToolArgHelpers.Str(args, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var q = name.ToLowerInvariant();
                id = await _db.Customers.AsNoTracking()
                    .Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(q))
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Customer not found." };

        await _mediator.Send(new DeleteCustomerCommand(id.Value), cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = "Customer deleted successfully.",
            ActionResult = new AiActionResultDto
            {
                Action = "DeleteCustomer",
                Success = true,
                Message = "Customer deleted successfully.",
                EntityType = "Customer",
                EntityId = id
            }
        };
    }
}
