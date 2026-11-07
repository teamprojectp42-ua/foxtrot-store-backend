using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace foxtrot_store_backend.Migrations
{
    /// <inheritdoc />
    public partial class Add_Product_Translations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductSpecifications_ProductId_Name",
                table: "ProductSpecifications");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ProductSpecifications");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "ProductSpecifications");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Products");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProductSpecifications_Id_ProductId",
                table: "ProductSpecifications",
                columns: new[] { "Id", "ProductId" });

            migrationBuilder.CreateTable(
                name: "ProductSpecificationTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CultureCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecificationTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecificationTranslations_ProductSpecifications_Speci~",
                        columns: x => new { x.SpecificationId, x.ProductId },
                        principalTable: "ProductSpecifications",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProductTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CultureCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(5000)", maxLength: 5000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductTranslations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecificationTranslations_ProductId_CultureCode_Name",
                table: "ProductSpecificationTranslations",
                columns: new[] { "ProductId", "CultureCode", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecificationTranslations_SpecificationId_CultureCode",
                table: "ProductSpecificationTranslations",
                columns: new[] { "SpecificationId", "CultureCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecificationTranslations_SpecificationId_ProductId",
                table: "ProductSpecificationTranslations",
                columns: new[] { "SpecificationId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductTranslations_ProductId_CultureCode",
                table: "ProductTranslations",
                columns: new[] { "ProductId", "CultureCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductSpecificationTranslations");

            migrationBuilder.DropTable(
                name: "ProductTranslations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProductSpecifications_Id_ProductId",
                table: "ProductSpecifications");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ProductSpecifications",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "ProductSpecifications",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Products",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Products",
                type: "longtext",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecifications_ProductId_Name",
                table: "ProductSpecifications",
                columns: new[] { "ProductId", "Name" },
                unique: true);
        }
    }
}
