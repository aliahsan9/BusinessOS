using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Payments.Queries;

public static class PaymentProjections
{
    public static readonly Expression<Func<Payment, PaymentSummaryResponse>> ToSummary = x =>
        new PaymentSummaryResponse
        {
            Id = x.Id,
            OrderId = x.OrderId,
            OrderNumber = x.Order.OrderNumber,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
            Amount = x.Amount,
            PaymentMethod = x.PaymentMethod,
            PaymentDate = x.PaymentDate,
            ReferenceNo = x.ReferenceNo,
            CreatedAt = x.CreatedAt
        };

    public static readonly Expression<Func<Payment, PaymentResponse>> ToDetail = x =>
        new PaymentResponse
        {
            Id = x.Id,
            OrderId = x.OrderId,
            OrderNumber = x.Order.OrderNumber,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
            Amount = x.Amount,
            PaymentMethod = x.PaymentMethod,
            PaymentDate = x.PaymentDate,
            ReferenceNo = x.ReferenceNo,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
}
