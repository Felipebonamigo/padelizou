using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeDonoLogoClubeOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dropa o FK atual de Times.ClubeId por nome DINÂMICO — o nome da constraint pode
            // divergir entre local e produção (histórico de drift), então não assumimos o nome.
            migrationBuilder.Sql(@"
DECLARE @fk sysname;
SELECT @fk = fk.name
FROM sys.foreign_keys fk
WHERE fk.parent_object_id = OBJECT_ID(N'[Times]')
  AND EXISTS (
      SELECT 1 FROM sys.foreign_key_columns fkc
      JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
      WHERE fkc.constraint_object_id = fk.object_id AND c.name = 'ClubeId');
IF @fk IS NOT NULL EXEC('ALTER TABLE [Times] DROP CONSTRAINT [' + @fk + ']');
");

            migrationBuilder.AlterColumn<int>(
                name: "ClubeId",
                table: "Times",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DonoId",
                table: "Times",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logo",
                table: "Times",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Times_Clubes_ClubeId",
                table: "Times",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Times_Clubes_ClubeId",
                table: "Times");

            migrationBuilder.DropColumn(
                name: "DonoId",
                table: "Times");

            migrationBuilder.DropColumn(
                name: "Logo",
                table: "Times");

            migrationBuilder.AlterColumn<int>(
                name: "ClubeId",
                table: "Times",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Times_Clubes_ClubeId",
                table: "Times",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
