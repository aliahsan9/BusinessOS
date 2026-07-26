using BusinessOS.Application.Features.VectorSearch.Services;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class VectorEntityProjectorRegistry : IVectorEntityProjectorRegistry
{
    private readonly IReadOnlyList<IVectorEntityProjector> _projectors;
    private readonly IReadOnlyDictionary<Type, IVectorEntityProjector> _byClr;
    private readonly IReadOnlyDictionary<string, IVectorEntityProjector> _byName;

    public VectorEntityProjectorRegistry(IEnumerable<IVectorEntityProjector> projectors)
    {
        _projectors = projectors.ToList();
        _byClr = _projectors.ToDictionary(p => p.ClrType);
        _byName = _projectors.ToDictionary(p => p.EntityType, StringComparer.OrdinalIgnoreCase);
        TrackedClrTypes = _byClr.Keys.ToHashSet();
    }

    public IReadOnlyList<IVectorEntityProjector> All => _projectors;
    public IReadOnlySet<Type> TrackedClrTypes { get; }

    public IVectorEntityProjector? Resolve(Type clrType)
        => _byClr.TryGetValue(clrType, out var projector) ? projector : null;

    public IVectorEntityProjector? Resolve(string entityType)
        => _byName.TryGetValue(entityType, out var projector) ? projector : null;
}
