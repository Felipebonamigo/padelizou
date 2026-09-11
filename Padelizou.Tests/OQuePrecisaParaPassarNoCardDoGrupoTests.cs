using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — "O QUE CADA UM PRECISA PARA PASSAR" NO CARD DO GRUPO.
//
// 🗣️ Felipe, num print do Grupo B do 2ª Etapa ER Padel Tour: *"quando chegar nessa parte, que o
// grupo de 3, ja tiveram 2 jogos e falta um, exiba botão 'O que cada um precisa para passar' e
// nele abre um pop up, explicando qual placar cada um precisa fazer para passar, por que as
// vezes cada dupla ganha um jogo ou enfim, e fica a duvida de quantos games precisa fazer para
// passar de fase"*.
//
// ⚠️ O MOTOR JÁ EXISTIA (`OQuePrecisaParaClassificar`, 13/08/2026, do mesmo pedido) — só que
// vivia na tela `/Torneios/Classificacao`, e não no card do grupo, que é onde o print foi
// tirado. Então isto NÃO refaz a conta: leva o painel pra página do torneio e acrescenta o que
// faltava pro pedido ser atendido de verdade — a LINHA DE CADA DUPLA dizendo, direto, qual
// placar ela precisa fazer. A tabela de cenários responde isso de lado; a pergunta do Felipe é
// de frente.
//
// ⚠️ E continua valendo a regra que criou o serviço: nenhuma régua nova. A frase de cada dupla
// sai dos MESMOS blocos de cenário que o `ClassificacaoDeGrupos` (a régua única, a que monta a
// chave) já decidiu — aqui não há nenhum `>` comparando vitória com vitória.
public class OQuePrecisaParaPassarNoCardDoGrupoTests
{
    private static readonly FormatoDaPartida.Formato Ate9 = new(1, 9);

    private static Dupla Dupla(int id, string nome) => new()
    {
        Id = id,
        Grupo = "B",
        Jogador1 = new Jogador { Nome = nome, Cpf = $"9990000000{id}", Login = $"j{id}" },
    };

    private static Partida Jogo(int dupla1, int dupla2, int? g1, int? g2) => new()
    {
        Dupla1Id = dupla1, Dupla2Id = dupla2,
        GamesDupla1 = g1, GamesDupla2 = g2,
        Fase = "Grupo B",
    };

    // ═══════════════ O GRUPO B DO PRINT, NÚMERO POR NÚMERO ═══════════════
    //
    // Marcelo/Enio  J1 V1 D0 +3   (venceu Eder/Augusto 9x6)
    // Eder/Augusto  J2 V1 D1 +2   (venceu Paulo/Arthur 9x4, perdeu 6x9)
    // Paulo/Arthur  J1 V0 D1 −5   (perdeu 4x9)
    // Falta:        Marcelo/Enio x Paulo/Arthur
    //
    // É o caso exato que o Felipe descreve — "as vezes cada dupla ganha um jogo" — e a resposta
    // NÃO é óbvia: o 3º critério (games a favor) decide o empate de saldo em −1 que aparece
    // quando o Paulo vence por 4.
    private static (Dupla[] Duplas, Partida[] Jogos) GrupoBDoPrint()
    {
        var duplas = new[] { Dupla(1, "Marcelo"), Dupla(2, "Eder"), Dupla(3, "Paulo") };
        var jogos = new[]
        {
            Jogo(2, 3, 9, 4),      // Eder/Augusto 9 x 4 Paulo/Arthur
            Jogo(1, 2, 9, 6),      // Marcelo/Enio 9 x 6 Eder/Augusto
            Jogo(1, 3, null, null) // o que falta
        };
        return (duplas, jogos);
    }

    private static OQuePrecisaParaClassificar.Situacao Da(
        OQuePrecisaParaClassificar.Quadro quadro, int duplaId) =>
        quadro.Situacoes.Single(s => s.Dupla.Id == duplaId);

    [Fact]
    public void Quem_ja_esta_dentro_e_dito_com_todas_as_letras()
    {
        var (duplas, jogos) = GrupoBDoPrint();

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9)!;

