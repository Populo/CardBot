using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CardBot.Migrations
{
    public partial class cardpoll : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Poll",
                table: "Cards");
        }
    }
}
