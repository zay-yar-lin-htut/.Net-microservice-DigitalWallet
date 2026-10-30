using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferEventIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ReferenceId_EntryType",
                table: "LedgerEntries",
                columns: new[] { "ReferenceId", "EntryType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_ReferenceId_EntryType",
                table: "LedgerEntries");
        }
    }
}
