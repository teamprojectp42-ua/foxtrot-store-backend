using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace foxtrot_store_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueProductSpecificationName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecifications_ProductId_Name",
                table: "ProductSpecifications",
                columns: new[] { "ProductId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductSpecifications_ProductId_Name",
                table: "ProductSpecifications");
        }
    }
}
