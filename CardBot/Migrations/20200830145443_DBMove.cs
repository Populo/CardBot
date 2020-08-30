using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CardBot.Migrations
{
    public partial class DBMove : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GivenTime",
                table: "CardGivings");

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeStamp",
                table: "CardGivings",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeStamp",
                table: "CardGivings");

            migrationBuilder.AddColumn<DateTime>(
                name: "GivenTime",
                table: "CardGivings",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
