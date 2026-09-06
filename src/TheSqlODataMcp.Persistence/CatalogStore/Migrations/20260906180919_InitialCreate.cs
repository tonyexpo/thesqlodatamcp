using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheSqlODataMcp.Persistence.CatalogStore.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TechnicalHash = table.Column<string>(type: "TEXT", nullable: false),
                    TechnicalCatalogJson = table.Column<string>(type: "TEXT", nullable: false),
                    MergedHash = table.Column<string>(type: "TEXT", nullable: true),
                    MergedCatalogJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationResultJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRevisions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogRevisions");
        }
    }
}
