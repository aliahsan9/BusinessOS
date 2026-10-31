using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Expenses.Commands.CreateExpense;
using BusinessOS.Application.Features.Expenses.Commands.CreateExpenseCategory;
using BusinessOS.Application.Features.Expenses.Commands.DeleteExpense;
using BusinessOS.Application.Features.Expenses.Commands.DeleteExpenseCategory;
using BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Application.Features.Payments.Commands.CreatePayment;
using BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.ReceivePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Application.Features.Quotations.Commands.ConvertQuotationToOrder;
using BusinessOS.Application.Features.Quotations.Commands.CreateQuotation;
using BusinessOS.Application.Features.Quotations.Queries;
using BusinessOS.Application.Features.Quotations.Services;
using BusinessOS.Application.Features.Suppliers.Commands.CreateSupplier;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class ExpenseWorkflowTests
{
    [Fact]
    public async Task CreateExpense_RoundsAmount_TrimsText_AndRejectsMissingCategory()
    {
        var (context, _, provider) = TenantDatabase.Open();
        var missing = () => Expenses(context, provider).Handle(ExpenseCommand(Guid.NewGuid(), 10), CancellationToken.None);
        await missing.Should().ThrowAsync<NotFoundException>().WithMessage("*category*");

        var categoryId = await Categories(context, provider).Handle(
            new CreateExpenseCategoryCommand("  Rent  ", "  "),
            CancellationToken.None);
        var duplicate = () => Categories(context, provider).Handle(
            new CreateExpenseCategoryCommand("Rent", null),
            CancellationToken.None);
        await duplicate.Should().ThrowAsync<ConflictException>().WithMessage("*already exists*");

        var id = await Expenses(context, provider).Handle(ExpenseCommand(categoryId, 10.126m), CancellationToken.None);
        var expense = await context.Expenses.SingleAsync(x => x.Id == id);
        expense.Amount.Should().Be(10.13m);
        expense.Title.Should().Be("Office rent");
        expense.Vendor.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExpense_IsTenantScoped_AndBlocksCategoryRemoval()
    {
        var database = Guid.NewGuid().ToString();
        var (home, _, homeProvider) = TenantDatabase.Open(name: database);
        var (other, _, otherProvider) = TenantDatabase.Open(name: database);
        var categoryId = await Categories(home, homeProvider).Handle(new CreateExpenseCategoryCommand("Fuel", null), CancellationToken.None);
        var expenseId = await Expenses(home, homeProvider).Handle(ExpenseCommand(categoryId, 5), CancellationToken.None);

        var crossTenant = () => new DeleteExpenseCommandHandler(
            other, TestHandlerDependencies.CreateCache(), otherProvider, TestHandlerDependencies.CreateLogger<DeleteExpenseCommandHandler>())
            .Handle(new DeleteExpenseCommand(expenseId), CancellationToken.None);
        await crossTenant.Should().ThrowAsync<NotFoundException>();
        (await home.Expenses.AnyAsync(x => x.Id == expenseId)).Should().BeTrue();

        var blocked = () => new DeleteExpenseCategoryCommandHandler(home, TestHandlerDependencies.CreateCache(), homeProvider)
            .Handle(new DeleteExpenseCategoryCommand(categoryId), CancellationToken.None);
        await blocked.Should().ThrowAsync<ConflictException>().WithMessage("*existing expenses*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CreateExpenseValidator_RejectsNonPositiveAmounts(decimal amount)
    {
        var result = new CreateExpenseCommandValidator().Validate(ExpenseCommand(Guid.NewGuid(), amount));
        result.IsValid.Should().BeFalse();
    }

    private static CreateExpenseCommand ExpenseCommand(Guid categoryId, decimal amount) =>
        new(
            "  Office rent  ",
            amount,
            DateTime.UtcNow,
            categoryId,
            PaymentMethodNames.Cash,
            "   ",
            null,
            null,
            null,
            ExpenseStatusNames.Pending,
            false,
            null);

    private static CreateExpenseCommandHandler Expenses(BusinessOSDbContext context, ITenantProvider provider) =>
        new(
            context,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateExpenseCommandHandler>());

    private static CreateExpenseCategoryCommandHandler Categories(BusinessOSDbContext context, ITenantProvider provider) =>
        new(context, TestHandlerDependencies.CreateCache(), provider);
}

public class PaymentAndInvoiceTests
{
    [Fact]
    public async Task CreatePayment_RoundsAmount_AndRejectsMismatchedCustomer()
    {
        var (context, provider, customer, order) = await SeedOrderAsync(OrderStatusNames.Completed, 100.129m);
        var otherCustomer = TestDataFactory.CreateCustomer(provider.TenantId, "other@test.com", "Other", "Buyer");
        context.Customers.Add(otherCustomer);
        await context.SaveChangesAsync();

        var mismatch = () => Payments(context, provider).Handle(
            PaymentCommand(order.Id, otherCustomer.Id, 10),
            CancellationToken.None);
        await mismatch.Should().ThrowAsync<BadRequestException>().WithMessage("*does not match*");

        var invalidMethod = () => Payments(context, provider).Handle(
            new CreatePaymentCommand(order.Id, customer.Id, 10, "Crypto", DateTime.UtcNow, null),
            CancellationToken.None);
        await invalidMethod.Should().ThrowAsync<BadRequestException>().WithMessage("*Invalid payment method*");

        var id = await Payments(context, provider).Handle(PaymentCommand(order.Id, customer.Id, 10.126m), CancellationToken.None);
        (await context.Payments.SingleAsync(x => x.Id == id)).Amount.Should().Be(10.13m);
    }

    [Fact]
    public async Task CreatePayment_CannotSeeAnotherTenantsOrder()
    {
        var database = Guid.NewGuid().ToString();
        var (_, _, _, order) = await SeedOrderAsync(OrderStatusNames.Completed, 40, database);
        var (other, otherProvider, otherCustomer, _) = await SeedOrderAsync(OrderStatusNames.Completed, 40, database);

        var act = () => Payments(other, otherProvider).Handle(
            PaymentCommand(order.Id, otherCustomer.Id, 5),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Order not found*");
    }

    [Fact]
    public async Task Invoice_CopiesCompletedOrderTotals_AndRejectsDuplicatesAndDrafts()
    {
        var (context, provider, _, order) = await SeedOrderAsync(OrderStatusNames.Pending, 80.456m);
        var pending = () => Invoices(context).Handle(new CreateInvoiceFromOrderCommand(order.Id, DateTime.UtcNow.AddDays(14), "  "), CancellationToken.None);
        await pending.Should().ThrowAsync<BadRequestException>().WithMessage("*completed orders*");

        order.Status = OrderStatusNames.Completed;
        await context.SaveChangesAsync();

        var id = await Invoices(context).Handle(
            new CreateInvoiceFromOrderCommand(order.Id, DateTime.UtcNow.AddDays(14), "  note  "),
            CancellationToken.None);
        var invoice = await context.Invoices.SingleAsync(x => x.Id == id);
        invoice.GrandTotal.Should().Be(80.456m);
        invoice.OutstandingAmount.Should().Be(80.456m);
        invoice.AmountPaid.Should().Be(0);
        invoice.Status.Should().Be(InvoiceStatusNames.Draft);
        invoice.Notes.Should().Be("note");

        var duplicate = () => Invoices(context).Handle(
            new CreateInvoiceFromOrderCommand(order.Id, DateTime.UtcNow.AddDays(7), null),
            CancellationToken.None);
        await duplicate.Should().ThrowAsync<ConflictException>().WithMessage("*already exists*");

        var jumpToPaid = () => Status(context, provider).Handle(
            new UpdateInvoiceStatusCommand(id, InvoiceStatusNames.Paid),
            CancellationToken.None);
        await jumpToPaid.Should().ThrowAsync<BadRequestException>().WithMessage("*Cannot transition*");
        (await context.Invoices.SingleAsync()).Status.Should().Be(InvoiceStatusNames.Draft);
    }

    private static CreatePaymentCommand PaymentCommand(Guid orderId, Guid customerId, decimal amount) =>
        new(orderId, customerId, amount, PaymentMethodNames.BankTransfer, DateTime.UtcNow, "  REF-1  ");

    private static CreatePaymentCommandHandler Payments(BusinessOSDbContext context, ITenantProvider provider) =>
        new(
            context,
            TestHandlerDependencies.CreateCache(),
            provider,
            new BusinessOS.Application.Common.Idempotency.IdempotencyService(
                context,
                provider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BusinessOS.Application.Common.Idempotency.IdempotencyService>.Instance),
            TestHandlerDependencies.CreateLogger<CreatePaymentCommandHandler>());

    private static CreateInvoiceFromOrderCommandHandler Invoices(BusinessOSDbContext context)
    {
        var numbers = new Mock<IInvoiceNumberGenerator>();
        numbers.Setup(x => x.GenerateNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("INV-100");
        var provider = new TenantProvider();
        provider.SetTenantId(context.Customers.Select(x => x.TenantId).First());
        return new CreateInvoiceFromOrderCommandHandler(
            context,
            numbers.Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateInvoiceFromOrderCommandHandler>());
    }

    private static UpdateInvoiceStatusCommandHandler Status(BusinessOSDbContext context, ITenantProvider provider) =>
        new(
            context,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<UpdateInvoiceStatusCommandHandler>());

    private static async Task<(BusinessOSDbContext Context, TenantProvider Provider, Customer Customer, Order Order)> SeedOrderAsync(
        string status,
        decimal grandTotal,
        string? database = null)
    {
        var (context, _, provider) = TenantDatabase.Open(name: database);
        var customer = TestDataFactory.CreateCustomer(provider.TenantId, $"{Guid.NewGuid():N}@test.com");
        var order = TestDataFactory.CreateOrder(provider.TenantId, customer.Id, status, grandTotal, $"ORD-{Guid.NewGuid():N}"[..18]);
        order.TotalAmount = grandTotal;
        order.Discount = 0;
        order.Tax = 0;
        context.AddRange(customer, order);
        await context.SaveChangesAsync();
        return (context, provider, customer, order);
    }
}

public class QuotationPurchaseAndSupplierTests
{
    [Fact]
    public async Task CreateQuotation_CalculatesTotal_AndRejectsNegativeOrForeignProduct()
    {
        var database = Guid.NewGuid().ToString();
        var (context, provider, customer, product) = await SeedCatalogAsync(database);
        var (other, _, _, foreignProduct) = await SeedCatalogAsync(database);

        var id = await Quotes(context, provider).Handle(
            Quote(customer.Id, product.Id, 2, 10.555m, discount: 1.10m, tax: 0.40m),
            CancellationToken.None);
        var quotation = await context.Quotations.Include(x => x.QuotationItems).SingleAsync(x => x.Id == id);
        quotation.SubTotal.Should().Be(21.11m);
        quotation.GrandTotal.Should().Be(20.41m);
        quotation.QuotationItems.Should().ContainSingle();

        var negative = () => Quotes(context, provider).Handle(
            Quote(customer.Id, product.Id, 1, 5, discount: 20, tax: 0),
            CancellationToken.None);
        await negative.Should().ThrowAsync<BadRequestException>().WithMessage("*cannot be negative*");

        var foreign = () => Quotes(context, provider).Handle(
            Quote(customer.Id, foreignProduct.Id, 1, 5, 0, 0),
            CancellationToken.None);
        await foreign.Should().ThrowAsync<BadRequestException>().WithMessage("*do not exist*");
        other.Should().NotBeNull();
    }

    [Fact]
    public async Task ConvertAcceptedQuotation_DeductsStock_AndDraftCannotConvert()
    {
        var (context, provider, customer, product) = await SeedCatalogAsync(stock: 2);
        context.Inventories.Add(TestDataFactory.CreateInventory(provider.TenantId, product.Id, 2));
        await context.SaveChangesAsync();

        var draftId = await Quotes(context, provider).Handle(Quote(customer.Id, product.Id, 2, 15, 0, 0), CancellationToken.None);
        var blocked = () => Convert(context, provider).Handle(new ConvertQuotationToOrderCommand(draftId), CancellationToken.None);
        await blocked.Should().ThrowAsync<ConflictException>().WithMessage("*cannot be converted*");

        var quotation = await context.Quotations.SingleAsync(x => x.Id == draftId);
        quotation.Status = QuotationStatusNames.Accepted;
        await context.SaveChangesAsync();

        var orderId = await Convert(context, provider).Handle(new ConvertQuotationToOrderCommand(draftId), CancellationToken.None);
        (await context.Orders.SingleAsync(x => x.Id == orderId)).GrandTotal.Should().Be(30);
        (await context.Inventories.SingleAsync(x => x.ProductId == product.Id)).CurrentStock.Should().Be(0);

        var second = () => Convert(context, provider).Handle(new ConvertQuotationToOrderCommand(draftId), CancellationToken.None);
        await second.Should().ThrowAsync<BadRequestException>().WithMessage("*Insufficient stock*");
        (await context.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PurchaseOrder_TotalsAndReceiveIncreaseStock()
    {
        var (context, provider, _, product) = await SeedCatalogAsync(stock: 4);
        context.Inventories.Add(TestDataFactory.CreateInventory(provider.TenantId, product.Id, 4));
        var supplierId = await Suppliers(context, provider).Handle(
            new CreateSupplierCommand("  Acme  ", " acme@test.com ", "123", "Street", null, "  "),
            CancellationToken.None);
        var duplicate = () => Suppliers(context, provider).Handle(
            new CreateSupplierCommand("Other", "acme@test.com", "123", "Street", null, null),
            CancellationToken.None);
        await duplicate.Should().ThrowAsync<ConflictException>().WithMessage("*already exists*");

        var received = () => Purchases(context, provider).Handle(
            new CreatePurchaseOrderCommand(
                supplierId,
                DateTime.UtcNow,
                PurchaseOrderStatusNames.Received,
                null,
                null,
                [new CreatePurchaseOrderLineItemDto(product.Id, 2, 4.5m)]),
            CancellationToken.None);
        await received.Should().ThrowAsync<BadRequestException>().WithMessage("*Draft or Pending*");

        var id = await Purchases(context, provider).Handle(
            new CreatePurchaseOrderCommand(
                supplierId,
                DateTime.UtcNow,
                PurchaseOrderStatusNames.Draft,
                "  PO-9  ",
                null,
                [new CreatePurchaseOrderLineItemDto(product.Id, 2, 4.555m)]),
            CancellationToken.None);
        (await context.Purchases.SingleAsync()).TotalAmount.Should().Be(9.11m);

        var purchase = await context.Purchases.SingleAsync(x => x.Id == id);
        purchase.Status = PurchaseOrderStatusNames.Approved;
        await context.SaveChangesAsync();

        await new ReceivePurchaseOrderCommandHandler(
            context,
            TenantDatabase.Inventory(context),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<ReceivePurchaseOrderCommandHandler>())
            .Handle(new ReceivePurchaseOrderCommand(id), CancellationToken.None);

        (await context.Purchases.SingleAsync()).Status.Should().Be(PurchaseOrderStatusNames.Received);
        (await context.Inventories.SingleAsync(x => x.ProductId == product.Id)).CurrentStock.Should().Be(6);
    }

    private static CreateQuotationCommand Quote(
        Guid customerId,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discount,
        decimal tax) =>
        new(
            customerId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            QuotationStatusNames.Draft,
            discount,
            tax,
            null,
            [new QuotationLineItemDto(productId, quantity, unitPrice)]);

    private static CreateQuotationCommandHandler Quotes(BusinessOSDbContext context, ITenantProvider provider)
    {
        var numbers = new Mock<IQuotationNumberGenerator>();
        numbers.Setup(x => x.GenerateNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("QT-100");
        return new CreateQuotationCommandHandler(
            context,
            numbers.Object,
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateQuotationCommandHandler>());
    }

    private static ConvertQuotationToOrderCommandHandler Convert(BusinessOSDbContext context, ITenantProvider provider)
    {
        var numbers = new Mock<IOrderNumberGenerator>();
        numbers.Setup(x => x.GenerateNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ORD-200");
        return new ConvertQuotationToOrderCommandHandler(
            context,
            numbers.Object,
            TenantDatabase.Inventory(context),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<ConvertQuotationToOrderCommandHandler>());
    }

    private static CreatePurchaseOrderCommandHandler Purchases(BusinessOSDbContext context, ITenantProvider provider) =>
        new(context, TestHandlerDependencies.CreateCache(), provider, TestHandlerDependencies.CreateLogger<CreatePurchaseOrderCommandHandler>());

    private static CreateSupplierCommandHandler Suppliers(BusinessOSDbContext context, ITenantProvider provider) =>
        new(context, TestHandlerDependencies.CreateCache(), provider);

    private static async Task<(BusinessOSDbContext Context, TenantProvider Provider, Customer Customer, Product Product)> SeedCatalogAsync(
        string? database = null,
        decimal stock = 10)
    {
        var (context, _, provider) = TenantDatabase.Open(name: database);
        var category = TestDataFactory.CreateCategory(provider.TenantId);
        var customer = TestDataFactory.CreateCustomer(provider.TenantId, $"{Guid.NewGuid():N}@test.com");
        var product = TestDataFactory.CreateProduct(provider.TenantId, category.Id, "Item", $"SKU-{Guid.NewGuid():N}", salePrice: 15, stock: stock);
        context.AddRange(category, customer, product);
        await context.SaveChangesAsync();
        return (context, provider, customer, product);
    }
}
