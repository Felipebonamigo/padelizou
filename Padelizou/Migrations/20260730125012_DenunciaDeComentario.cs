using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class DenunciaDeComentario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DenunciadoEm",
                table: "ComentariosPerfil",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DenunciadoPorId",
                table: "ComentariosPerfil",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DenunciadoEm",
                table: "ComentariosPerfil");

            migrationBuilder.DropColumn(
                name: "DenunciadoPorId",
                table: "ComentariosPerfil");
        }
    }
}
