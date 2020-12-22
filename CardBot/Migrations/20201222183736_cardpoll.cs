using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CardBot.Migrations
{
    public partial class cardpoll : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FailedCardId",
                table: "Cards",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailedId",
                table: "Cards",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "Poll",
                table: "Cards",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_FailedCardId",
                table: "Cards",
                column: "FailedCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Cards_FailedCardId",
                table: "Cards",
                column: "FailedCardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Cards_FailedCardId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_FailedCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "FailedCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "FailedId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Poll",
                table: "Cards");
        }
    }
}
