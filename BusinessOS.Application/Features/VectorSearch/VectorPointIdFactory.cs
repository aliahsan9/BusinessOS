using System.Security.Cryptography;
using System.Text;

namespace BusinessOS.Application.Features.VectorSearch;

public static class VectorPointIdFactory
{
    public static Guid Create(Guid tenantId, string entityType, Guid entityId, int chunkIndex)
    {
        var key = $"{tenantId:N}:{entityType}:{entityId:N}:{chunkIndex}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}
