using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentBatchProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchIdempotencyRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatchIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchIdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchIdempotencyRecords_PaymentBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "PaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchIdempotencyRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchIdempotencyRecords_BatchId",
                table: "BatchIdempotencyRecords",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchIdempotencyRecords_TenantId_IdempotencyKey",
                table: "BatchIdempotencyRecords",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchIdempotencyRecords");
        }
    }
}
