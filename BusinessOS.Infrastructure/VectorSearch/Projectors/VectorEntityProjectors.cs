using BusinessOS.Application.Features.VectorSearch;
using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Infrastructure.VectorSearch.Projectors;

public sealed class DocumentVectorProjector : IVectorEntityProjector
{
    public string EntityType => VectorEntityTypes.Document;
    public Type ClrType => typeof(AiDocument);
    public bool CanHandle(Type clrType) => clrType == ClrType;

    public IReadOnlyList<VectorPointDocument> BuildDocuments(object entity)
    {
        if (entity is not AiDocument doc)
            return [];

        var chunks = VectorTextChunker.Chunk(doc.Content);
        if (chunks.Count == 0)
            chunks = [doc.Title];

        return chunks.Select((chunk, index) => new VectorPointDocument
        {
            PointId = VectorPointIdFactory.Create(doc.TenantId, EntityType, doc.Id, index),
            TenantId = doc.TenantId,
            EntityType = EntityType,
            EntityId = doc.Id,
            ChunkIndex = index,
            Title = doc.Title,
            Text = $"{doc.Title}\n{chunk}",
            Excerpt = VectorTextChunker.Truncate(chunk, 200),
            Payload = new Dictionary<string, object?>
            {
                ["documentType"] = doc.DocumentType,
                ["tags"] = doc.Tags,
                ["sourceEntityType"] = doc.SourceEntityType,
                ["sourceEntityId"] = doc.SourceEntityId?.ToString()
            }
        }).ToList();
    }
}

public sealed class ProductVectorProjector : IVectorEntityProjector
{
    public string EntityType => VectorEntityTypes.Product;
    public Type ClrType => typeof(Product);
    public bool CanHandle(Type clrType) => clrType == ClrType;

    public IReadOnlyList<VectorPointDocument> BuildDocuments(object entity)
    {
        if (entity is not Product product)
            return [];

        var categoryName = product.Category?.Name;
        var text = string.Join('\n', new[]
        {
            $"Product: {product.Name}",
            $"SKU: {product.SKU}",
            string.IsNullOrWhiteSpace(categoryName) ? null : $"Category: {categoryName}",
            product.Description
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return
        [
            new VectorPointDocument
            {
                PointId = VectorPointIdFactory.Create(product.TenantId, EntityType, product.Id, 0),
                TenantId = product.TenantId,
                EntityType = EntityType,
                EntityId = product.Id,
                ChunkIndex = 0,
                Title = product.Name,
                Text = text,
                Excerpt = VectorTextChunker.Truncate(text, 200),
                Payload = new Dictionary<string, object?>
                {
                    ["sku"] = product.SKU,
                    ["category"] = categoryName,
                    ["isActive"] = product.IsActive.ToString()
                }
            }
        ];
    }
}

public sealed class ProjectVectorProjector : IVectorEntityProjector
{
    public string EntityType => VectorEntityTypes.Project;
    public Type ClrType => typeof(Project);
    public bool CanHandle(Type clrType) => clrType == ClrType;

    public IReadOnlyList<VectorPointDocument> BuildDocuments(object entity)
    {
        if (entity is not Project project)
            return [];

        var text = string.Join('\n', new[]
        {
            $"Project: {project.Name}",
            $"Status: {project.Status}",
            string.IsNullOrWhiteSpace(project.Tags) ? null : $"Tags: {project.Tags}",
            project.Description
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return
        [
            new VectorPointDocument
            {
                PointId = VectorPointIdFactory.Create(project.TenantId, EntityType, project.Id, 0),
                TenantId = project.TenantId,
                EntityType = EntityType,
                EntityId = project.Id,
                ChunkIndex = 0,
                Title = project.Name,
                Text = text,
                Excerpt = VectorTextChunker.Truncate(text, 200),
                Payload = new Dictionary<string, object?>
                {
                    ["status"] = project.Status.ToString(),
                    ["tags"] = project.Tags
                }
            }
        ];
    }
}

public sealed class CustomerVectorProjector : IVectorEntityProjector
{
    public string EntityType => VectorEntityTypes.Customer;
    public Type ClrType => typeof(Customer);
    public bool CanHandle(Type clrType) => clrType == ClrType;

    public IReadOnlyList<VectorPointDocument> BuildDocuments(object entity)
    {
        if (entity is not Customer customer)
            return [];

        var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
        var address = string.Join(", ", new[]
        {
            customer.Address,
            customer.City,
            customer.Country,
            customer.PostalCode
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var text = string.Join('\n', new[]
        {
            $"Customer: {fullName}",
            string.IsNullOrWhiteSpace(customer.Company) ? null : $"Company: {customer.Company}",
            $"Email: {customer.Email}",
            $"Phone: {customer.PhoneNumber}",
            string.IsNullOrWhiteSpace(address) ? null : $"Address: {address}",
            customer.Notes
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return
        [
            new VectorPointDocument
            {
                PointId = VectorPointIdFactory.Create(customer.TenantId, EntityType, customer.Id, 0),
                TenantId = customer.TenantId,
                EntityType = EntityType,
                EntityId = customer.Id,
                ChunkIndex = 0,
                Title = fullName,
                Text = text,
                Excerpt = VectorTextChunker.Truncate(text, 200),
                Payload = new Dictionary<string, object?>
                {
                    ["company"] = customer.Company,
                    ["city"] = customer.City,
                    ["country"] = customer.Country,
                    ["email"] = customer.Email,
                    ["isActive"] = customer.IsActive.ToString()
                }
            }
        ];
    }
}
