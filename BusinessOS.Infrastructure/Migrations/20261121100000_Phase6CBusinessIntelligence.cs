using System;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

[DbContext(typeof(BusinessOSDbContext))]
[Migration("20261121100000_Phase6CBusinessIntelligence")]
public partial class Phase6CBusinessIntelligence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PredictionAccuracyLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MetricKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Method = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                PredictedValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                ActualValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                AbsolutePercentError = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                Confidence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                TargetPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                TargetPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                AssumptionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PredictionAccuracyLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PredictionAccuracyLogs_TenantId_MetricKey_TargetPeriodStart",
            table: "PredictionAccuracyLogs",
            columns: new[] { "TenantId", "MetricKey", "TargetPeriodStart" });

        migrationBuilder.CreateIndex(
            name: "IX_PredictionAccuracyLogs_TenantId_Status_TargetPeriodEnd",
            table: "PredictionAccuracyLogs",
            columns: new[] { "TenantId", "Status", "TargetPeriodEnd" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PredictionAccuracyLogs");
    }
}
