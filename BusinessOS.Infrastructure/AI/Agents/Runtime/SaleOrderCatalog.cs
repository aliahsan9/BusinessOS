using BusinessOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public enum SaleMatchKind
{
    NotSpecified,
    Unique,
    Ambiguous,
    NotFound,
    Inactive
}

public sealed class SaleCustomerHit
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public bool IsActive { get; init; }
}

public sealed class SaleProductHit
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Sku { get; init; } = "";
    public string? Description { get; init; }
    public string? Category { get; init; }
    public decimal SalePrice { get; init; }
    public decimal Stock { get; init; }
    public bool HasInventory { get; init; }
    public bool IsActive { get; init; }
}

public sealed class SaleMatch<T>
{
    public SaleMatchKind Kind { get; init; }
    public T? Value { get; init; }
    public IReadOnlyList<T> Options { get; init; } = [];

    public static SaleMatch<T> SpecifiedNone() => new() { Kind = SaleMatchKind.NotSpecified };
}

public interface ISaleOrderCatalog
{
    Task<SaleMatch<SaleCustomerHit>> ResolveCustomerAsync(
        string? customerId,
        string? customerName,
        CancellationToken cancellationToken = default);

    Task<SaleMatch<SaleProductHit>> ResolveProductAsync(
        string? productId,
        string? productName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves sale customers and products inside the current tenant filter.
/// A name never falls through to an unrelated record.
/// </summary>
public sealed class SaleOrderCatalog : ISaleOrderCatalog
{
    private readonly IApplicationDbContext _db;

    public SaleOrderCatalog(IApplicationDbContext db) => _db = db;

    public async Task<SaleMatch<SaleCustomerHit>> ResolveCustomerAsync(
        string? customerId,
        string? customerName,
        CancellationToken cancellationToken = default)
    {
        var named = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        SaleCustomerHit? byId = null;
        if (Guid.TryParse(customerId, out var id))
        {
            byId = await _db.Customers.AsNoTracking()
                .Where(customer => customer.Id == id)
                .Select(customer => new SaleCustomerHit
                {
                    Id = customer.Id,
                    Name = (customer.FirstName + " " + customer.LastName).Trim(),
                    IsActive = customer.IsActive
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (byId is not null && (named is null || NamesMatch(byId.Name, named)))
                return byId.IsActive
                    ? Unique(byId)
                    : new SaleMatch<SaleCustomerHit> { Kind = SaleMatchKind.Inactive, Value = byId };
        }

        if (named is null)
            return byId is null ? SaleMatch<SaleCustomerHit>.SpecifiedNone() : NotFound<SaleCustomerHit>();

        var people = await _db.Customers.AsNoTracking()
            .Select(customer => new SaleCustomerHit
            {
                Id = customer.Id,
                Name = (customer.FirstName + " " + customer.LastName).Trim(),
                IsActive = customer.IsActive
            })
            .ToListAsync(cancellationToken);

        var exact = people.Where(person => NamesMatch(person.Name, named)).ToList();
        var pool = exact.Count > 0 ? exact : people.Where(person => TokensMatch(person.Name, named)).ToList();
        return Classify(pool);
    }

    public async Task<SaleMatch<SaleProductHit>> ResolveProductAsync(
        string? productId,
        string? productName,
        CancellationToken cancellationToken = default)
    {
        var named = string.IsNullOrWhiteSpace(productName) ? null : productName.Trim();
        if (Guid.TryParse(productId, out var id))
        {
            var byId = await LoadProductsAsync(cancellationToken, id);
            var hit = byId.FirstOrDefault();
            if (hit is not null && (named is null || ProductMatches(hit, named)))
            {
                return hit.IsActive
                    ? Unique(hit)
                    : new SaleMatch<SaleProductHit> { Kind = SaleMatchKind.Inactive, Value = hit };
            }
        }

        if (named is null)
            return SaleMatch<SaleProductHit>.SpecifiedNone();

        var products = await LoadProductsAsync(cancellationToken, productId: null);
        var active = products.Where(product => product.IsActive).ToList();
        var sku = active
            .Where(product => product.Sku.Equals(named, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (sku.Count == 1)
            return Unique(sku[0]);
        if (sku.Count > 1)
            return Ambiguous(sku);

        var tokens = SaleOrderLanguage.CatalogTokens(named);
        if (tokens.Count == 0)
            return NotFound<SaleProductHit>();

        var full = active.Where(product => tokens.All(token => ProductContains(product, token))).ToList();
        if (full.Count == 1)
            return Unique(full[0]);
        if (full.Count > 1)
            return Ambiguous(full);

        var numbers = tokens.Where(token => token.All(char.IsDigit)).ToList();
        var longest = tokens
            .Where(token => token.Any(char.IsLetter))
            .OrderByDescending(token => token.Length)
            .FirstOrDefault();
        var relaxed = active.Where(product =>
            numbers.All(token => ProductContains(product, token))
            && (longest is null || ProductContains(product, longest))).ToList();

        if (relaxed.Count == 1)
            return Unique(relaxed[0]);
        if (relaxed.Count > 1)
            return Ambiguous(relaxed);
        return NotFound<SaleProductHit>();
    }

    private async Task<List<SaleProductHit>> LoadProductsAsync(CancellationToken cancellationToken, Guid? productId)
    {
        var query = _db.Products.AsNoTracking().Where(product => productId == null || product.Id == productId);
        var rows = await query
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.SKU,
                product.Description,
                Category = product.Category.Name,
                product.SalePrice,
                product.IsActive
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(row => row.Id).ToList();
        var stock = await _db.Inventories.AsNoTracking()
            .Where(item => ids.Contains(item.ProductId))
            .Select(item => new { item.ProductId, item.CurrentStock })
            .ToListAsync(cancellationToken);
        var stockByProduct = stock.ToDictionary(item => item.ProductId, item => item.CurrentStock);

        return rows.Select(row => new SaleProductHit
        {
            Id = row.Id,
            Name = row.Name,
            Sku = row.SKU,
            SalePrice = row.SalePrice,
            IsActive = row.IsActive,
            HasInventory = stockByProduct.ContainsKey(row.Id),
            Stock = stockByProduct.TryGetValue(row.Id, out var quantity) ? quantity : 0,
            Description = row.Description,
            Category = row.Category
        }).ToList();
    }

    private static bool ProductMatches(SaleProductHit product, string query)
    {
        var tokens = SaleOrderLanguage.CatalogTokens(query);
        return tokens.Count > 0 && tokens.All(token => ProductContains(product, token));
    }

    private static bool ProductContains(SaleProductHit product, string token)
    {
        var haystack = SaleOrderLanguage.NormalizeCatalogText(
            $"{product.Name} {product.Sku} {product.Description} {product.Category}");
        return SaleOrderLanguage.ContainsToken(haystack, token);
    }

    private static bool NamesMatch(string fullName, string query) =>
        fullName.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TokensMatch(string fullName, string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return false;
        var haystack = SaleOrderLanguage.NormalizeCatalogText(fullName);
        return tokens.All(token => SaleOrderLanguage.ContainsToken(haystack, token.ToLowerInvariant()));
    }

    private static SaleMatch<SaleCustomerHit> Classify(List<SaleCustomerHit> pool)
    {
        if (pool.Count == 0)
            return NotFound<SaleCustomerHit>();

        var active = pool.Where(person => person.IsActive).ToList();
        if (active.Count == 1)
            return Unique(active[0]);
        if (active.Count > 1)
            return Ambiguous(active);
        return new SaleMatch<SaleCustomerHit>
        {
            Kind = SaleMatchKind.Inactive,
            Value = pool[0],
            Options = pool
        };
    }

    private static SaleMatch<T> Unique<T>(T value) =>
        new() { Kind = SaleMatchKind.Unique, Value = value, Options = [value] };

    private static SaleMatch<T> Ambiguous<T>(IReadOnlyList<T> options) =>
        new() { Kind = SaleMatchKind.Ambiguous, Options = options.Take(5).ToList() };

    private static SaleMatch<T> NotFound<T>() => new() { Kind = SaleMatchKind.NotFound };
}
