using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Traktor.Core.Migrations
{
    public partial class SchemaUpdateWatchedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WatchedAt",
                table: "Media",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WatchedAt",
                table: "Media");
        }
    }
}