        // Eder/Augusto não joga o último jogo e passa em QUALQUER resultado dele: pro Eder ficar
        // em 3º os dois outros precisariam terminar acima de +2, e o jogo que falta dá no máximo
        // uma vitória pra cada um deles.
        Assert.Equal(OQuePrecisaParaClassificar.Estado.JaClassificado, Da(quadro, 2).Estado);
    }

    [Fact]
    public void A_dupla_que_esta_atras_ouve_de_quantos_games_precisa()
    {
        var (duplas, jogos) = GrupoBDoPrint();

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9)!;

        // ⚠️ 5, e não 4: vencendo por 4 (9x5) o Paulo empata o saldo com o Marcelo em −1, e o
        // desempate seguinte — games a favor — fica com o Marcelo, 14 a 13. É EXATAMENTE a
        // conta que ninguém faz de cabeça na beira da quadra, e a razão do pedido.
        Assert.Equal(OQuePrecisaParaClassificar.Estado.Depende, Da(quadro, 3).Estado);
        Assert.Equal("Passa vencendo por 5 games ou mais.", Da(quadro, 3).Frase);
    }

    [Fact]
    public void Quem_lidera_ouve_ate_quanto_pode_perder()
    {
        var (duplas, jogos) = GrupoBDoPrint();

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9)!;

        // Vencer basta; e mesmo perdendo ele passa, desde que a derrota seja por até 4 games.
        Assert.Equal(OQuePrecisaParaClassificar.Estado.Depende, Da(quadro, 1).Estado);
        Assert.Equal("Passa vencendo ou se Paulo vencer por até 4 games.", Da(quadro, 1).Frase);

        // O nome que vai pra coluna "Classificam" da tabela sai daqui, e é o CURTO: a regra
        // vivia copiada na view da Classificação, onde podia divergir da frase ao lado.
        Assert.Equal("Marcelo", Da(quadro, 1).NomeCurto);
    }

    [Fact]
    public void Toda_dupla_do_grupo_ganha_uma_linha()
    {
        var (duplas, jogos) = GrupoBDoPrint();

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9)!;

        // A tela lista as duplas a partir daqui — faltando uma, ela some do pop-up sem erro
        // nenhum, e o painel passa a responder por um grupo que não é o da tela.
        Assert.Equal(new[] { 1, 2, 3 }, quadro.Situacoes.Select(s => s.Dupla.Id).OrderBy(x => x));
    }

    // ═══════════════ OS OUTROS DOIS ESTADOS ═══════════════

    [Fact]
    public void Quem_nao_tem_mais_chance_ouve_isso_e_nao_um_placar()
    {
        // A venceu os dois; B e C perderam pra ela. Falta B x C, e passam 2: quem vencer pega a
        // vaga. Com passam=1 ninguém além da A tem chance — e prometer placar a quem não pode
        // mais passar é a pior coisa que este painel poderia fazer.
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), Jogo(2, 3, null, null) };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 1, Ate9)!;

        Assert.Equal(OQuePrecisaParaClassificar.Estado.JaClassificado, Da(quadro, 1).Estado);
        Assert.Equal(OQuePrecisaParaClassificar.Estado.SemChance, Da(quadro, 2).Estado);
        Assert.Equal(OQuePrecisaParaClassificar.Estado.SemChance, Da(quadro, 3).Estado);
    }

    [Fact]
    public void Quem_nao_joga_o_ultimo_jogo_ouve_o_nome_de_quem_decide_por_ela()
    {
        // C perdeu os dois e está fora; falta A x B, e passa 1. A dupla que não joga não tem
        // "vencendo" nenhum a fazer — a frase dela precisa falar do jogo dos outros.
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 3, 9, 2), Jogo(2, 3, 9, 1), Jogo(1, 2, null, null) };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 1, Ate9)!;

        Assert.Equal("Passa vencendo.", Da(quadro, 1).Frase);
        Assert.Equal("Passa vencendo.", Da(quadro, 2).Frase);
        Assert.Equal(OQuePrecisaParaClassificar.Estado.SemChance, Da(quadro, 3).Estado);
    }

    // ═══════════════ A TELA ═══════════════

    [Fact]
    public void O_card_do_grupo_ganhou_o_botao_que_abre_o_pop_up()
    {
        var fonte = TestInfra.SemComentarios(Details());

        Assert.Contains("O que cada um precisa para passar", fonte);
        Assert.Contains("data-bs-toggle=\"modal\"", fonte);
        Assert.Contains("_OQuePrecisaParaClassificar", fonte);
    }

    // ⚠️ O MODAL NÃO PODE NASCER DENTRO DO CARD. `site.css` dá `transform: translateY(-5px)` no
    // hover de `.card.h-100` — e o card do grupo é exatamente isso. Ancestral com `transform`
    // vira bloco de contenção pro `position: fixed`: o pop-up ficaria preso ao card, do tamanho
    // dele, no instante em que o dedo/mouse encostasse. Ele vai FORA, irmão do card.
    [Fact]
    public void O_modal_do_grupo_fica_fora_do_card()
    {
        var fonte = TestInfra.SemComentarios(Details());

        // São DOIS laços sobre os grupos da categoria, e é essa separação que põe o modal fora
        // do card: o primeiro desenha os cards (com o botão), o segundo desenha os pop-ups,
        // depois do </div> da .row. Junta-os num laço só e o modal volta pra dentro do card.
        var lacos = Regex.Matches(fonte, @"foreach \(var grupo in categoria\.GruposTorneio")
            .Select(m => m.Index).ToList();
        Assert.Equal(2, lacos.Count);

        var card = fonte.IndexOf("pdz-grupo-jogos", StringComparison.Ordinal);
        var modal = fonte.IndexOf("class=\"modal fade\" id=\"oQuePrecisa-", StringComparison.Ordinal);
        Assert.True(card > lacos[0] && card < lacos[1], "o card do grupo saiu do primeiro laço");
        Assert.True(modal > lacos[1], "o modal do grupo nasceu dentro do card");
    }

    [Fact]
    public void A_tela_de_classificacao_passou_a_usar_a_MESMA_parcial()
    {
        // Duas cópias do mesmo painel são duas verdades sobre a régua: a segunda envelhece
        // calada no dia em que o desempate mudar. Depois desta troca, a tabela de cenários mora
        // num arquivo só.
        var classificacao = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Classificacao.cshtml")));

        Assert.Contains("_OQuePrecisaParaClassificar", classificacao);
        Assert.DoesNotContain("Se acontecer", classificacao);
    }

    [Fact]
    public void A_parcial_desenha_a_linha_de_cada_dupla_e_a_tabela_de_cenarios()
    {
        var parcial = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_OQuePrecisaParaClassificar.cshtml")));

        Assert.Contains("Model.Situacoes", parcial);
        Assert.Contains("Model.Cenarios", parcial);

        // ⚠️ NOME CURTO na coluna "Classificam", e nome inteiro só na lista de cima. Cada célula
        // dali lista DUAS duplas ao lado de uma frase que já usa o nome curto; com o nome
        // inteiro, cada linha da tabela ocupava três alturas no celular (visto no Chromium a
        // 430px). E o nome curto sai do SERVIÇO — a regra vivia copiada na view.
        Assert.Contains("s!.NomeCurto", parcial);
        // A régua escrita na tela evita a pergunta "e se o sistema calcular diferente na hora?".
        Assert.Contains("saldo de games", parcial);
    }

    // ═══════════════ O CONTROLLER ENTREGA O QUADRO ═══════════════

    [Fact]
    public async Task O_Details_entrega_o_quadro_do_grupo_em_que_falta_UM_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, grupoCheio, grupoIntocado, _, org) = GrupoDeTresNaReta(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        var quadros = Assert.IsType<Dictionary<int, OQuePrecisaParaClassificar.Quadro>>(
            controller.ViewBag.OQuePrecisaPorGrupo);

        Assert.True(quadros.ContainsKey(grupoCheio),
            "o grupo em que falta um jogo ficou sem o painel");
        Assert.False(quadros.ContainsKey(grupoIntocado),
            "o grupo com três jogos por fazer não tem o que responder, e ganhou painel mesmo assim");
    }

    // A régua de QUANTOS passam é a do chaveamento (`categoria.ClassificadosPorGrupo ?? 2`, a
    // mesma do AvancoDaChave). Um painel que simule com outro número promete vaga que a chave
    // não vai dar — o defeito exato que o serviço foi escrito pra impedir.
    //
    // ⚠️ A prova é a MUDANÇA de resposta com o número: `torneio.ClassificadosPorGrupo` nasce 2
    // e nenhuma tela o edita, então um painel que lesse o do TORNEIO passaria calado num teste
    // que só olhasse o caso de duas vagas.
    [Theory]
    [InlineData(2, OQuePrecisaParaClassificar.Estado.JaClassificado)]
    [InlineData(1, OQuePrecisaParaClassificar.Estado.Depende)]
    public async Task O_painel_simula_com_o_numero_de_vagas_do_chaveamento(
        int vagas, OQuePrecisaParaClassificar.Estado esperado)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, grupoCheio, _, duplas, org) = GrupoDeTresNaReta(ctx, classificadosPorGrupo: vagas);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        var quadros = (Dictionary<int, OQuePrecisaParaClassificar.Quadro>)controller.ViewBag.OQuePrecisaPorGrupo;
        var quadro = quadros[grupoCheio];

        // Eder/Augusto (o que jogou os dois): com DUAS vagas ele está dentro de qualquer jeito;
        // com UMA, a liderança ainda depende do jogo que falta.
        Assert.Equal(esperado, quadro.Situacoes.Single(s => s.Dupla.Id == duplas[1]).Estado);
    }

    // Grupo B do print, montado no banco: 3 duplas, 2 jogos feitos, 1 por jogar. Mais um grupo
    // inteiro por jogar ao lado, que é o que prova que o painel não aparece fora de hora.
    private static (Torneio torneio, int grupoCheio, int grupoIntocado, List<int> duplas, Jogador org)
        GrupoDeTresNaReta(DbPadelContext ctx, int classificadosPorGrupo = 2)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6, status: "Fase de Grupos");
        torneio.GamesFaseGrupos = 9;
        torneio.SetsFaseGrupos = 1;
        categoria.ClassificadosPorGrupo = classificadosPorGrupo;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();

        var grupoB = new GrupoTorneio { Nome = "Grupo B", CategoriaId = categoria.Id };
        var grupoC = new GrupoTorneio { Nome = "Grupo C", CategoriaId = categoria.Id };
        // Não há `DbSet<GrupoTorneio>`: a entidade entra pelo grafo da Categoria.
        ctx.Add(grupoB);
        ctx.Add(grupoC);
        ctx.SaveChanges();

        for (int i = 0; i < 3; i++) { duplas[i].GrupoTorneioId = grupoB.Id; duplas[i].Grupo = "Grupo B"; }
        for (int i = 3; i < 6; i++) { duplas[i].GrupoTorneioId = grupoC.Id; duplas[i].Grupo = "Grupo C"; }

        void Jogo(Dupla d1, Dupla d2, int? g1, int? g2) => ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            Fase = "Grupo B",
            Status = g1 == null ? "Agendada" : "Finalizada",
            GamesDupla1 = g1,
            GamesDupla2 = g2,
            Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
        });

        Jogo(duplas[1], duplas[2], 9, 4);
        Jogo(duplas[0], duplas[1], 9, 6);
        Jogo(duplas[0], duplas[2], null, null);

        // O Grupo C não jogou nada: três jogos por fazer.
        foreach (var (a, b) in new[] { (3, 4), (3, 5), (4, 5) })
            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id,
                CategoriaId = categoria.Id,
                Dupla1Id = duplas[a].Id,
                Dupla2Id = duplas[b].Id,
                Fase = "Grupo C",
                Status = "Agendada",
                Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
            });

        ctx.SaveChanges();
        return (torneio, grupoB.Id, grupoC.Id, duplas.Select(d => d.Id).ToList(), org);
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
