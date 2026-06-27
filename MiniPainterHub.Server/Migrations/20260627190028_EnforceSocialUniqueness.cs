using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPainterHub.Server.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSocialUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Likes_PostId",
                table: "Likes");

            migrationBuilder.Sql(
                @"WITH RankedLikes AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY PostId, UserId ORDER BY CreatedAt, Id) AS RowNumber
                    FROM Likes
                )
                DELETE FROM RankedLikes
                WHERE RowNumber > 1;");

            migrationBuilder.AddColumn<string>(
                name: "DirectConversationKey",
                table: "Conversations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                @"WITH DirectPairs AS
                (
                    SELECT
                        c.Id,
                        MIN(cp.UserId) AS UserA,
                        MAX(cp.UserId) AS UserB
                    FROM Conversations c
                    INNER JOIN ConversationParticipants cp ON cp.ConversationId = c.Id
                    GROUP BY c.Id
                    HAVING COUNT(*) = 2 AND COUNT(DISTINCT cp.UserId) = 2
                ),
                RankedPairs AS
                (
                    SELECT
                        Id,
                        UserA,
                        UserB,
                        ROW_NUMBER() OVER (PARTITION BY UserA, UserB ORDER BY Id) AS PairRank
                    FROM DirectPairs
                )
                UPDATE c
                SET DirectConversationKey = LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), CONCAT(r.UserA, NCHAR(31), r.UserB))), 2))
                FROM Conversations c
                INNER JOIN RankedPairs r ON r.Id = c.Id
                WHERE r.PairRank = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_PostId_UserId",
                table: "Likes",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_DirectConversationKey",
                table: "Conversations",
                column: "DirectConversationKey",
                unique: true,
                filter: "[DirectConversationKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Likes_PostId_UserId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_DirectConversationKey",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DirectConversationKey",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_PostId",
                table: "Likes",
                column: "PostId");
        }
    }
}
