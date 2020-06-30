using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CardBot.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardGivings",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    CardReason = table.Column<string>(nullable: true),
                    GiverId = table.Column<Guid>(nullable: false),
                    DegenerateId = table.Column<Guid>(nullable: false),
                    CardId = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardGivings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardGivings_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardGivings_Users_DegenerateId",
                        column: x => x.DegenerateId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardGivings_Users_GiverId",
                        column: x => x.GiverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardGivings_CardId",
                table: "CardGivings",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardGivings_DegenerateId",
                table: "CardGivings",
                column: "DegenerateId");

            migrationBuilder.CreateIndex(
                name: "IX_CardGivings_GiverId",
                table: "CardGivings",
                column: "GiverId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardGivings");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
