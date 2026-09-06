using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheSqlODataMcp.Persistence.CatalogStore.Migrations
{
    /// <inheritdoc />
    public partial class AddActivatedAtToCatalogRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAt",
                table: "CatalogRevisions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "CatalogRevisions");
        }
    }
}
