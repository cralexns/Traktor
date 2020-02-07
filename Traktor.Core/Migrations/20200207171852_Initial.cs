using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Traktor.Core.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.CreateTable(
            //    name: "Media",
            //    columns: table => new
            //    {
            //        DbId = table.Column<int>(nullable: false)
            //            .Annotation("Sqlite:Autoincrement", true),
            //        Id_Trakt = table.Column<int>(nullable: true),
            //        Id_Slug = table.Column<string>(nullable: true),
            //        Id_TVDB = table.Column<int>(nullable: true),
            //        Id_IMDB = table.Column<string>(nullable: true),
            //        Id_TMDB = table.Column<int>(nullable: true),
            //        Title = table.Column<string>(nullable: true),
            //        Year = table.Column<int>(nullable: false),
            //        Genres = table.Column<string>(nullable: true),
            //        ImageUrl = table.Column<string>(nullable: true),
            //        State = table.Column<int>(nullable: false),
            //        StateDate = table.Column<DateTime>(nullable: false),
            //        Release = table.Column<DateTime>(nullable: true),
            //        CollectedAt = table.Column<DateTime>(nullable: true),
            //        WatchlistedAt = table.Column<DateTime>(nullable: true),
            //        FirstSpottedAt = table.Column<DateTime>(nullable: true),
            //        LastScoutedAt = table.Column<DateTime>(nullable: true),
            //        Magnet = table.Column<string>(nullable: true),
            //        RelativePath = table.Column<string>(nullable: true),
            //        Discriminator = table.Column<string>(nullable: false),
            //        Number = table.Column<int>(nullable: true),
            //        Season = table.Column<int>(nullable: true),
            //        ShowTitle = table.Column<string>(nullable: true),
            //        TotalEpisodesInSeason = table.Column<int>(nullable: true),
            //        ShowIMDB = table.Column<string>(nullable: true),
            //        ShowSlug = table.Column<string>(nullable: true),
            //        ShowTMDB = table.Column<int>(nullable: true),
            //        ShowTVDB = table.Column<int>(nullable: true),
            //        ShowTrakt = table.Column<int>(nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_Media", x => x.DbId);
            //    });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropTable(
            //    name: "Media");
        }
    }
}
