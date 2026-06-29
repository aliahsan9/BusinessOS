using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Audit;

public static class EntityAuditSnapshots
{
    public static object CustomerSnapshot(Customer customer) =>
        new
        {
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.PhoneNumber,
            customer.Address,
            customer.City,
            customer.Country,
            customer.PostalCode,
            customer.IsActive
        };

    public static object InvoiceSnapshot(Invoice invoice) =>
        new
        {
            invoice.InvoiceNumber,
            invoice.Status,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.SubTotal,
            invoice.Discount,
            invoice.Tax,
            invoice.GrandTotal,
            invoice.AmountPaid,
            invoice.OutstandingAmount,
            invoice.Notes
        };

    public static object ExpenseSnapshot(Expense expense) =>
        new
        {
            expense.Title,
            expense.Amount,
            expense.ExpenseDate,
            expense.ExpenseCategoryId,
            expense.PaymentMethod,
            expense.Vendor,
            expense.ReferenceNumber,
            expense.Description,
            expense.Status,
            expense.IsRecurring
        };

    public static object ProjectSnapshot(Project project) =>
        new
        {
            project.Name,
            project.Description,
            project.Status,
            project.AssignedUserId,
            project.CustomerId
        };
}
