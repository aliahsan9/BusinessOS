using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Application.Features.Payments.Commands.CreatePayment;
using BusinessOS.Application.Features.Quotations.Commands.ConvertQuotationToOrder;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class BusinessWorkflowService : IBusinessWorkflowService
{
    private readonly IApplicationDbContext _db;
    private readonly ICrmOperationsService _crm;
    private readonly ICommerceOperationsService _commerce;
    private readonly ISender _sender;
    private readonly IOrderNumberGenerator _orderNumbers;
    private readonly IInventoryService _inventory;

    public BusinessWorkflowService(
        IApplicationDbContext db,
        ICrmOperationsService crm,
        ICommerceOperationsService commerce,
        ISender sender,
        IOrderNumberGenerator orderNumbers,
        IInventoryService inventory)
    {
        _db = db;
        _crm = crm;
        _commerce = commerce;
        _sender = sender;
        _orderNumbers = orderNumbers;
        _inventory = inventory;
    }

    public Task<ConvertLeadResult> LeadToCustomerAsync(Guid leadId, CancellationToken cancellationToken = default) =>
        _crm.ConvertLeadAsync(leadId, true, cancellationToken);

    public async Task<WorkflowIdsDto> QuotationToPaymentAsync(QuotationPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var existingOrderId = await _db.Orders.Where(x => x.QuotationId == request.QuotationId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var orderId = existingOrderId ?? await _sender.Send(new ConvertQuotationToOrderCommand(request.QuotationId), cancellationToken);
        var invoiceId = await _db.Invoices.Where(x => x.OrderId == orderId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken)
            ?? await _sender.Send(new CreateInvoiceFromOrderCommand(orderId, request.DueDate, "Created from accepted quotation"), cancellationToken);
        Guid? paymentId = null;
        if (request.RecordPayment && request.PaymentAmount is > 0)
        {
            var customerId = await _db.Orders.Where(x => x.Id == orderId).Select(x => x.CustomerId).FirstAsync(cancellationToken);
            paymentId = await _sender.Send(new CreatePaymentCommand(
                orderId,
                customerId,
                request.PaymentAmount.Value,
                string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Bank transfer" : request.PaymentMethod,
                DateTime.UtcNow,
                request.Reference,
                $"quote-pay-{request.QuotationId:N}-{request.PaymentAmount}"), cancellationToken);
        }
        return new WorkflowIdsDto(null, null, null, request.QuotationId, orderId, invoiceId, paymentId, null, null, null);
    }

    public async Task<PurchaseInvoiceDto> PurchaseToInvoiceAsync(Guid purchaseId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.PurchaseId == purchaseId, cancellationToken);
        if (existing is not null)
            return new PurchaseInvoiceDto(existing.Id, existing.InvoiceNumber, existing.SupplierId, existing.Total, existing.AmountPaid, existing.Status, existing.DueDate);
        var purchase = await _db.Purchases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == purchaseId, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found.");
        return await _commerce.SavePurchaseInvoiceAsync(new PurchaseInvoiceRequest(
            purchase.ReferenceNumber ?? Ops.Number("PINV"),
            purchase.SupplierId,
            purchase.Id,
            purchase.PurchaseDate,
            purchase.PurchaseDate.AddDays(30),
            purchase.TotalAmount,
            0,
            purchase.Notes,
            null), cancellationToken);
    }

    public Task<ReturnDto> SettleReturnAsync(Guid returnId, ReturnDecisionRequest request, CancellationToken cancellationToken = default) =>
        _commerce.DecideReturnAsync(returnId, request, cancellationToken);

    public async Task<ProjectDto> DealToProjectAsync(Guid dealId, CancellationToken cancellationToken = default)
    {
        var deal = await _crm.WinDealAsync(dealId, true, cancellationToken);
        var project = await _db.Projects.FirstAsync(x => x.Id == deal.ProjectId, cancellationToken);
        return new ProjectDto(project.Id, project.Name, project.Code, project.Description, project.CustomerId, project.Status.ToString(),
            project.ManagerUserId, project.StartDate, project.Deadline, project.Budget, project.Priority, project.Workflow, project.Visibility, project.DealId);
    }

    public async Task<WorkflowIdsDto> BillProjectAsync(ProjectBillingRequest request, CancellationToken cancellationToken = default)
    {
        var entries = await _db.TimeEntries
            .Where(x => x.ProjectId == request.ProjectId && x.IsBillable && x.Status == TimeEntryStatuses.Approved)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
            throw new BadRequestException("There is no approved billable time to invoice.");
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        if (project.CustomerId is null)
            throw new BadRequestException("The project needs a customer before it can be billed.");
        var productId = request.ServiceProductId ?? await EnsureServiceProductAsync(cancellationToken);
        var hours = entries.Sum(x => x.Hours);
        var amount = Math.Round(entries.Sum(x => x.Hours * (x.HourlyRate ?? 0)), 2);
        if (amount <= 0)
            throw new BadRequestException("Billable time needs an hourly rate before invoicing.");
        var unitPrice = Math.Round(amount / hours, 2);
        var order = new Order
        {
            CustomerId = project.CustomerId.Value,
            OrderNumber = await _orderNumbers.GenerateNextAsync(cancellationToken),
            OrderDate = DateTime.UtcNow,
            Status = OrderStatusNames.Confirmed,
            TotalAmount = amount,
            GrandTotal = amount,
            PaymentStatus = "Unpaid",
            OrderItems =
            [
                new OrderItem
                {
                    ProductId = productId,
                    Quantity = hours,
                    UnitPrice = unitPrice,
                    UnitCost = 0,
                    Total = amount
                }
            ]
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        var product = await _db.Products.AsNoTracking().FirstAsync(x => x.Id == productId, cancellationToken);
        if (!string.Equals(product.Kind, ProductKinds.Service, StringComparison.OrdinalIgnoreCase))
        {
            await _inventory.DeductForOrderAsync(order, order.OrderItems, cancellationToken);
        }
        var invoiceId = await _sender.Send(new CreateInvoiceFromOrderCommand(order.Id, request.DueDate, $"Project {project.Name} billable time"), cancellationToken);
        return new WorkflowIdsDto(null, project.CustomerId, project.DealId, null, order.Id, invoiceId, null, project.Id, null, null);
    }

    private async Task<Guid> EnsureServiceProductAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Products.Where(x => x.Kind == ProductKinds.Service && x.SKU == "SVC-TIME").Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return existing.Value;
        var category = await _db.Categories.FirstOrDefaultAsync(x => x.Name == "Services", cancellationToken);
        if (category is null)
        {
            category = new Category { Name = "Services", Description = "Professional services" };
            _db.Categories.Add(category);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return await _commerce.SaveCatalogItemAsync(new CatalogItemRequest(
            category.Id, "Professional services", "SVC-TIME", "Billable project time", 0, 0, ProductKinds.Service,
            null, "hour", 0, "Hourly", null, null, null), cancellationToken);
    }
}
