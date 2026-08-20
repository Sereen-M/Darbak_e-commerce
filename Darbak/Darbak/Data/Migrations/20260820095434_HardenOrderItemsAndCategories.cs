using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Darbak.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenOrderItemsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==========================================
            // PROTECT ORDER ITEMS FROM PRODUCT DELETION
            // ==========================================

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            // ==========================================
            // PRODUCT NAME SNAPSHOT
            // ==========================================
            // Add as nullable first so existing rows
            // can be populated safely.

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "OrderItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            // Copy the current product name into all
            // existing historical OrderItems.

            migrationBuilder.Sql(
                """
                UPDATE oi
                SET oi.ProductName = p.Name
                FROM OrderItems AS oi
                INNER JOIN Products AS p
                    ON oi.ProductId = p.Id;
                """);

            // After the old rows have been populated,
            // make ProductName required.

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                table: "OrderItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            // ==========================================
            // UNIQUE CATEGORY NAME
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            // ==========================================
            // RESTORE PRODUCT FK AS RESTRICT
            // ==========================================

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "OrderItems");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}