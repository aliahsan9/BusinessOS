using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI;

public sealed class AiContextService : IAiContextService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AiContextService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public AiPageContextDto BuildPageContext(AiChatRequest request)
    {
        var url = request.CurrentPage?.Trim() ?? string.Empty;
        var lower = url.ToLowerInvariant();

        var customerId = request.CustomerId ?? ExtractGuidFromUrl(lower, "customers");
        var orderId = request.OrderId ?? ExtractGuidFromUrl(lower, "orders");
        var invoiceId = request.InvoiceId ?? ExtractGuidFromUrl(lower, "invoices");
        var projectId = request.ProjectId;

        return new AiPageContextDto
        {
            Url = url,
            Module = ResolveModule(lower),
            CustomerId = customerId,
            OrderId = orderId,
            InvoiceId = invoiceId,
            ProjectId = projectId
        };
    }

    public async Task<AiContextDto> BuildContextAsync(
        AiChatRequest request,
        AiRetrievalScope scope,
        CancellationToken cancellationToken = default)
    {
        var page = BuildPageContext(request);
        var user = BuildUserContext();

        var context = new AiContextDto
        {
            User = user,
            Page = page
        };

        return scope switch
        {
            AiRetrievalScope.CurrentCustomer when page.CustomerId is not null =>
                await LoadCustomerBundleAsync(page.CustomerId.Value, cancellationToken),
            AiRetrievalScope.CustomerBundle when page.CustomerId is not null =>
                await LoadCustomerBundleAsync(page.CustomerId.Value, cancellationToken),
            AiRetrievalScope.CurrentOrder when page.OrderId is not null =>
                await LoadOrderContextAsync(page.OrderId.Value, cancellationToken),
            AiRetrievalScope.CurrentInvoice when page.InvoiceId is not null =>
                await LoadInvoiceContextAsync(page.InvoiceId.Value, cancellationToken),
            AiRetrievalScope.CurrentProject when page.ProjectId is not null =>
                await LoadProjectContextAsync(page.ProjectId.Value, cancellationToken),
            AiRetrievalScope.OverdueInvoices =>
                await LoadOverdueInvoicesAsync(cancellationToken),
            AiRetrievalScope.RevenueRanking =>
                await LoadRevenueRankingAsync(cancellationToken),
            AiRetrievalScope.ProjectProgress =>
                await LoadProjectProgressAsync(cancellationToken),
            AiRetrievalScope.PageDefaults =>
                await LoadPageDefaultsAsync(page, cancellationToken),
            _ => new AiContextDto { User = user, Page = page }
        };
    }

    private AiUserContextDto BuildUserContext() =>
        new()
        {
            UserId = _currentUser.UserId ?? "unknown",
            Email = _currentUser.Email,
            Roles = _currentUser.Roles
        };

    private async Task<AiContextDto> LoadCustomerBundleAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var page = new AiPageContextDto { CustomerId = customerId, Module = "customers" };
        var user = BuildUserContext();

        var customer = await _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName,
                FullName = x.FirstName + " " + x.LastName,
                x.Email,
                x.PhoneNumber,
                x.City,
                x.Country,
                x.IsActive,
                x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return new AiContextDto { User = user, Page = page };
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.OrderDate)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                x.OrderDate,
                x.Status,
                x.GrandTotal
            })
            .ToListAsync(cancellationToken);

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.InvoiceDate)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.InvoiceDate,
                x.DueDate,
                x.Status,
                x.GrandTotal,
                x.AmountPaid,
                x.OutstandingAmount
            })
            .ToListAsync(cancellationToken);

        var orderStats = await _context.Orders
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalSpending = g.Sum(x => x.GrandTotal),
                AverageOrderValue = g.Any() ? g.Average(x => x.GrandTotal) : 0m,
                LastOrderDate = g.Max(x => (DateTime?)x.OrderDate),
                TotalCompletedOrders = g.Count(x => x.Status == "Completed")
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new AiContextDto
        {
            User = user,
            Page = new AiPageContextDto
            {
                CustomerId = customerId,
                Module = "customers"
            },
            Customer = customer,
            Orders = orders.Cast<object>().ToList(),
            Invoices = invoices.Cast<object>().ToList(),
            Analytics = orderStats
        };
    }

    private async Task<AiContextDto> LoadOrderContextAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var user = BuildUserContext();
        var page = new AiPageContextDto { OrderId = orderId, Module = "orders" };

        var order = await _context.Orders
            .AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                x.CustomerId,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                x.OrderDate,
                x.Status,
                x.GrandTotal,
                Items = x.OrderItems.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product != null ? i.Product.Name : "Unknown",
                    i.Quantity,
                    i.UnitPrice,
                    i.Total
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return new AiContextDto { User = user, Page = page };
        }

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.Status,
                x.GrandTotal,
                x.OutstandingAmount
            })
            .ToListAsync(cancellationToken);

        return new AiContextDto
        {
            User = user,
            Page = new AiPageContextDto
            {
                OrderId = orderId,
                CustomerId = order.CustomerId,
                Module = "orders"
            },
            Customer = new { order.CustomerId, order.CustomerName },
            Orders = [order],
            Invoices = invoices.Cast<object>().ToList()
        };
    }

    private async Task<AiContextDto> LoadInvoiceContextAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var user = BuildUserContext();
        var page = new AiPageContextDto { InvoiceId = invoiceId, Module = "invoices" };

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Id == invoiceId)
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.OrderId,
                OrderNumber = x.Order.OrderNumber,
                x.CustomerId,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                x.InvoiceDate,
                x.DueDate,
                x.Status,
                x.GrandTotal,
                x.AmountPaid,
                x.OutstandingAmount,
                x.Notes
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
        {
            return new AiContextDto { User = user, Page = page };
        }

        return new AiContextDto
        {
            User = user,
            Page = new AiPageContextDto
            {
                InvoiceId = invoiceId,
                OrderId = invoice.OrderId,
                CustomerId = invoice.CustomerId,
                Module = "invoices"
            },
            Customer = new { invoice.CustomerId, invoice.CustomerName },
            Invoices = [invoice]
        };
    }

    private async Task<AiContextDto> LoadProjectContextAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var user = BuildUserContext();
        var page = new AiPageContextDto { ProjectId = projectId, Module = "projects" };

        var project = await _context.Projects
            .AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                Status = x.Status.ToString(),
                x.CustomerId,
                CustomerName = x.Customer != null
                    ? x.Customer.FirstName + " " + x.Customer.LastName
                    : null,
                Tasks = x.Tasks.Select(t => new
                {
                    t.Id,
                    t.Title,
                    Status = t.Status.ToString(),
                    t.Priority,
                    t.DueDate
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return new AiContextDto { User = user, Page = page };
        }

        return new AiContextDto
        {
            User = user,
            Page = new AiPageContextDto
            {
                ProjectId = projectId,
                CustomerId = project.CustomerId,
                Module = "projects"
            },
            Customer = project.CustomerId is not null
                ? new { project.CustomerId, project.CustomerName }
                : null,
            Projects = [project]
        };
    }

    private async Task<AiContextDto> LoadOverdueInvoicesAsync(CancellationToken cancellationToken)
    {
        var user = BuildUserContext();
        var now = DateTime.UtcNow;

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.OutstandingAmount > 0
                && x.DueDate < now
                && x.Status != "Paid"
                && x.Status != "Cancelled")
            .OrderBy(x => x.DueDate)
            .Take(50)
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                x.DueDate,
                x.Status,
                x.GrandTotal,
                x.OutstandingAmount,
                DaysOverdue = (int)(now - x.DueDate).TotalDays
            })
            .ToListAsync(cancellationToken);

        return new AiContextDto
        {
            User = user,
            Page = new AiPageContextDto { Module = "invoices", Url = "/invoices" },
            Invoices = invoices.Cast<object>().ToList(),
            Analytics = new
            {
                TotalOverdue = invoices.Count,
                TotalOutstanding = invoices.Sum(x => x.OutstandingAmount)
            }
        };
    }

    private async Task<AiContextDto> LoadRevenueRankingAsync(CancellationToken cancellationToken)
    {
        var user = BuildUserContext();

        var ranking = await _context.Orders
            .AsNoTracking()
            .GroupBy(x => new { x.CustomerId, x.Customer.FirstName, x.Customer.LastName })
            .Select(g => new
            {
                g.Key.CustomerId,
                CustomerName = g.Key.FirstName + " " + g.Key.LastName,
                OrderCount = g.Count(),
                TotalRevenue = g.Sum(x => x.GrandTotal),
                LastOrderDate = g.Max(x => x.OrderDate)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(15)
            .ToListAsync(cancellationToken);

        return new AiContextDto
        {
            User = user,
            Page = new AiPageContextDto { Module = "analytics", Url = "/analytics" },
            Analytics = new
            {
                TopCustomersByRevenue = ranking,
                TotalCustomers = ranking.Count
            }
        };
    }

    private async Task<AiContextDto> LoadProjectProgressAsync(CancellationToken cancellationToken)
    {
        var user = BuildUserContext();
        var now = DateTime.UtcNow;

        var orders = await _context.Orders
            .AsNoTracking()
            .OrderByDescending(x => x.OrderDate)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                x.Status,
                x.GrandTotal,
                x.OrderDate,
                TaskCount = x.OrderItems.Count
            })
            .ToListAsync(cancellationToken);

        var delayedTasks = await _context.WorkTasks
            .AsNoTracking()
            .Where(t => t.DueDate != null && t.DueDate < now && t.Status != Domain.Enums.WorkTaskStatus.Done)
            .OrderBy(t => t.DueDate)
            .Take(20)
            .Select(t => new
            {
                t.Id,
                t.Title,
                Status = t.Status.ToString(),
                t.DueDate,
                ProjectName = t.Project.Name
            })
            .ToListAsync(cancellationToken);

        return new AiContextDto
        {
            User = user,
            Page = new AiPageContextDto { Module = "orders", Url = "/orders" },
            Orders = orders.Cast<object>().ToList(),
            Projects = delayedTasks.Cast<object>().ToList(),
            Analytics = new
            {
                ActiveProjects = orders.Count(x => x.Status != "Completed" && x.Status != "Cancelled"),
                DelayedTasks = delayedTasks.Count
            }
        };
    }

    private async Task<AiContextDto> LoadPageDefaultsAsync(
        AiPageContextDto page,
        CancellationToken cancellationToken)
    {
        if (page.CustomerId is not null)
            return await LoadCustomerBundleAsync(page.CustomerId.Value, cancellationToken);

        if (page.OrderId is not null)
            return await LoadOrderContextAsync(page.OrderId.Value, cancellationToken);

        if (page.InvoiceId is not null)
            return await LoadInvoiceContextAsync(page.InvoiceId.Value, cancellationToken);

        if (page.ProjectId is not null)
            return await LoadProjectContextAsync(page.ProjectId.Value, cancellationToken);

        var module = page.Module;
        var user = BuildUserContext();

        if (module is "customers")
        {
            var customers = await _context.Customers
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(10)
                .Select(x => new { x.Id, Name = x.FirstName + " " + x.LastName, x.Email, x.IsActive })
                .ToListAsync(cancellationToken);

            return new AiContextDto
            {
                User = user,
                Page = page,
                Analytics = new { RecentCustomers = customers }
            };
        }

        if (module is "invoices")
        {
            return await LoadOverdueInvoicesAsync(cancellationToken);
        }

        if (module is "orders")
        {
            return await LoadProjectProgressAsync(cancellationToken);
        }

        return new AiContextDto { User = user, Page = page };
    }

    private static string ResolveModule(string url)
    {
        if (url.Contains("customer")) return "customers";
        if (url.Contains("order")) return "orders";
        if (url.Contains("invoice")) return "invoices";
        if (url.Contains("project")) return "projects";
        if (url.Contains("expense")) return "expenses";
        if (url.Contains("analytics")) return "analytics";
        if (url.Contains("report")) return "reports";
        if (url.Contains("dashboard")) return "dashboard";
        if (url.Contains("setting")) return "settings";
        return "general";
    }

    private static Guid? ExtractGuidFromUrl(string url, string segment)
    {
        var marker = $"/{segment}/";
        var idx = url.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;

        var start = idx + marker.Length;
        var end = url.IndexOf('/', start);
        var raw = end < 0 ? url[start..] : url[start..end];
        if (raw.Equals("new", StringComparison.OrdinalIgnoreCase)) return null;

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
