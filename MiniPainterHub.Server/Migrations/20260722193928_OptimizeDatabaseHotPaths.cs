using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPainterHub.Server.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeDatabaseHotPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_CreatedById_IsDeleted_CreatedUtc",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_IsDeleted_CreatedUtc",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_ConversationId",
                table: "DirectMessages");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedById_IsDeleted_CreatedUtc_Id",
                table: "Posts",
                columns: new[] { "CreatedById", "IsDeleted", "CreatedUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_IsDeleted_CreatedUtc_Id",
                table: "Posts",
                columns: new[] { "IsDeleted", "CreatedUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId_Id",
                table: "DirectMessages",
                columns: new[] { "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId_SentUtc_Id",
                table: "DirectMessages",
                columns: new[] { "ConversationId", "SentUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId_IsDeleted_CreatedUtc_Id",
                table: "Comments",
                columns: new[] { "PostId", "IsDeleted", "CreatedUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_CreatedById_IsDeleted_CreatedUtc_Id",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_IsDeleted_CreatedUtc_Id",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_ConversationId_Id",
                table: "DirectMessages");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_ConversationId_SentUtc_Id",
                table: "DirectMessages");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId_IsDeleted_CreatedUtc_Id",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedById_IsDeleted_CreatedUtc",
                table: "Posts",
                columns: new[] { "CreatedById", "IsDeleted", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_IsDeleted_CreatedUtc",
                table: "Posts",
                columns: new[] { "IsDeleted", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId",
                table: "DirectMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId",
                table: "Comments",
                column: "PostId");
        }
    }
}
