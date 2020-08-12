using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CardBot.Migrations
{
    public partial class DateandServerId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GivenTime",
                table: "CardGivings",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<ulong>(
                name: "ServerId",
                table: "CardGivings",
                nullable: false,
                defaultValue: 0ul);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GivenTime",
                table: "CardGivings");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "CardGivings");
        }
    }
}
