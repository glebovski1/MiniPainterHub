using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPainterHub.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddViewerMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "PostImages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "PostImages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommentImageMarks",
                columns: table => new
                {
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    PostImageId = table.Column<int>(type: "int", nullable: false),
                    NormalizedX = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    NormalizedY = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentImageMarks", x => x.CommentId);
                    table.ForeignKey(
                        name: "FK_CommentImageMarks_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommentImageMarks_PostImages_PostImageId",
                        column: x => x.PostImageId,
                        principalTable: "PostImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageAuthorMarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostImageId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NormalizedX = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    NormalizedY = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageAuthorMarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageAuthorMarks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImageAuthorMarks_PostImages_PostImageId",
                        column: x => x.PostImageId,
                        principalTable: "PostImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommentImageMarks_PostImageId",
                table: "CommentImageMarks",
                column: "PostImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageAuthorMarks_CreatedByUserId",
                table: "ImageAuthorMarks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageAuthorMarks_PostImageId",
                table: "ImageAuthorMarks",
                column: "PostImageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommentImageMarks");

            migrationBuilder.DropTable(
                name: "ImageAuthorMarks");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "PostImages");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "PostImages");
        }
    }
}
