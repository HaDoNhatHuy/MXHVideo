using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class addTableCelebrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Celebrity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Age = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Job = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Celebrity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecognizeCelebrities",
                columns: table => new
                {
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CelebrityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecognizeCelebrities", x => new { x.CelebrityId, x.VideoId });
                    table.ForeignKey(
                        name: "FK_RecognizeCelebrities_Celebrity_CelebrityId",
                        column: x => x.CelebrityId,
                        principalTable: "Celebrity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecognizeCelebrities_Video_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Video",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecognizeCelebrities_VideoId",
                table: "RecognizeCelebrities",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecognizeCelebrities");

            migrationBuilder.DropTable(
                name: "Celebrity");
        }
    }
}
