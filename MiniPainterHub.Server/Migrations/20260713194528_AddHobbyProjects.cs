using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPainterHub.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddHobbyProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HobbyProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    GameSystem = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FactionTheme = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Goal = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CoverPostId = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    ModeratedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModeratedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ModerationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HobbyProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HobbyProjects_AspNetUsers_ModeratedByUserId",
                        column: x => x.ModeratedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HobbyProjects_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HobbyProjects_Posts_CoverPostId",
                        column: x => x.CoverPostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HobbyProjectEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    LinkedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MilestoneLabel = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ShowcaseOrder = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HobbyProjectEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HobbyProjectEntries_HobbyProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "HobbyProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HobbyProjectEntries_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjectEntries_PostId",
                table: "HobbyProjectEntries",
                column: "PostId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjectEntries_ProjectId_LinkedUtc_PostId",
                table: "HobbyProjectEntries",
                columns: new[] { "ProjectId", "LinkedUtc", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjectEntries_ProjectId_PostId",
                table: "HobbyProjectEntries",
                columns: new[] { "ProjectId", "PostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjectEntries_ProjectId_ShowcaseOrder",
                table: "HobbyProjectEntries",
                columns: new[] { "ProjectId", "ShowcaseOrder" },
                unique: true,
                filter: "[ShowcaseOrder] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjects_CoverPostId",
                table: "HobbyProjects",
                column: "CoverPostId");

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjects_IsHidden_ArchivedUtc_UpdatedUtc",
                table: "HobbyProjects",
                columns: new[] { "IsHidden", "ArchivedUtc", "UpdatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjects_Kind_IsHidden_ArchivedUtc_UpdatedUtc",
                table: "HobbyProjects",
                columns: new[] { "Kind", "IsHidden", "ArchivedUtc", "UpdatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjects_ModeratedByUserId",
                table: "HobbyProjects",
                column: "ModeratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjects_OwnerUserId_ArchivedUtc_UpdatedUtc",
                table: "HobbyProjects",
                columns: new[] { "OwnerUserId", "ArchivedUtc", "UpdatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HobbyProjects_Status_IsHidden_ArchivedUtc_UpdatedUtc",
                table: "HobbyProjects",
                columns: new[] { "Status", "IsHidden", "ArchivedUtc", "UpdatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HobbyProjectEntries");

            migrationBuilder.DropTable(
                name: "HobbyProjects");
        }
    }
}
