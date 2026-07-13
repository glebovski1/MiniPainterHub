using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPainterHub.Server.Data;
using System;

#nullable disable

namespace MiniPainterHub.Server.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260712180000_AddSupportCenter")]
public partial class AddSupportCenter : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SupportTickets",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RequesterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastStaffReplyUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RequesterReadUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ResolvedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportTickets", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportTickets_AspNetUsers_RequesterUserId",
                    column: x => x.RequesterUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SupportTicketMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TicketId = table.Column<int>(type: "int", nullable: false),
                AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                SentUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsStaffReply = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportTicketMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportTicketMessages_AspNetUsers_AuthorUserId",
                    column: x => x.AuthorUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SupportTicketMessages_SupportTickets_TicketId",
                    column: x => x.TicketId,
                    principalTable: "SupportTickets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTicketMessages_AuthorUserId",
            table: "SupportTicketMessages",
            column: "AuthorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportTicketMessages_TicketId_SentUtc_Id",
            table: "SupportTicketMessages",
            columns: new[] { "TicketId", "SentUtc", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_Category_Status_UpdatedUtc",
            table: "SupportTickets",
            columns: new[] { "Category", "Status", "UpdatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_RequesterUserId_UpdatedUtc",
            table: "SupportTickets",
            columns: new[] { "RequesterUserId", "UpdatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_Status_UpdatedUtc",
            table: "SupportTickets",
            columns: new[] { "Status", "UpdatedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SupportTicketMessages");
        migrationBuilder.DropTable(name: "SupportTickets");
    }
}
