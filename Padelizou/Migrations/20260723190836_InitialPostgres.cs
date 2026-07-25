using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriaPadrao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaPadrao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cidades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(255)", unicode: false, maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Organiza__3214EC079F755F08", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Aula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: true),
                    LocalAulaId = table.Column<int>(type: "integer", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    NomeAlunoAvulso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TelefoneAlunoAvulso = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RecorrenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenConfirmacao = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleEventId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aula", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvisoJogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CriadorId = table.Column<int>(type: "integer", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "integer", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "AvisoParceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CriadorId = table.Column<int>(type: "integer", nullable: false),
                    Local = table.Column<string>(type: "text", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NomeTorneio = table.Column<string>(type: "text", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoParceiro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvisoRaqueteLivre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    CriadorId = table.Column<int>(type: "integer", nullable: false),
                    DataHoraInicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DataHoraFim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    LimiteVagas = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoRaqueteLivre", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidaturaParceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvisoParceiroId = table.Column<int>(type: "integer", nullable: false),
                    CandidatoId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Categoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    LimiteDuplas = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Categori__3214EC079FE58FF8", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GrupoTorneio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false)
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
                name: "ClubeAdministrador",
                columns: table => new
                {
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    AdicionadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubeAdministrador", x => new { x.ClubeId, x.JogadorId });
                });

            migrationBuilder.CreateTable(
                name: "Clubes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Endereco = table.Column<string>(type: "text", nullable: false),
                    Contato = table.Column<string>(type: "text", nullable: false),
                    DonoId = table.Column<int>(type: "integer", nullable: true),
                    CidadeId = table.Column<int>(type: "integer", nullable: true),
                    MarcacaoHorariosAtiva = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarHorariosDiariamente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clubes_Cidades_CidadeId",
                        column: x => x.CidadeId,
                        principalTable: "Cidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuadraClube",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuadraClube", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuadraClube_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Times",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: true),
                    DonoId = table.Column<int>(type: "integer", nullable: true),
                    Logo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Times", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Times_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Torneio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizadorId = table.Column<int>(type: "integer", nullable: true),
                    Nome = table.Column<string>(type: "character varying(150)", unicode: false, maxLength: 150, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    DataInicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PermiteImpedimentos = table.Column<bool>(type: "boolean", nullable: false),
                    PermiteImpedimentoSextaNoite = table.Column<bool>(type: "boolean", nullable: false),
                    PermiteImpedimentoSabadoManha = table.Column<bool>(type: "boolean", nullable: false),
                    PermiteImpedimentoSabadoTarde = table.Column<bool>(type: "boolean", nullable: false),
                    PrecoInscricao = table.Column<decimal>(type: "numeric", nullable: false),
                    LocalTorneio = table.Column<string>(type: "text", nullable: true),
                    ImagemCapa = table.Column<string>(type: "text", nullable: true),
                    QuantidadeQuadras = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Formato = table.Column<string>(type: "text", nullable: false),
                    FormatoUnico = table.Column<bool>(type: "boolean", nullable: false),
                    SetsFaseGrupos = table.Column<int>(type: "integer", nullable: false),
                    GamesFaseGrupos = table.Column<int>(type: "integer", nullable: false),
                    SetsFaseMataMata = table.Column<int>(type: "integer", nullable: false),
                    GamesFaseMataMata = table.Column<int>(type: "integer", nullable: false),
                    SetsFaseFinal = table.Column<int>(type: "integer", nullable: false),
                    GamesFaseFinal = table.Column<int>(type: "integer", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    TempoPrevistoPartidaMinutos = table.Column<int>(type: "integer", nullable: false),
                    TamanhoGrupo = table.Column<int>(type: "integer", nullable: false),
                    ClassificadosPorGrupo = table.Column<int>(type: "integer", nullable: false),
                    BloquearCategoriaInferior = table.Column<bool>(type: "boolean", nullable: false),
                    RestricaoCategoria = table.Column<string>(type: "text", nullable: false),
                    LimiteDuplasTotal = table.Column<int>(type: "integer", nullable: true),
                    Restrito = table.Column<bool>(type: "boolean", nullable: false),
                    ChaveAcesso = table.Column<string>(type: "text", nullable: true)
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
                name: "HorarioMarcacaoDisponivel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    QuadraClubeId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HoraFim = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorarioMarcacaoDisponivel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorarioMarcacaoDisponivel_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HorarioMarcacaoDisponivel_QuadraClube_QuadraClubeId",
                        column: x => x.QuadraClubeId,
                        principalTable: "QuadraClube",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jogador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    CPF = table.Column<string>(type: "character varying(11)", unicode: false, maxLength: 11, nullable: false),
                    Login = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    Celular = table.Column<string>(type: "text", nullable: true),
                    Cidade = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    SenhaHash = table.Column<string>(type: "text", nullable: true),
                    FotoPerfil = table.Column<string>(type: "text", nullable: true),
                    Instagram = table.Column<string>(type: "text", nullable: true),
                    PontuacaoGlobal = table.Column<int>(type: "integer", nullable: false),
                    IsProfessor = table.Column<bool>(type: "boolean", nullable: false),
                    LadoQuadra = table.Column<string>(type: "text", nullable: true),
                    Lateralidade = table.Column<string>(type: "text", nullable: true),
                    PerfilPrivado = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarEmail = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarWhatsApp = table.Column<bool>(type: "boolean", nullable: false),
                    AceitaConvitesJogo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AgendaMostrarJogosSemanais = table.Column<bool>(type: "boolean", nullable: false),
                    AgendaMostrarTorneios = table.Column<bool>(type: "boolean", nullable: false),
                    AgendaMostrarAulas = table.Column<bool>(type: "boolean", nullable: false),
                    AgendaMostrarAlunos = table.Column<bool>(type: "boolean", nullable: false),
                    AgendaMostrarMarcacoes = table.Column<bool>(type: "boolean", nullable: false),
                    AgendaFeedToken = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAdminRaiz = table.Column<bool>(type: "boolean", nullable: false),
                    IsAdminGeral = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarTorneiosAbertos = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarSeguidosTorneio = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarAvisoJogo = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarJogoAula = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarRaqueteLivre = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarHorarioVagoRegiao = table.Column<bool>(type: "boolean", nullable: false),
                    TimeId = table.Column<int>(type: "integer", nullable: true)
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
                name: "Quadra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quadra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quadra_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dupla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Jogador1Id = table.Column<int>(type: "integer", nullable: false),
                    Jogador2Id = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    ImpedimentoSextaNoite = table.Column<bool>(type: "boolean", nullable: false),
                    ImpedimentoSabadoManha = table.Column<bool>(type: "boolean", nullable: false),
                    ImpedimentoSabadoTarde = table.Column<bool>(type: "boolean", nullable: false),
                    GrupoTorneioId = table.Column<int>(type: "integer", nullable: true),
                    UltimaFase = table.Column<string>(type: "text", nullable: false),
                    Grupo = table.Column<string>(type: "text", nullable: true),
                    EmListaDeEspera = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "GrupoPrivado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    CodigoConvite = table.Column<string>(type: "text", nullable: false),
                    AdministradorId = table.Column<int>(type: "integer", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: true),
                    DiaSemanaFixo = table.Column<int>(type: "integer", nullable: true),
                    HorarioFixo = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CategoriaPadraoId = table.Column<int>(type: "integer", nullable: true),
                    ValorMensalidade = table.Column<decimal>(type: "numeric", nullable: true),
                    ValorAvulso = table.Column<decimal>(type: "numeric", nullable: true),
                    VagasMaximas = table.Column<int>(type: "integer", nullable: false),
                    EnviarLembrete24h = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrupoPrivado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrupoPrivado_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrupoPrivado_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrupoPrivado_Jogador_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InscricaoAmericana",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    EmListaDeEspera = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricaoAmericana", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InscricaoAmericana_Categoria_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InscricaoAmericana_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InscricaoRaqueteLivre",
                columns: table => new
                {
                    AvisoRaqueteLivreId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    EmListaDeEspera = table.Column<bool>(type: "boolean", nullable: false),
                    InscritoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricaoRaqueteLivre", x => new { x.AvisoRaqueteLivreId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_InscricaoRaqueteLivre_AvisoRaqueteLivre_AvisoRaqueteLivreId",
                        column: x => x.AvisoRaqueteLivreId,
                        principalTable: "AvisoRaqueteLivre",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InscricaoRaqueteLivre_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JogadorCategoria",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "integer", nullable: false)
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
                name: "JogadorCidade",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    CidadeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorCidade", x => new { x.JogadorId, x.CidadeId });
                    table.ForeignKey(
                        name: "FK_JogadorCidade_Cidades_CidadeId",
                        column: x => x.CidadeId,
                        principalTable: "Cidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogadorCidade_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JogadorClube",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: false)
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
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    Periodo = table.Column<string>(type: "text", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Endereco = table.Column<string>(type: "text", nullable: false),
                    PrecoPadrao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CustoPorAula = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PacoteAtivo = table.Column<bool>(type: "boolean", nullable: false),
                    PacoteQuantidadeAulas = table.Column<int>(type: "integer", nullable: true),
                    PacotePreco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
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
                name: "MarcacaoJogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    QuadraClubeId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarcacaoJogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarcacaoJogo_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarcacaoJogo_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarcacaoJogo_QuadraClube_QuadraClubeId",
                        column: x => x.QuadraClubeId,
                        principalTable: "QuadraClube",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessorCidade",
                columns: table => new
                {
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    CidadeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorCidade", x => new { x.ProfessorId, x.CidadeId });
                    table.ForeignKey(
                        name: "FK_ProfessorCidade_Cidades_CidadeId",
                        column: x => x.CidadeId,
                        principalTable: "Cidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessorCidade_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptionJogador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    P256dh = table.Column<string>(type: "text", nullable: false),
                    Auth = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptionJogador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptionJogador_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeguidorJogador",
                columns: table => new
                {
                    SeguidorId = table.Column<int>(type: "integer", nullable: false),
                    SeguidoId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeguidorJogador", x => new { x.SeguidorId, x.SeguidoId });
                    table.ForeignKey(
                        name: "FK_SeguidorJogador_Jogador_SeguidoId",
                        column: x => x.SeguidoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeguidorJogador_Jogador_SeguidorId",
                        column: x => x.SeguidorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TorneioOrganizador",
                columns: table => new
                {
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    NivelAcesso = table.Column<string>(type: "text", nullable: false)
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
                name: "Partida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Dupla1Id = table.Column<int>(type: "integer", nullable: false),
                    Dupla2Id = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SetsDupla1 = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SetsDupla2 = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    GamesDupla1 = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    GamesDupla2 = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    TorneioId = table.Column<int>(type: "integer", nullable: true),
                    SendoTransmitida = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    VencedorId = table.Column<int>(type: "integer", nullable: true),
                    Fase = table.Column<string>(type: "text", nullable: false),
                    HorarioPrevisto = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HorarioInicioReal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HorarioFimReal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NomeQuadra = table.Column<string>(type: "text", nullable: true),
                    LinkTransmissao = table.Column<string>(type: "text", nullable: true)
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
                name: "JogadorGrupo",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    PontuacaoInterna = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    DataJogo = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Dupla1Jogador1Id = table.Column<int>(type: "integer", nullable: false),
                    Dupla1Jogador2Id = table.Column<int>(type: "integer", nullable: false),
                    Dupla2Jogador1Id = table.Column<int>(type: "integer", nullable: false),
                    Dupla2Jogador2Id = table.Column<int>(type: "integer", nullable: false),
                    GamesDupla1 = table.Column<int>(type: "integer", nullable: false),
                    GamesDupla2 = table.Column<int>(type: "integer", nullable: false),
                    RegistradoPorId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "MensalidadeGrupo",
                columns: table => new
                {
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    Pago = table.Column<bool>(type: "boolean", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensalidadeGrupo", x => new { x.GrupoId, x.JogadorId, x.Ano, x.Mes });
                    table.ForeignKey(
                        name: "FK_MensalidadeGrupo_GrupoPrivado_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GrupoPrivado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MensalidadeGrupo_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessaoGrupo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessaoGrupo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessaoGrupo_GrupoPrivado_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GrupoPrivado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HorarioDisponivel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    LocalAulaId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HoraFim = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "JogoAula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    LocalAulaId = table.Column<int>(type: "integer", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "integer", nullable: false),
                    Modalidade = table.Column<string>(type: "text", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    LimiteVagas = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogoAula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JogoAula_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoAula_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogoAula_LocalAula_LocalAulaId",
                        column: x => x.LocalAulaId,
                        principalTable: "LocalAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PalpitePartida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartidaId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    DuplaEscolhidaId = table.Column<int>(type: "integer", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ConfirmacaoSessao",
                columns: table => new
                {
                    SessaoId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Lado = table.Column<string>(type: "text", nullable: true),
                    Avulso = table.Column<bool>(type: "boolean", nullable: false),
                    RespondidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LembreteEnviadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmacaoSessao", x => new { x.SessaoId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_ConfirmacaoSessao_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmacaoSessao_SessaoGrupo_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "SessaoGrupo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InscricaoJogoAula",
                columns: table => new
                {
                    JogoAulaId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    EmListaDeEspera = table.Column<bool>(type: "boolean", nullable: false),
                    InscritoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricaoJogoAula", x => new { x.JogoAulaId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_InscricaoJogoAula_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InscricaoJogoAula_JogoAula_JogoAulaId",
                        column: x => x.JogoAulaId,
                        principalTable: "JogoAula",
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
                name: "IX_AvisoRaqueteLivre_ClubeId",
                table: "AvisoRaqueteLivre",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoRaqueteLivre_CriadorId",
                table: "AvisoRaqueteLivre",
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
                name: "IX_ClubeAdministrador_JogadorId",
                table: "ClubeAdministrador",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Clubes_CidadeId",
                table: "Clubes",
                column: "CidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_Clubes_DonoId",
                table: "Clubes",
                column: "DonoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmacaoSessao_JogadorId",
                table: "ConfirmacaoSessao",
                column: "JogadorId");

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
                name: "IX_GrupoPrivado_CategoriaPadraoId",
                table: "GrupoPrivado",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_GrupoPrivado_ClubeId",
                table: "GrupoPrivado",
                column: "ClubeId");

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
                name: "IX_HorarioMarcacaoDisponivel_ClubeId",
                table: "HorarioMarcacaoDisponivel",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_HorarioMarcacaoDisponivel_QuadraClubeId",
                table: "HorarioMarcacaoDisponivel",
                column: "QuadraClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricaoAmericana_CategoriaId",
                table: "InscricaoAmericana",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricaoAmericana_JogadorId",
                table: "InscricaoAmericana",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricaoJogoAula_JogadorId",
                table: "InscricaoJogoAula",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricaoRaqueteLivre_JogadorId",
                table: "InscricaoRaqueteLivre",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogador_AgendaFeedToken",
                table: "Jogador",
                column: "AgendaFeedToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jogador_Login",
                table: "Jogador",
                column: "Login",
                unique: true);

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
                name: "IX_JogadorCidade_CidadeId",
                table: "JogadorCidade",
                column: "CidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_JogadorClube_ClubeId",
                table: "JogadorClube",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_JogadorGrupo_GrupoId",
                table: "JogadorGrupo",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoAula_CategoriaPadraoId",
                table: "JogoAula",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoAula_LocalAulaId",
                table: "JogoAula",
                column: "LocalAulaId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoAula_ProfessorId",
                table: "JogoAula",
                column: "ProfessorId");

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
                name: "IX_MarcacaoJogo_ClubeId",
                table: "MarcacaoJogo",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarcacaoJogo_JogadorId",
                table: "MarcacaoJogo",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarcacaoJogo_QuadraClubeId",
                table: "MarcacaoJogo",
                column: "QuadraClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_MensalidadeGrupo_JogadorId",
                table: "MensalidadeGrupo",
                column: "JogadorId");

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
                name: "IX_ProfessorCidade_CidadeId",
                table: "ProfessorCidade",
                column: "CidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptionJogador_Endpoint",
                table: "PushSubscriptionJogador",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptionJogador_JogadorId",
                table: "PushSubscriptionJogador",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Quadra_TorneioId",
                table: "Quadra",
                column: "TorneioId");

            migrationBuilder.CreateIndex(
                name: "IX_QuadraClube_ClubeId",
                table: "QuadraClube",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_SeguidorJogador_SeguidoId",
                table: "SeguidorJogador",
                column: "SeguidoId");

            migrationBuilder.CreateIndex(
                name: "IX_SessaoGrupo_GrupoId_DataHora",
                table: "SessaoGrupo",
                columns: new[] { "GrupoId", "DataHora" },
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Aula_Jogador_ProfessorId",
                table: "Aula",
                column: "ProfessorId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK__Aula__AlunoId__114A936A",
                table: "Aula",
                column: "AlunoId",
                principalTable: "Jogador",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Aula_LocalAula_LocalAulaId",
                table: "Aula",
                column: "LocalAulaId",
                principalTable: "LocalAula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AvisoJogo_Clubes_ClubeId",
                table: "AvisoJogo",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AvisoJogo_Jogador_CriadorId",
                table: "AvisoJogo",
                column: "CriadorId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AvisoParceiro_Jogador_CriadorId",
                table: "AvisoParceiro",
                column: "CriadorId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AvisoRaqueteLivre_Clubes_ClubeId",
                table: "AvisoRaqueteLivre",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AvisoRaqueteLivre_Jogador_CriadorId",
                table: "AvisoRaqueteLivre",
                column: "CriadorId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CandidaturaParceiro_Jogador_CandidatoId",
                table: "CandidaturaParceiro",
                column: "CandidatoId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK__Categoria__Torne__5165187F",
                table: "Categoria",
                column: "TorneioId",
                principalTable: "Torneio",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ClubeAdministrador_Clubes_ClubeId",
                table: "ClubeAdministrador",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClubeAdministrador_Jogador_JogadorId",
                table: "ClubeAdministrador",
                column: "JogadorId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Clubes_Jogador_DonoId",
                table: "Clubes",
                column: "DonoId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clubes_Jogador_DonoId",
                table: "Clubes");

            migrationBuilder.DropTable(
                name: "Aula");

            migrationBuilder.DropTable(
                name: "AvisoJogo");

            migrationBuilder.DropTable(
                name: "CandidaturaParceiro");

            migrationBuilder.DropTable(
                name: "ClubeAdministrador");

            migrationBuilder.DropTable(
                name: "ConfirmacaoSessao");

            migrationBuilder.DropTable(
                name: "HorarioDisponivel");

            migrationBuilder.DropTable(
                name: "HorarioMarcacaoDisponivel");

            migrationBuilder.DropTable(
                name: "InscricaoAmericana");

            migrationBuilder.DropTable(
                name: "InscricaoJogoAula");

            migrationBuilder.DropTable(
                name: "InscricaoRaqueteLivre");

            migrationBuilder.DropTable(
                name: "JogadorCategoria");

            migrationBuilder.DropTable(
                name: "JogadorCidade");

            migrationBuilder.DropTable(
                name: "JogadorClube");

            migrationBuilder.DropTable(
                name: "JogadorDiaHorario");

            migrationBuilder.DropTable(
                name: "JogadorGrupo");

            migrationBuilder.DropTable(
                name: "JogoSemanal");

            migrationBuilder.DropTable(
                name: "MarcacaoJogo");

            migrationBuilder.DropTable(
                name: "MensalidadeGrupo");

            migrationBuilder.DropTable(
                name: "PalpitePartida");

            migrationBuilder.DropTable(
                name: "ProfessorCidade");

            migrationBuilder.DropTable(
                name: "PushSubscriptionJogador");

            migrationBuilder.DropTable(
                name: "Quadra");

            migrationBuilder.DropTable(
                name: "SeguidorJogador");

            migrationBuilder.DropTable(
                name: "TorneioOrganizador");

            migrationBuilder.DropTable(
                name: "AvisoParceiro");

            migrationBuilder.DropTable(
                name: "SessaoGrupo");

            migrationBuilder.DropTable(
                name: "JogoAula");

            migrationBuilder.DropTable(
                name: "AvisoRaqueteLivre");

            migrationBuilder.DropTable(
                name: "QuadraClube");

            migrationBuilder.DropTable(
                name: "Partida");

            migrationBuilder.DropTable(
                name: "GrupoPrivado");

            migrationBuilder.DropTable(
                name: "LocalAula");

            migrationBuilder.DropTable(
                name: "Dupla");

            migrationBuilder.DropTable(
                name: "CategoriaPadrao");

            migrationBuilder.DropTable(
                name: "GrupoTorneio");

            migrationBuilder.DropTable(
                name: "Categoria");

            migrationBuilder.DropTable(
                name: "Torneio");

            migrationBuilder.DropTable(
                name: "Organizador");

            migrationBuilder.DropTable(
                name: "Jogador");

            migrationBuilder.DropTable(
                name: "Times");

            migrationBuilder.DropTable(
                name: "Clubes");

            migrationBuilder.DropTable(
                name: "Cidades");
        }
    }
}
