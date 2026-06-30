using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class AiConversationSessionConfiguration : IEntityTypeConfiguration<AiConversationSession>
{
    public void Configure(EntityTypeBuilder<AiConversationSession> builder)
    {
        builder.ToTable("AiConversationSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CurrentPage).HasMaxLength(500);
        builder.Property(x => x.MemoryJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.LastActivityAt });
    }
}

public class AiDocumentConfiguration : IEntityTypeConfiguration<AiDocument>
{
    public void Configure(EntityTypeBuilder<AiDocument> builder)
    {
        builder.ToTable("AiDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Tags).HasMaxLength(500);
        builder.Property(x => x.SourceEntityType).HasMaxLength(100);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.DocumentType });
    }
}

public class AiDocumentChunkConfiguration : IEntityTypeConfiguration<AiDocumentChunk>
{
    public void Configure(EntityTypeBuilder<AiDocumentChunk> builder)
    {
        builder.ToTable("AiDocumentChunks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.EmbeddingJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Keywords).HasMaxLength(2000);
        builder.Property(x => x.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Tags).HasMaxLength(500);
        builder.HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.DocumentType });
    }
}

public class AiCopilotAuditLogConfiguration : IEntityTypeConfiguration<AiCopilotAuditLog>
{
    public void Configure(EntityTypeBuilder<AiCopilotAuditLog> builder)
    {
        builder.ToTable("AiCopilotAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Intent).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UserMessage).HasMaxLength(2000);
        builder.Property(x => x.ToolsUsedJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RetrievedDocumentsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
    }
}

public class AIConversationConfiguration : IEntityTypeConfiguration<AIConversation>
{
    public void Configure(EntityTypeBuilder<AIConversation> builder)
    {
        builder.ToTable("AIConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Prompt).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Response).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.Intent).HasMaxLength(100);
        builder.Property(x => x.ToolsUsedJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CitationsJson).HasColumnType("nvarchar(max)");
        builder.HasOne(x => x.Session).WithMany(x => x.Messages).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
    }
}
