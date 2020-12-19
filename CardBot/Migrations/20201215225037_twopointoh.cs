using Microsoft.EntityFrameworkCore.Migrations;

namespace CardBot.Migrations
{
    public partial class twopointoh : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Emoji",
                table: "Cards",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "ServerId",
                table: "Cards",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<int>(
                name: "Value",
                table: "Cards",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Emoji",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Cards");
        }
    }
}
