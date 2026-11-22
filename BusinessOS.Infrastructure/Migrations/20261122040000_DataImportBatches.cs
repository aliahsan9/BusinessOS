using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class DataImportBatches : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DataImportBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                TotalRows = table.Column<int>(type: "int", nullable: false),
                CreatedCount = table.Column<int>(type: "int", nullable: false),
                UpdatedCount = table.Column<int>(type: "int", nullable: false),
                SkippedCount = table.Column<int>(type: "int", nullable: false),
                ErrorCount = table.Column<int>(type: "int", nullable: false),
                ImportedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                ErrorSummaryJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataImportBatches", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DataImportBatches_TenantId_CreatedAt",
            table: "DataImportBatches",
            columns: new[] { "TenantId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_DataImportBatches_TenantId_EntityType_CreatedAt",
            table: "DataImportBatches",
            columns: new[] { "TenantId", "EntityType", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DataImportBatches");
    }
}