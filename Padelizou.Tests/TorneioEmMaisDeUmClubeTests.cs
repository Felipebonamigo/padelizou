using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O TORNEIO EM MAIS DE UM CLUBE (Services/SedesDoTorneio).
//
// O que estes testes protegem são as DUAS regras de naturezas opostas que a coisa toda tem, e a
// ordem entre elas:
//   • onde a categoria joga é trava DURA — a 4ª não vai pro outro clube nem com o horário lotado;
//   • a folga pra atravessar a cidade é MOLE — ela cede assim que respeitá-la deixaria uma
//     quadra vazia, porque a prioridade declarada do torneio é nenhuma quadra parar.
// Trocar uma pela outra é o defeito que este arquivo existe pra impedir.
public class TorneioEmMaisDeUmClubeTests
{
    private const int Nata = 10;
    private const int Batata = 20;

    private const int CategoriaDoNata = 1;
    private const int CategoriaDoBatata = 2;
    private const int SemSede = 3;

    private const int Duracao = 50;

    private static Quadra QuadraDe(string nome, int? clube) =>
        new() { Nome = nome, ClubeId = clube };

    private static Categoria CategoriaDe(int id, int? clube) =>
        new() { Id = id, ClubeId = clube };

    private static Partida Jogo(int categoriaId, int dupla1, int dupla2) =>
        new() { CategoriaId = categoriaId, Dupla1Id = dupla1, Dupla2Id = dupla2 };

    // Duas quadras em cada clube. O clube principal é o Nata, e a quadra sem clube dito é dele —
    // é assim que todo torneio anterior a esta opção continua funcionando sem conversão nenhuma.
    private static readonly string[] TodasAsQuadras =
        { "Central Nata", "Quadra 2 Nata", "Central Batata", "Quadra 2 Batata" };

    private static SedesDoTorneio DoisClubes(int folgaMinutos = 30, params Categoria[] categorias) =>
        SedesDoTorneio.Montar(
            clubePrincipalId: Nata,
            minutosParaTrocarDeClube: folgaMinutos,
            quadras: new[]
            {
                QuadraDe("Central Nata", null),          // nulo = clube do torneio
                QuadraDe("Quadra 2 Nata", Nata),
                QuadraDe("Central Batata", Batata),
                QuadraDe("Quadra 2 Batata", Batata),
            },
            categorias: categorias.Length > 0
                ? categorias
                : new[]
                {
                    CategoriaDe(CategoriaDoNata, Nata),
                    CategoriaDe(CategoriaDoBatata, Batata),
                    CategoriaDe(SemSede, null),
                },
            nomesDosClubes: new Dictionary<int, string> { [Nata] = "Nata", [Batata] = "Batata" });

    private static List<DateTime> Vagas(DateTime inicio, int quantosHorarios, int quadras) =>
        GradeDeJogos.Horarios(inicio, new TimeSpan(23, 0, 0), quadras, Duracao, quantosHorarios * quadras).ToList();

    // ── O mapa, sem grade nenhuma ─────────────────────────────────────────────────────────

    [Fact]
    public void Quadra_sem_clube_dito_e_do_clube_do_torneio()
    {
        var sedes = DoisClubes();

        Assert.Equal(Nata, sedes.ClubeDaQuadra("Central Nata"));
        Assert.Equal(Batata, sedes.ClubeDaQuadra("Central Batata"));
    }

    // O interruptor que as telas consultam. Sem ele, "Nata · Quadra 2" apareceria em cada linha
    // da lista de jogos de um torneio de uma sede só — copiando o cabeçalho da página.
    [Fact]
    public void Torneio_de_um_clube_so_nao_tem_sede_nenhuma()
    {
        var sedes = SedesDoTorneio.Montar(Nata, 30,
            new[] { QuadraDe("Central", null), QuadraDe("Quadra 2", Nata) },
            new[] { CategoriaDe(CategoriaDoNata, Nata) });

        Assert.False(sedes.MaisDeUmClube);
        Assert.Null(sedes.QuadrasDe(CategoriaDoNata));
        Assert.Equal(TimeSpan.Zero, sedes.FolgaParaTrocarDeClube);
    }

