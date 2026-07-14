using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPainterHub.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT [NormalizedEmail]
                    FROM [AspNetUsers]
                    WHERE [NormalizedEmail] IS NOT NULL
                    GROUP BY [NormalizedEmail]
                    HAVING COUNT_BIG(*) > 1
                )
                THROW 51000, 'AddGoogleAuthentication cannot create a unique email index because duplicate normalized emails exist. Resolve duplicates manually before applying this migration.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "ExternalAuthExchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandleHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderSubject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VerifiedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SuggestedDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReturnUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAuthExchanges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuthExchanges_ExpiresUtc",
                table: "ExternalAuthExchanges",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuthExchanges_HandleHash",
                table: "ExternalAuthExchanges",
                column: "HandleHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalAuthExchanges");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");
        }
    }
}
