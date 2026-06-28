using BusinessOS.Application.Features.Suppliers.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierProducts;

public record GetSupplierProductsQuery(Guid SupplierId) : IRequest<IReadOnlyList<SupplierProductSummaryResponse>>;
