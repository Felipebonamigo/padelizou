using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        // Baseline vazia de propósito: todas as tabelas do modelo atual já existem no banco
        // (o schema foi criado manualmente antes de adotarmos EF Core Migrations). Esta
        // migration só existe para registrar um ponto de partida no __EFMigrationsHistory.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