    [Fact]
    public void A_categoria_presa_a_um_clube_so_enxerga_as_quadras_dele()
    {
        var doNata = DoisClubes().QuadrasDe(CategoriaDoNata);

        Assert.NotNull(doNata);
        Assert.Equal(new[] { "Central Nata", "Quadra 2 Nata" }, doNata!.OrderBy(q => q));
    }

    [Fact]
    public void Categoria_sem_clube_joga_em_qualquer_quadra()
    {
        Assert.Null(DoisClubes().QuadrasDe(SemSede));
    }

    // ⚠️ A guarda defensiva. O organizador ainda apaga quadra depois do sorteio sem trava
    // nenhuma, e uma lista VAZIA faria a grade nunca achar quadra pra essa categoria — ela
    // ficaria sem horário nenhum, calada. O preço de uma sede que evaporou não pode ser a
    // categoria inteira sumir da grade.
    [Fact]
    public void Categoria_num_clube_que_ficou_sem_quadra_volta_a_jogar_em_qualquer_lugar()
    {
        const int ClubeQueEvaporou = 99;
        var sedes = DoisClubes(30,
            CategoriaDe(CategoriaDoNata, Nata),
            CategoriaDe(CategoriaDoBatata, ClubeQueEvaporou));

        Assert.Null(sedes.QuadrasDe(CategoriaDoBatata));
        // E o par tem que concordar: sem quadras presas, não há clube pra folga comparar.
        Assert.Null(sedes.ClubeDaCategoria(CategoriaDoBatata));
    }

    // ── A trava DURA na grade ─────────────────────────────────────────────────────────────

    // O coração da coisa. É por isto que sede NÃO pôde pegar carona na quadra preferida: o
    // degrau 3 de PreferenciaDeQuadra cede a quadra quando aperta, e ceder aqui manda o jogador
    // pro outro lado da cidade no meio do torneio.
    [Fact]
    public void Nenhum_jogo_cai_em_quadra_de_outro_clube_nem_com_a_grade_lotada()
    {
        var inicio = new DateTime(2026, 9, 5, 8, 0, 0);

        // 12 jogos do Nata, que tem só 2 quadras — a grade aperta de verdade.
        var jogos = Enumerable.Range(0, 12)
            .Select(i => Jogo(CategoriaDoNata, 100 + i * 2, 101 + i * 2))
            .ToList();

        GradeDeJogos.Encaixar(jogos, Vagas(inicio, 12, 4), Duracao,
            quadras: TodasAsQuadras, sedes: DoisClubes());

        var marcados = jogos.Where(j => j.HorarioPrevisto != null).ToList();
        Assert.NotEmpty(marcados);

        foreach (var jogo in marcados)
        {
            Assert.NotNull(jogo.NomeQuadra);
            Assert.Contains("Nata", jogo.NomeQuadra!);
        }
    }

    // A outra metade da mesma regra, e a que o organizador cobraria primeiro: a quadra do outro
    // clube não pode ficar parada só porque a categoria da vez não pode usá-la. Quem entra é um
    // jogo que está mais atrás na fila e PODE.
    [Fact]
    public void A_quadra_do_outro_clube_recebe_jogo_de_quem_pode_usar_ela()
    {
        var inicio = new DateTime(2026, 9, 5, 8, 0, 0);

        // A fila começa com 6 jogos do Nata (que só têm 2 quadras) e só depois vem o Batata.
        // Sem escolher o jogo olhando a quadra, os dois horários do Batata ficariam vagos.
        var jogos = Enumerable.Range(0, 6).Select(i => Jogo(CategoriaDoNata, 100 + i * 2, 101 + i * 2))
            .Concat(Enumerable.Range(0, 6).Select(i => Jogo(CategoriaDoBatata, 200 + i * 2, 201 + i * 2)))
            .ToList();

        GradeDeJogos.Encaixar(jogos, Vagas(inicio, 6, 4), Duracao,
            quadras: TodasAsQuadras, sedes: DoisClubes());

        // No PRIMEIRO horário as quatro quadras têm que estar ocupadas: duas do Nata e duas do
        // Batata. É a prioridade declarada do torneio — nenhuma quadra parada.
        var noPrimeiroHorario = jogos.Where(j => j.HorarioPrevisto == inicio).ToList();

        Assert.Equal(4, noPrimeiroHorario.Count);
        Assert.Equal(2, noPrimeiroHorario.Count(j => j.NomeQuadra!.Contains("Batata")));
    }

