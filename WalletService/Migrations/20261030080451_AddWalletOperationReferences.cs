using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletService.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletOperationReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "WalletOperations",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperations_ReferenceId",
                table: "WalletOperations",
                column: "ReferenceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletOperations_ReferenceId",
                table: "WalletOperations");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "WalletOperations");
        }
    }
}
