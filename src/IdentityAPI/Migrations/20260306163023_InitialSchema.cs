using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitchenOrchestrator.IdentityAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MatchSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchBeginUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    MatchEndUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    LevelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FinalScore = table.Column<int>(type: "integer", nullable: false),
                    TargetScore = table.Column<int>(type: "integer", nullable: false),
                    FinalState = table.Column<string>(type: "text", nullable: false),
                    FailedOrders = table.Column<int>(type: "integer", nullable: false),
                    CompletedOrders = table.Column<int>(type: "integer", nullable: false),
                    PerfectOrders = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SteamId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccountCreatedUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    LastLoggedInUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    MatchesPlayed = table.Column<int>(type: "integer", nullable: false),
                    MatchesWon = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    PerfectOrders = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MatchHistoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndividualScore = table.Column<int>(type: "integer", nullable: false),
                    OrdersDelivered = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_MatchHistories_MatchHistoryId",
                        column: x => x.MatchHistoryId,
                        principalTable: "MatchHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_PlayerProfiles_PlayerProfileId",
                        column: x => x.PlayerProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_MatchSessionId",
                table: "MatchHistories",
                column: "MatchSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_MatchHistoryId",
                table: "MatchParticipants",
                column: "MatchHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_PlayerProfileId",
                table: "MatchParticipants",
                column: "PlayerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_SteamId",
                table: "PlayerProfiles",
                column: "SteamId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchParticipants");

            migrationBuilder.DropTable(
                name: "MatchHistories");

            migrationBuilder.DropTable(
                name: "PlayerProfiles");
        }
    }
}
