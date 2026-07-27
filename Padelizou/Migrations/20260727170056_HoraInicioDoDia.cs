using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class HoraInicioDoDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraInicioDoDia",
                table: "Torneio",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 8, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoraInicioDoDia",
                table: "Torneio");
        }
    }
}