    // ── A folga MOLE ──────────────────────────────────────────────────────────────────────

    // Quem joga duas categorias em clubes diferentes não pode ser chamado no Batata logo depois
    // de jogar no Nata. `Cruza` já separava os dois jogos por `duracao`; a folga soma mais.
    [Fact]
    public void Quem_joga_nos_dois_clubes_ganha_tempo_pra_atravessar_a_cidade()
    {
        var inicio = new DateTime(2026, 9, 5, 8, 0, 0);
        const int OMesmoSujeito = 500;

        var jogos = new List<Partida>
        {
            Jogo(CategoriaDoNata,   OMesmoSujeito, 501),
            Jogo(CategoriaDoBatata, OMesmoSujeito, 601),
        };

        // Duplas diferentes, MESMA pessoa nas duas — é o caso da chave direta e o motivo de a
        // grade comparar pessoa, e não dupla.
        var ocupantes = new Dictionary<int, int[]>
        {
            [OMesmoSujeito] = new[] { OMesmoSujeito },
            [501] = new[] { 501 },
            [601] = new[] { 601 },
        };

        // Grade folgada de propósito: aqui existe vaga sobrando, então a folga NÃO precisa ceder.
        GradeDeJogos.Encaixar(jogos, Vagas(inicio, 8, 4), Duracao,
            ocupantesPorDupla: ocupantes, quadras: TodasAsQuadras, sedes: DoisClubes(folgaMinutos: 30));

        var noNata = jogos[0].HorarioPrevisto!.Value;
        var noBatata = jogos[1].HorarioPrevisto!.Value;

        Assert.True((noBatata - noNata).Duration() >= TimeSpan.FromMinutes(Duracao + 30),
            $"Os dois jogos ficaram a {(noBatata - noNata).Duration().TotalMinutes} min um do outro — "
            + "não dá tempo de atravessar a cidade.");
    }

