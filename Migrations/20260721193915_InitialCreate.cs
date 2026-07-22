using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriaPadrao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaPadrao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clubes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endereco = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contato = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Codigo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SenhaHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Organiza__3214EC079F755F08", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Times",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClubeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Times", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Times_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Torneio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizadorId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Codigo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    DataInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PermiteImpedimentos = table.Column<bool>(type: "bit", nullable: false),
                    PrecoInscricao = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LocalTorneio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantidadeQuadras = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormatoUnico = table.Column<bool>(type: "bit", nullable: false),
                    SetsFaseGrupos = table.Column<int>(type: "int", nullable: false),
                    GamesFaseGrupos = table.Column<int>(type: "int", nullable: false),
                    SetsFaseMataMata = table.Column<int>(type: "int", nullable: false),
                    GamesFaseMataMata = table.Column<int>(type: "int", nullable: false),
                    SetsFaseFinal = table.Column<int>(type: "int", nullable: false),
                    GamesFaseFinal = table.Column<int>(type: "int", nullable: false),
                    ClubeId = table.Column<int>(type: "int", nullable: false),
                    TempoPrevistoPartidaMinutos = table.Column<int>(type: "int", nullable: false),
                    TamanhoGrupo = table.Column<int>(type: "int", nullable: false),
                    ClassificadosPorGrupo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Torneio__3214EC072B430D79", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Torneio_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK__Torneio__Organiz__4E88ABD4",
                        column: x => x.OrganizadorId,
                        principalTable: "Organizador",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Jogador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    CPF = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    Codigo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Celular = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SenhaHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FotoPerfil = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PontuacaoGlobal = table.Column<int>(type: "int", nullable: false),
                    IsProfessor = table.Column<bool>(type: "bit", nullable: false),
                    LadoQuadra = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NotificarEmail = table.Column<bool>(type: "bit", nullable: false),
                    NotificarWhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    AgendaMostrarJogosSemanais = table.Column<bool>(type: "bit", nullable: false),
                    AgendaMostrarTorneios = table.Column<bool>(type: "bit", nullable: false),
                    AgendaMostrarAulas = table.Column<bool>(type: "bit", nullable: false),
                    AgendaMostrarAlunos = table.Column<bool>(type: "bit", nullable: false),
                    TimeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Jogador__3214EC07E9B77CE2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jogador_Times_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Times",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Categoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TorneioId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Codigo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Categori__3214EC079FE58FF8", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Categoria__Torne__5165187F",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AvisoJogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CriadorId = table.Column<int>(type: "int", nullable: false),
                    ClubeId = table.Column<int>(type: "int", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "int", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoJogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvisoJogo_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoJogo_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoJogo_Jogador_CriadorId",
                        column: x => x.CriadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AvisoParceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CriadorId = table.Column<int>(type: "int", nullable: false),
                    Local = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NomeTorneio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoParceiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvisoParceiro_Jogador_CriadorId",
                        column: x => x.CriadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrupoPrivado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoConvite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrupoPrivado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrupoPrivado_Jogador_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JogadorCategoria",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorCategoria", x => new { x.JogadorId, x.CategoriaPadraoId });
                    table.ForeignKey(
                        name: "FK_JogadorCategoria_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogadorCategoria_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JogadorClube",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    ClubeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorClube", x => new { x.JogadorId, x.ClubeId });
                    table.ForeignKey(
                        name: "FK_JogadorClube_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogadorClube_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JogadorDiaHorario",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorDiaHorario", x => new { x.JogadorId, x.DiaSemana, x.Periodo });
                    table.ForeignKey(
                        name: "FK_JogadorDiaHorario_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalAula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endereco = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrecoPadrao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CustoPorAula = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PacoteAtivo = table.Column<bool>(type: "bit", nullable: false),
                    PacoteQuantidadeAulas = table.Column<int>(type: "int", nullable: true),
                    PacotePreco = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalAula_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TorneioOrganizador",
                columns: table => new
                {
                    TorneioId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    NivelAcesso = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TorneioOrganizador", x => new { x.TorneioId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_TorneioOrganizador_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TorneioOrganizador_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrupoTorneio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrupoTorneio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrupoTorneio_Categoria_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidaturaParceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AvisoParceiroId = table.Column<int>(type: "int", nullable: false),
                    CandidatoId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidaturaParceiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidaturaParceiro_AvisoParceiro_AvisoParceiroId",
                        column: x => x.AvisoParceiroId,
                        principalTable: "AvisoParceiro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidaturaParceiro_Jogador_CandidatoId",
                        column: x => x.CandidatoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JogadorGrupo",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    GrupoId = table.Column<int>(type: "int", nullable: false),
                    PontuacaoInterna = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorGrupo", x => new { x.JogadorId, x.GrupoId });
                    table.ForeignKey(
                        name: "FK_JogadorGrupo_GrupoPrivado_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GrupoPrivado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogadorGrupo_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JogoSemanal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrupoId = table.Column<int>(type: "int", nullable: false),
                    DataJogo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Dupla1Jogador1Id = table.Column<int>(type: "int", nullable: false),
                    Dupla1Jogador2Id = table.Column<int>(type: "int", nullable: false),
                    Dupla2Jogador1Id = table.Column<int>(type: "int", nullable: false),
                    Dupla2Jogador2Id = table.Column<int>(type: "int", nullable: false),
                    GamesDupla1 = table.Column<int>(type: "int", nullable: false),
                    GamesDupla2 = table.Column<int>(type: "int", nullable: false),
                    RegistradoPorId = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogoSemanal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_GrupoPrivado_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GrupoPrivado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla1Jogador1Id",
                        column: x => x.Dupla1Jogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla1Jogador2Id",
                        column: x => x.Dupla1Jogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla2Jogador1Id",
                        column: x => x.Dupla2Jogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla2Jogador2Id",
                        column: x => x.Dupla2Jogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Aula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    AlunoId = table.Column<int>(type: "int", nullable: true),
                    LocalAulaId = table.Column<int>(type: "int", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NomeAlunoAvulso = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TelefoneAlunoAvulso = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RecorrenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenConfirmacao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleEventId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aula_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Aula_LocalAula_LocalAulaId",
                        column: x => x.LocalAulaId,
                        principalTable: "LocalAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK__Aula__AlunoId__114A936A",
                        column: x => x.AlunoId,
                        principalTable: "Jogador",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HorarioDisponivel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    LocalAulaId = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "time", nullable: false),
                    HoraFim = table.Column<TimeSpan>(type: "time", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorarioDisponivel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorarioDisponivel_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HorarioDisponivel_LocalAula_LocalAulaId",
                        column: x => x.LocalAulaId,
                        principalTable: "LocalAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dupla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Jogador1Id = table.Column<int>(type: "int", nullable: false),
                    Jogador2Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ImpedimentoSextaNoite = table.Column<bool>(type: "bit", nullable: false),
                    ImpedimentoSabadoManha = table.Column<bool>(type: "bit", nullable: false),
                    ImpedimentoSabadoTarde = table.Column<bool>(type: "bit", nullable: false),
                    GrupoTorneioId = table.Column<int>(type: "int", nullable: true),
                    UltimaFase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Grupo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Dupla__3214EC07E192F96C", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dupla_GrupoTorneio_GrupoTorneioId",
                        column: x => x.GrupoTorneioId,
                        principalTable: "GrupoTorneio",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Dupla__Categoria__571DF1D5",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Dupla__Jogador1I__5812160E",
                        column: x => x.Jogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Dupla__Jogador2I__59063A47",
                        column: x => x.Jogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Partida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Dupla1Id = table.Column<int>(type: "int", nullable: false),
                    Dupla2Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SetsDupla1 = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    SetsDupla2 = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    GamesDupla1 = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    GamesDupla2 = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    TorneioId = table.Column<int>(type: "int", nullable: true),
                    SendoTransmitida = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VencedorId = table.Column<int>(type: "int", nullable: true),
                    Fase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HorarioPrevisto = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HorarioInicioReal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HorarioFimReal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NomeQuadra = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkTransmissao = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Partida__3214EC0755DC2078", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Partida__Categor__5FB337D6",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Partida__Dupla1I__60A75C0F",
                        column: x => x.Dupla1Id,
                        principalTable: "Dupla",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Partida__Dupla2I__619B8048",
                        column: x => x.Dupla2Id,
                        principalTable: "Dupla",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PalpitePartida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartidaId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    DuplaEscolhidaId = table.Column<int>(type: "int", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PalpitePartida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PalpitePartida_Dupla_DuplaEscolhidaId",
                        column: x => x.DuplaEscolhidaId,
                        principalTable: "Dupla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PalpitePartida_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PalpitePartida_Partida_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aula_AlunoId",
                table: "Aula",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Aula_LocalAulaId",
                table: "Aula",
                column: "LocalAulaId");

            migrationBuilder.CreateIndex(
                name: "IX_Aula_ProfessorId",
                table: "Aula",
                column: "ProfessorId");

            migrationBuilder.CreateIndex(
                name: "IX_Aula_TokenConfirmacao",
                table: "Aula",
                column: "TokenConfirmacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvisoJogo_CategoriaPadraoId",
                table: "AvisoJogo",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoJogo_ClubeId",
                table: "AvisoJogo",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoJogo_CriadorId",
                table: "AvisoJogo",
                column: "CriadorId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoParceiro_CriadorId",
                table: "AvisoParceiro",
                column: "CriadorId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidaturaParceiro_AvisoParceiroId",
                table: "CandidaturaParceiro",
                column: "AvisoParceiroId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidaturaParceiro_CandidatoId",
                table: "CandidaturaParceiro",
                column: "CandidatoId");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_TorneioId",
                table: "Categoria",
                column: "TorneioId");

            migrationBuilder.CreateIndex(
                name: "IX_Dupla_CategoriaId",
                table: "Dupla",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Dupla_GrupoTorneioId",
                table: "Dupla",
                column: "GrupoTorneioId");

            migrationBuilder.CreateIndex(
                name: "IX_Dupla_Jogador1Id",
                table: "Dupla",
                column: "Jogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Dupla_Jogador2Id",
                table: "Dupla",
                column: "Jogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_GrupoPrivado_AdministradorId",
                table: "GrupoPrivado",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_GrupoTorneio_CategoriaId",
                table: "GrupoTorneio",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_HorarioDisponivel_LocalAulaId",
                table: "HorarioDisponivel",
                column: "LocalAulaId");

            migrationBuilder.CreateIndex(
                name: "IX_HorarioDisponivel_ProfessorId",
                table: "HorarioDisponivel",
                column: "ProfessorId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogador_TimeId",
                table: "Jogador",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "UQ__Jogador__C1F897318C6002EF",
                table: "Jogador",
                column: "CPF",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JogadorCategoria_CategoriaPadraoId",
                table: "JogadorCategoria",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogadorClube_ClubeId",
                table: "JogadorClube",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_JogadorGrupo_GrupoId",
                table: "JogadorGrupo",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla1Jogador1Id",
                table: "JogoSemanal",
                column: "Dupla1Jogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla1Jogador2Id",
                table: "JogoSemanal",
                column: "Dupla1Jogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla2Jogador1Id",
                table: "JogoSemanal",
                column: "Dupla2Jogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla2Jogador2Id",
                table: "JogoSemanal",
                column: "Dupla2Jogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_GrupoId",
                table: "JogoSemanal",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_RegistradoPorId",
                table: "JogoSemanal",
                column: "RegistradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalAula_ProfessorId",
                table: "LocalAula",
                column: "ProfessorId");

            migrationBuilder.CreateIndex(
                name: "UQ__Organiza__06370DAC46EF791B",
                table: "Organizador",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Organiza__A9D10534817DC0DF",
                table: "Organizador",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PalpitePartida_DuplaEscolhidaId",
                table: "PalpitePartida",
                column: "DuplaEscolhidaId");

            migrationBuilder.CreateIndex(
                name: "IX_PalpitePartida_JogadorId",
                table: "PalpitePartida",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PalpitePartida_PartidaId_JogadorId",
                table: "PalpitePartida",
                columns: new[] { "PartidaId", "JogadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partida_CategoriaId",
                table: "Partida",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Partida_Dupla1Id",
                table: "Partida",
                column: "Dupla1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Partida_Dupla2Id",
                table: "Partida",
                column: "Dupla2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Times_ClubeId",
                table: "Times",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_Torneio_ClubeId",
                table: "Torneio",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_Torneio_OrganizadorId",
                table: "Torneio",
                column: "OrganizadorId");

            migrationBuilder.CreateIndex(
                name: "UQ__Torneio__06370DAC1187A52A",
                table: "Torneio",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TorneioOrganizador_JogadorId",
                table: "TorneioOrganizador",
                column: "JogadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aula");

            migrationBuilder.DropTable(
                name: "AvisoJogo");

            migrationBuilder.DropTable(
                name: "CandidaturaParceiro");

            migrationBuilder.DropTable(
                name: "HorarioDisponivel");

            migrationBuilder.DropTable(
                name: "JogadorCategoria");

            migrationBuilder.DropTable(
                name: "JogadorClube");

            migrationBuilder.DropTable(
                name: "JogadorDiaHorario");

            migrationBuilder.DropTable(
                name: "JogadorGrupo");

            migrationBuilder.DropTable(
                name: "JogoSemanal");

            migrationBuilder.DropTable(
                name: "PalpitePartida");

            migrationBuilder.DropTable(
                name: "TorneioOrganizador");

            migrationBuilder.DropTable(
                name: "AvisoParceiro");

            migrationBuilder.DropTable(
                name: "LocalAula");

            migrationBuilder.DropTable(
                name: "CategoriaPadrao");

            migrationBuilder.DropTable(
                name: "GrupoPrivado");

            migrationBuilder.DropTable(
                name: "Partida");

            migrationBuilder.DropTable(
                name: "Dupla");

            migrationBuilder.DropTable(
                name: "GrupoTorneio");

            migrationBuilder.DropTable(
                name: "Jogador");

            migrationBuilder.DropTable(
                name: "Categoria");

            migrationBuilder.DropTable(
                name: "Times");

            migrationBuilder.DropTable(
                name: "Torneio");

            migrationBuilder.DropTable(
                name: "Clubes");

            migrationBuilder.DropTable(
                name: "Organizador");
        }
    }
}
