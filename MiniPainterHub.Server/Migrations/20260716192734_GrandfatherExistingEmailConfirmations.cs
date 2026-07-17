using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPainterHub.Server.Migrations
{
    /// <inheritdoc />
    public partial class GrandfatherExistingEmailConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [AspNetUsers] SET [EmailConfirmed] = CAST(1 AS bit) WHERE [EmailConfirmed] = CAST(0 AS bit);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Confirmation is security-sensitive user data and cannot be safely inferred in reverse.
        }
    }
}