    // A folga não pode fazer um jogo ficar SEM HORÁRIO NENHUM. Com as vagas contadas, o último
    // recurso da grade tem que continuar valendo.
    //
    // ⚠️ ISTO NÃO PROVA A ORDEM DOS DEGRAUS — provei tentando: tornar a folga DURA (mudando os
    // dois degraus do escape pra também exigir `SemCorreria`) deixa este teste VERDE, porque o
    // `?? fila[0]` do fim pega o jogo de qualquer jeito. Quem prova a ordem é o teste logo
    // abaixo. Este aqui guarda o último recurso, que já existia antes das sedes e que eu
    // reescrevi junto.
    [Fact]
    public void Com_as_vagas_contadas_ninguem_fica_sem_horario()
    {
        var inicio = new DateTime(2026, 9, 5, 8, 0, 0);
        const int OMesmoSujeito = 500;

        var jogos = new List<Partida>
        {
            Jogo(CategoriaDoNata,   OMesmoSujeito, 501),
            Jogo(CategoriaDoBatata, OMesmoSujeito, 601),
        };

        var ocupantes = new Dictionary<int, int[]>
        {
            [OMesmoSujeito] = new[] { OMesmoSujeito },
            [501] = new[] { 501 },
            [601] = new[] { 601 },
        };

        // DUAS vagas pra DOIS jogos: não há pra onde empurrar.
        var vagasContadas = new List<DateTime> { inicio, inicio };

        GradeDeJogos.Encaixar(jogos, vagasContadas, Duracao,
            ocupantesPorDupla: ocupantes, quadras: TodasAsQuadras, sedes: DoisClubes(folgaMinutos: 30));

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    // ⚠️ A ORDEM EM QUE AS REGRAS CEDEM, que é a regra de negócio declarada pelo Felipe: a folga
    // pra atravessar a cidade cede ANTES de dobrar o jogador. Um sujeito com dois jogos
    // apertados em clubes diferentes o organizador resolve na hora; a mesma pessoa chamada pra
    // duas quadras ao mesmo tempo não tem conserto — alguém fica esperando em quadra.
    //
    // O cenário isola exatamente isso: uma vaga só, dois jogos na fila.
    //   • o PRIMEIRO da fila tem um jogador já em quadra (não está livre);
    //   • o SEGUNDO está livre, mas precisaria atravessar a cidade.
    // Com a folga DURA, os dois degraus do escape recusam o segundo e o `?? fila[0]` entrega o
    // primeiro — dobrando o jogador. Com ela mole, entra o segundo, e ninguém joga duas vezes.
    [Fact]
    public void Na_hora_do_aperto_a_folga_cede_antes_de_dobrar_o_jogador()
    {
        var abertura = new DateTime(2026, 9, 5, 8, 0, 0);
        var aVaga = abertura.AddMinutes(50);          // 08:50, a única vaga

        const int JaEmQuadra = 700;                   // joga 08:20, ainda em quadra às 08:50
        const int VindoDoNata = 800;                  // jogou 08:00 no Nata

        var jaMarcados = new List<Partida>
        {
            new() { CategoriaId = SemSede, Dupla1Id = JaEmQuadra, Dupla2Id = 701,
                    HorarioPrevisto = abertura.AddMinutes(20), NomeQuadra = "Quadra 2 Nata" },
            new() { CategoriaId = CategoriaDoNata, Dupla1Id = VindoDoNata, Dupla2Id = 801,
                    HorarioPrevisto = abertura, NomeQuadra = "Central Nata" },
        };

        var jogos = new List<Partida>
        {
            Jogo(SemSede,           JaEmQuadra,  702),   // 1º da fila: jogador ocupado
            Jogo(CategoriaDoBatata, VindoDoNata, 802),   // 2º: livre, mas atravessaria a cidade
        };

        var ocupantes = new Dictionary<int, int[]>
        {
            [JaEmQuadra] = new[] { JaEmQuadra },
            [VindoDoNata] = new[] { VindoDoNata },
            [701] = new[] { 701 }, [702] = new[] { 702 },
            [801] = new[] { 801 }, [802] = new[] { 802 },
        };

        GradeDeJogos.Encaixar(jogos, new List<DateTime> { aVaga }, Duracao,
            ocupantesPorDupla: ocupantes, quadras: TodasAsQuadras, jaMarcados: jaMarcados,
            sedes: DoisClubes(folgaMinutos: 30));

        Assert.Null(jogos[0].HorarioPrevisto);
        Assert.Equal(aVaga, jogos[1].HorarioPrevisto);
    }

    [Fact]
    public void Folga_zerada_desliga_a_regra()
    {
        var inicio = new DateTime(2026, 9, 5, 8, 0, 0);
        const int OMesmoSujeito = 500;

        var jogos = new List<Partida>
        {
            Jogo(CategoriaDoNata,   OMesmoSujeito, 501),
            Jogo(CategoriaDoBatata, OMesmoSujeito, 601),
        };

        var ocupantes = new Dictionary<int, int[]>
        {
            [OMesmoSujeito] = new[] { OMesmoSujeito },
            [501] = new[] { 501 },
            [601] = new[] { 601 },
        };

        GradeDeJogos.Encaixar(jogos, Vagas(inicio, 8, 4), Duracao,
            ocupantesPorDupla: ocupantes, quadras: TodasAsQuadras, sedes: DoisClubes(folgaMinutos: 0));

        var distancia = (jogos[1].HorarioPrevisto!.Value - jogos[0].HorarioPrevisto!.Value).Duration();

        // Sem folga vale só a regra de sempre: a mesma pessoa não joga em duas quadras ao mesmo
        // tempo, e `Cruza` é estritamente menor que a duração.
        Assert.Equal(TimeSpan.FromMinutes(Duracao), distancia);
    }

    // ── O torneio de um clube só não muda ─────────────────────────────────────────────────

    // A rede de segurança de tudo isto: a opção nova não pode ter mexido em nada pra quem não a
    // usa — que são TODOS os torneios do Padelizou até hoje.
    [Fact]
    public void Sem_sedes_a_grade_sai_exatamente_igual()
    {
        var inicio = new DateTime(2026, 9, 5, 8, 0, 0);

        List<Partida> Doze() => Enumerable.Range(0, 12)
            .Select(i => Jogo(i % 3 + 1, 100 + i * 2, 101 + i * 2))
            .ToList();

        var semParametro = Doze();
        var comMapaVazio = Doze();

        GradeDeJogos.Encaixar(semParametro, Vagas(inicio, 12, 4), Duracao, quadras: TodasAsQuadras);
        GradeDeJogos.Encaixar(comMapaVazio, Vagas(inicio, 12, 4), Duracao, quadras: TodasAsQuadras,
            sedes: SedesDoTorneio.Nenhuma);

        Assert.Equal(
            semParametro.Select(j => (j.HorarioPrevisto, j.NomeQuadra)),
            comMapaVazio.Select(j => (j.HorarioPrevisto, j.NomeQuadra)));
    }

    // ── A etiqueta das telas (Services/LugarDoJogo) ───────────────────────────────────────

    [Fact]
    public void Com_um_clube_so_a_etiqueta_e_so_a_quadra()
    {
        Assert.Equal("Central", LugarDoJogo.Etiqueta(SedesDoTorneio.Nenhuma, "Central"));
        Assert.Equal("Central", LugarDoJogo.Etiqueta(null, "Central"));
    }

    [Fact]
    public void Com_dois_clubes_a_etiqueta_diz_o_predio()
    {
        Assert.Equal("Batata · Central Batata",
            LugarDoJogo.Etiqueta(DoisClubes(), "Central Batata"));
    }

    // `Partida.NomeQuadra` é texto solto e a Mesa de Controle escreve nele à mão. Nome que não
    // está no cadastro NÃO recebe clube chutado: é ele que decide pra que prédio a pessoa dirige.
    [Fact]
    public void Quadra_que_nao_esta_no_cadastro_sai_sem_clube()
    {
        Assert.Equal("Quadra do vizinho", LugarDoJogo.Etiqueta(DoisClubes(), "Quadra do vizinho"));
        Assert.Null(LugarDoJogo.Etiqueta(DoisClubes(), "   "));
    }

    [Fact]
    public void No_calendario_e_no_aviso_o_separador_e_texto_comum()
    {
        // O "·" some ou vira caixinha no LOCATION do .ics e em app de e-mail antigo.
        Assert.Equal("Central Batata — Batata",
            LugarDoJogo.EmTextoCorrido(DoisClubes(), "Central Batata"));
    }

    // ── A régua dos formulários ───────────────────────────────────────────────────────────

    // Recusar isto é o ponto: a GRADE sobreviveria (trata como "sem sede" e marca em qualquer
    // lugar), e é justamente por isso que a tela precisa barrar — o organizador escolheria o
    // clube, veria salvar, e o torneio faria o contrário sem dizer nada.
    [Fact]
    public void Categoria_num_clube_sem_quadra_e_recusada_na_tela()
    {
        var motivo = SedesDoTorneio.MotivoParaNaoSalvar(
            clubeDeCadaQuadra: new int?[] { null, Nata, Batata },
            clubeDeCadaCategoria: new[] { Nata, 99 });

        Assert.NotNull(motivo);
        Assert.Contains("não tem nenhuma quadra", motivo);
    }

    [Fact]
    public void Torneio_de_uma_sede_so_passa_sem_conferencia()
    {
        Assert.Null(SedesDoTorneio.MotivoParaNaoSalvar(
            clubeDeCadaQuadra: new int?[] { null, null },
            clubeDeCadaCategoria: Array.Empty<int>()));
    }

    [Fact]
    public void Clube_que_nao_esta_entre_as_sedes_do_torneio_e_ignorado()
    {
        var permitidos = new HashSet<int> { Nata, Batata };

        Assert.Null(SedesDoTorneio.ClubeDaQuadraNaPosicao(new[] { "999" }, 0, permitidos));
        Assert.Equal(Batata, SedesDoTorneio.ClubeDaQuadraNaPosicao(new[] { "20" }, 0, permitidos));
        // Vazio = clube principal do torneio.
        Assert.Null(SedesDoTorneio.ClubeDaQuadraNaPosicao(new[] { "" }, 0, permitidos));
        // Posição além do que o formulário mandou.
        Assert.Null(SedesDoTorneio.ClubeDaQuadraNaPosicao(new[] { "20" }, 7, permitidos));
    }

    [Fact]
    public void O_clube_de_cada_categoria_vem_dos_pares_do_formulario()
    {
        var lido = SedesDoTorneio.LerClubePorCategoria(
            new[] { "1:10", "2:20", "3:999", "lixo", "4:" },
            new HashSet<int> { Nata, Batata });

        Assert.Equal(new Dictionary<int, int> { [1] = Nata, [2] = Batata }, lido);
    }

    // ── O nome da quadra continua único POR TORNEIO ───────────────────────────────────────

    // A tentação com sede é deixar "Quadra 1" existir nos dois clubes. Não pode: o motor
    // identifica quadra por NOME (`Partida.NomeQuadra` é texto solto, sem FK), então as duas
    // seriam a MESMA quadra e a grade marcaria um jogo em cada clube achando que dobrou.
    [Fact]
    public void Duas_quadras_de_mesmo_nome_continuam_recusadas_com_dois_clubes()
    {
        var motivo = NomeDeQuadraUnico.MotivoParaNaoSalvar(
            new[] { "Quadra 1", "Quadra 1" }, maisDeUmClube: true);

        Assert.NotNull(motivo);
        Assert.Contains("ponha o clube no nome", motivo);
    }

    [Fact]
    public void Sem_dois_clubes_a_mensagem_nao_fala_de_clube()
    {
        var motivo = NomeDeQuadraUnico.MotivoParaNaoSalvar(new[] { "Quadra 1", "quadra 1 " });

        Assert.NotNull(motivo);
        Assert.DoesNotContain("clube", motivo);
    }

    // ── A criação do torneio, do formulário até o banco ───────────────────────────────────

    // O caminho inteiro: o que o navegador manda chega no controller, casa POSIÇÃO com POSIÇÃO
    // e vira quadra com clube e categoria com clube no banco. É o teste que pega o
    // desencontro entre `nomesQuadras[i]` e `clubesQuadras[i]` — que não daria erro nenhum, só
    // poria a quadra no prédio errado.
    [Fact]
    public async Task Criar_torneio_em_dois_clubes_grava_a_sede_de_cada_quadra_e_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Nata" });
        ctx.Clubes.Add(new Clube { Id = 2, Nome = "Batata" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 4, Nome = "4ª Categoria Masculina", Codigo = "4CatM", Tipo = "Masculina",
        });
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "Copa de Duas Sedes",
            ClubeId = 1,                 // o Nata é a sede principal
            Status = "Inscrições Abertas",
            SetsFaseGrupos = 1,
            GamesFaseGrupos = 6,
            RestricaoCategoria = "Livre",
            FormaPagamento = "Externo",
            QuantidadeQuadras = 3,
            MinutosParaTrocarDeClube = 45,
        };

