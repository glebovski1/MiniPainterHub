using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPainterHub.Server.Migrations
{
    /// <inheritdoc />
    public partial class EventSpikeReliabilityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostTags_TagId",
                table: "PostTags");

            migrationBuilder.DropIndex(
                name: "IX_Posts_CreatedById",
                table: "Posts");

            migrationBuilder.Sql(
                "UPDATE [AspNetUsers] SET [DisplayName] = LEFT([DisplayName], 80) WHERE [DisplayName] IS NOT NULL AND LEN([DisplayName]) > 80");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_DisplayName",
                table: "Profiles",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_PostTags_TagId_PostId",
                table: "PostTags",
                columns: new[] { "TagId", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedById_IsDeleted_CreatedUtc",
                table: "Posts",
                columns: new[] { "CreatedById", "IsDeleted", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_IsDeleted_CreatedUtc",
                table: "Posts",
                columns: new[] { "IsDeleted", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DisplayName",
                table: "AspNetUsers",
                column: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Profiles_DisplayName",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_PostTags_TagId_PostId",
                table: "PostTags");

            migrationBuilder.DropIndex(
                name: "IX_Posts_CreatedById_IsDeleted_CreatedUtc",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_IsDeleted_CreatedUtc",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DisplayName",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostTags_TagId",
                table: "PostTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedById",
                table: "Posts",
                column: "CreatedById");
        }
    }
}
