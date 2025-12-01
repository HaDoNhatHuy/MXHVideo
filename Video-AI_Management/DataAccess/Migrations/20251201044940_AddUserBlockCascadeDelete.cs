using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBlockCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserBlock_TargetId",
                table: "UserBlock",
                column: "TargetId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserBlock_Video_TargetId",
                table: "UserBlock",
                column: "TargetId",
                principalTable: "Video",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserBlock_Video_TargetId",
                table: "UserBlock");

            migrationBuilder.DropIndex(
                name: "IX_UserBlock_TargetId",
                table: "UserBlock");
        }
    }
}