        var resultado = await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(torneio, new[] { 3, 4 }, null,
                nomesQuadras: new[] { "Central Nata", "Quadra 2 Nata", "Central Batata" },
                capa: null, limiteCategoria: null,
                clubesDoTorneio: new[] { 2 },
                // Posição a posição, junto com `nomesQuadras`: vazio = sede principal.
                clubesQuadras: new[] { "", "", "2" },
                // "categoriaDoCatálogo:clube" — a 3ª no Nata, a 4ª no Batata.
                clubesCategorias: new[] { "3:1", "4:2" });

        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(resultado);

        var criado = await ctx.Torneios.FirstAsync();
        Assert.Equal(45, criado.MinutosParaTrocarDeClube);

        var quadras = await ctx.Quadras.Where(q => q.TorneioId == criado.Id)
            .OrderBy(q => q.Id).ToListAsync();

        Assert.Equal(new[] { "Central Nata", "Quadra 2 Nata", "Central Batata" },
            quadras.Select(q => q.Nome));
        // As duas primeiras sem clube dito = clube do torneio; a terceira no Batata.
        Assert.Equal(new int?[] { null, null, 2 }, quadras.Select(q => q.ClubeId));

        var categorias = await ctx.Categorias.Where(c => c.TorneioId == criado.Id)
            .OrderBy(c => c.Nome).ToListAsync();

        Assert.Equal(1, categorias.Single(c => c.Nome.StartsWith("3ª")).ClubeId);
        Assert.Equal(2, categorias.Single(c => c.Nome.StartsWith("4ª")).ClubeId);
    }

    // A régua da tela, no caminho de verdade: categoria num clube que não recebeu quadra é
    // RECUSADA antes de gravar. Sem isto o organizador veria a tela salvar e o torneio ignorar
    // a escolha dele em silêncio.
    [Fact]
    public async Task Criar_com_categoria_num_clube_sem_quadra_e_recusado_antes_de_gravar()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Nata" });
        ctx.Clubes.Add(new Clube { Id = 2, Nome = "Batata" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "Copa Torta",
            ClubeId = 1,
            Status = "Inscrições Abertas",
            SetsFaseGrupos = 1,
            GamesFaseGrupos = 6,
            RestricaoCategoria = "Livre",
            FormaPagamento = "Externo",
            QuantidadeQuadras = 2,
        };

        var resultado = await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(torneio, new[] { 3 }, null,
                nomesQuadras: new[] { "Central Nata", "Quadra 2 Nata" },
                capa: null, limiteCategoria: null,
                clubesDoTorneio: new[] { 2 },
                clubesQuadras: new[] { "", "" },      // NENHUMA quadra no Batata
                clubesCategorias: new[] { "3:2" });   // mas a categoria foi pro Batata

        var view = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado);
        Assert.Contains("não tem nenhuma quadra", (string)view.ViewData["Erro"]!);
        Assert.Empty(ctx.Torneios);
    }

    // ── A migration ───────────────────────────────────────────────────────────────────────

    // ⚠️ TESTE DE ARQUIVO-FONTE, pelo mesmo motivo do que guarda a constraint de nome de quadra:
    // o EF gerou `defaultValue: 0` aqui, e 0 DESLIGA a folga. O padrão em C# só vale pra objeto
    // novo — um torneio antigo editado pra ter duas sedes nasceria sem folga nenhuma, e a falha
    // seria muda. Nenhum teste de comportamento pega isso, porque o defeito mora no DDL.
    [Fact]
    public void A_migration_escreve_o_padrao_de_30_minutos_a_mao()
    {
        var arquivo = CaminhoDaMigration();
        var texto = File.ReadAllText(arquivo);

        Assert.Contains("MinutosParaTrocarDeClube", texto);
        Assert.Contains("defaultValue: 30", texto);
        Assert.DoesNotContain("defaultValue: 0", texto);
    }

    // As duas colunas de clube precisam ser ANULÁVEIS: `int` liso nasceria ZERO no banco, que
    // não é clube nenhum — e aí toda quadra e toda categoria que já existem apontariam pra um
    // clube inexistente.
    [Fact]
    public void As_colunas_de_clube_nascem_anulaveis()
    {
        var texto = File.ReadAllText(CaminhoDaMigration());

        // Só o regex, e de propósito: uma conferência frouxa por "defaultValue: 0" aqui dispara
        // pela folga de minutos e faz este teste mentir sobre o que quebrou.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(
            texto, @"name: ""ClubeId"",\s*\r?\n\s*table: ""(Quadra|Categoria)"",\s*\r?\n\s*type: ""integer"",\s*\r?\n\s*nullable: true").Count);
    }

    private static string CaminhoDaMigration()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta != null && !Directory.Exists(Path.Combine(pasta.FullName, "Padelizou", "Migrations")))
            pasta = pasta.Parent;

        Assert.NotNull(pasta);

        var achados = Directory.GetFiles(Path.Combine(pasta!.FullName, "Padelizou", "Migrations"),
            "*_MaisDeUmClubeNoTorneio.cs");

        Assert.Single(achados);
        return achados[0];
    }
}
