using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Models;

namespace Padelizou.Tests;

// A FINAL PODE TER REGRA PRÓPRIA (Felipe, 12/09/2026): *"a final tem q ser separada da
// semi ou tem algum modo que a final é separada?"* — não tinha. `SetsFaseFinal`/
// `GamesFaseFinal`/`PontosTieBreakFinal` regem SEMIFINAL E FINAL juntas desde sempre, e é
// isso que continua valendo por padrão. O que nasceu é o desvio: três colunas novas que,
// quando preenchidas, valem só para a partida "Final".
//
// ⚠️ QUEM É O INTERRUPTOR: o `GamesSoDaFinal`. Zero = "não configurado" (a mesma leitura
// que `FormatoDaPartida.Valido` já faz das colunas antigas), e aí a final segue as semis.
// Não existe um `bool` pra isso de propósito — um interruptor separado poderia discordar do
// número gravado, e aí passariam a existir duas verdades sobre o mesmo jogo.
public class FinalComRegraPropriaTests
{
    // O torneio do pedido: tudo até 6, e a final em 3 sets de 9 com super tie-break.
    private static Torneio TorneioComFinalSeparada() => new()
    {
        Nome = "Interno", Codigo = "F1",
        SetsFaseGrupos = 1, GamesFaseGrupos = 4, PontosTieBreakGrupos = TieBreakDoJogo.PontosPadrao,
        SetsFaseMataMata = 1, GamesFaseMataMata = 6, PontosTieBreakMataMata = TieBreakDoJogo.PontosPadrao,
        SetsFaseFinal = 1, GamesFaseFinal = 6, PontosTieBreakFinal = TieBreakDoJogo.PontosPadrao,
        SetsSoDaFinal = 3, GamesSoDaFinal = 9, PontosTieBreakSoDaFinal = TieBreakDoJogo.SuperTieBreak,
    };

    [Fact]
    public void A_final_com_regra_propria_NAO_arrasta_a_semifinal()
    {
        var torneio = TorneioComFinalSeparada();

        var semi = FormatoDaPartida.De(torneio, "Semifinal");
        var final = FormatoDaPartida.De(torneio, "Final");

        Assert.Equal(6, semi.Games);
        Assert.Equal(1, semi.Sets);
        Assert.Equal(9, final.Games);
        Assert.Equal(3, final.Sets);
    }

    [Fact]
    public void Sem_regra_propria_a_final_continua_seguindo_as_semis()
    {
        // A REGRESSÃO QUE IMPORTA: é como TODO torneio que já existe está gravado (zero nas
        // colunas novas). Se este teste cair, a coluna nova mudou torneio de gente.
        var torneio = TorneioComFinalSeparada();
        torneio.SetsSoDaFinal = 0;
        torneio.GamesSoDaFinal = 0;
        torneio.PontosTieBreakSoDaFinal = 0;

        Assert.Equal(FormatoDaPartida.De(torneio, "Semifinal").Games,
                     FormatoDaPartida.De(torneio, "Final").Games);
        Assert.Equal(6, FormatoDaPartida.De(torneio, "Final").Games);
    }

    [Fact]
    public void Torneio_antigo_com_tudo_zerado_nao_muda_de_comportamento()
    {
        // Linha que nunca viu a tela nova: cai no padrão de sempre, e não em 0 games.
        var antigo = new Torneio { Nome = "Antigo", Codigo = "A1" };

        Assert.Equal(FormatoDaPartida.GamesPadrao, FormatoDaPartida.De(antigo, "Final").Games);
        Assert.Equal(FormatoDaPartida.SetsPadrao, FormatoDaPartida.De(antigo, "Final").Sets);
    }

    [Fact]
    public void Zero_no_games_da_final_ignora_os_sets_e_o_tie_break_dela()
    {
        // Linha meio configurada (o games é o interruptor, e ele está desligado): nada do
        // trio vaza. Sem isto, um `SetsSoDaFinal` esquecido de uma edição anterior mudaria a
        // final de um torneio que o organizador acabou de voltar pro modo simples.
        var torneio = TorneioComFinalSeparada();
        torneio.GamesSoDaFinal = 0;

        var final = FormatoDaPartida.De(torneio, "Final");

        Assert.Equal(1, final.Sets);                                  // das semis, não o 3
        Assert.Equal(6, final.Games);
        Assert.Equal(TieBreakDoJogo.PontosPadrao, final.PontosTieBreak);   // 7, não o 10
    }

    [Fact]
    public void Super_tie_break_so_na_final()
    {
        // O caso de quadra que motivou o pedido: 7 nas semis, 10 na decisão.
        var torneio = TorneioComFinalSeparada();

        Assert.Equal(TieBreakDoJogo.PontosPadrao, FormatoDaPartida.De(torneio, "Semifinal").PontosTieBreak);
        Assert.Equal(TieBreakDoJogo.SuperTieBreak, FormatoDaPartida.De(torneio, "Final").PontosTieBreak);
    }

    [Fact]
    public void A_regra_propria_da_final_nao_escapa_para_as_outras_fases()
    {
        var torneio = TorneioComFinalSeparada();

        Assert.Equal(4, FormatoDaPartida.De(torneio, "Grupo A").Games);
        Assert.Equal(6, FormatoDaPartida.De(torneio, "Quartas de Final").Games);
        Assert.Equal(6, FormatoDaPartida.De(torneio, "Oitavas de Final").Games);
        Assert.Equal(6, FormatoDaPartida.De(torneio, "Primeira Rodada").Games);
        Assert.Equal(4, FormatoDaPartida.De(torneio, "Americano - Rodada 1").Games);
    }

    [Fact]
    public void O_alvo_absurdo_do_tie_break_da_final_cai_em_desligado()
    {
        // Mesma régua das outras três fases: POST montado à mão não grava tie-break de -5.
        var torneio = TorneioComFinalSeparada();
        torneio.PontosTieBreakSoDaFinal = -5;

        Assert.Equal(TieBreakDoJogo.Desligado, FormatoDaPartida.De(torneio, "Final").PontosTieBreak);
    }

    // ─────────────────────────── A TELA DE GESTÃO ───────────────────────────

    private static Task<Microsoft.AspNetCore.Mvc.IActionResult> EditarAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId,
        bool? finalSeparada = null, int? sets = null, int? games = null, int? tieBreak = null) =>
        TestInfra.NovoTorneiosController(ctx, organizadorId).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null,
            dataInicio: torneio.DataInicio, precoInscricao: torneio.PrecoInscricao,
            clubeId: torneio.ClubeId, quantidadeQuadras: 1, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: torneio.RestricaoCategoria, capa: null,
            tempoPrevistoPartidaMinutos: 50,
            finalSeparada: finalSeparada,
            setsSoDaFinal: sets, gamesSoDaFinal: games, pontosTieBreakSoDaFinal: tieBreak);

    [Fact]
    public async Task Ligar_a_regra_propria_grava_as_tres_colunas()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        await EditarAsync(ctx, torneio, org.Id,
            finalSeparada: true, sets: 3, games: 9, tieBreak: TieBreakDoJogo.SuperTieBreak);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(3, salvo!.SetsSoDaFinal);
        Assert.Equal(9, salvo.GamesSoDaFinal);
        Assert.Equal(TieBreakDoJogo.SuperTieBreak, salvo.PontosTieBreakSoDaFinal);
    }

    [Fact]
    public async Task Desmarcar_a_regra_propria_zera_as_tres_colunas()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.SetsSoDaFinal = 3;
        torneio.GamesSoDaFinal = 9;
        torneio.PontosTieBreakSoDaFinal = TieBreakDoJogo.SuperTieBreak;
        await ctx.SaveChangesAsync();

        await EditarAsync(ctx, torneio, org.Id, finalSeparada: false);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(0, salvo!.SetsSoDaFinal);
        Assert.Equal(0, salvo.GamesSoDaFinal);
        Assert.Equal(0, salvo.PontosTieBreakSoDaFinal);
    }

    [Fact]
    public async Task Aba_antiga_sem_o_campo_NAO_apaga_a_regra_da_final()
    {
        // ⚠️ O motivo de o parâmetro ser `bool?` e não `bool`: caixa desmarcada não vai no
        // POST, então um `bool` normal não distingue "o organizador desmarcou" de "esta aba
        // foi aberta antes do deploy". O segundo caso apagaria a configuração da final de
        // quem só queria trocar o nome do torneio, calado.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.SetsSoDaFinal = 3;
        torneio.GamesSoDaFinal = 9;
        torneio.PontosTieBreakSoDaFinal = TieBreakDoJogo.SuperTieBreak;
        await ctx.SaveChangesAsync();

        await EditarAsync(ctx, torneio, org.Id, finalSeparada: null);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(3, salvo!.SetsSoDaFinal);
        Assert.Equal(9, salvo.GamesSoDaFinal);
        Assert.Equal(TieBreakDoJogo.SuperTieBreak, salvo.PontosTieBreakSoDaFinal);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(3, 0)]
    [InlineData(3, -1)]
    public async Task Sets_ou_games_zerados_na_final_sao_recusados(int sets, int games)
    {
        // Mesma recusa das outras fases: com zero games a Mesa não deixaria marcar nem um
        // ponto, e o organizador só descobriria com a quadra ocupada.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        await EditarAsync(ctx, torneio, org.Id, finalSeparada: true, sets: sets, games: games);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(0, salvo!.GamesSoDaFinal);
        Assert.Equal(0, salvo.SetsSoDaFinal);
    }

    // ─────────────────────────── CRIAÇÃO E CÓPIA ───────────────────────────

    [Fact]
    public async Task Formato_unico_na_criacao_zera_a_regra_propria_da_final()
    {
        // "Todas as partidas com a mesma regra" inclui a final: deixá-la de fora daria um
        // torneio com final de 3 sets que ninguém pediu.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });
        await ctx.SaveChangesAsync();

        var novo = new Torneio
        {
            Nome = "Interno do clube", ClubeId = 1, Status = "Inscrições Abertas",
            FormatoUnico = true,
            SetsFaseGrupos = 1, GamesFaseGrupos = 6,
            SetsSoDaFinal = 3, GamesSoDaFinal = 9,
            PontosTieBreakSoDaFinal = TieBreakDoJogo.SuperTieBreak,
            RestricaoCategoria = "Livre", FormaPagamento = "Externo",
        };

        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(novo, new[] { 3 }, null, null, null, null);

        var criado = Assert.Single(ctx.Torneios);
        Assert.Equal(0, criado.GamesSoDaFinal);
        Assert.Equal(0, criado.SetsSoDaFinal);
        Assert.Equal(0, criado.PontosTieBreakSoDaFinal);
        Assert.Equal(6, FormatoDaPartida.De(criado, "Final").Games);
    }

    [Fact]
    public async Task Criar_sem_marcar_a_caixa_NAO_liga_a_regra_propria()
    {
        // ⚠️ A caixa esconde os campos, mas esconder NÃO é não enviar: o `<div hidden>` manda
        // o valor padrão do input igual. Sem a caixa chegando no POST, todo torneio novo
        // nasceria com uma final de 9 games que ninguém pediu — inclusive os de formato único.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });
        await ctx.SaveChangesAsync();

        var novo = new Torneio
        {
            Nome = "Interno do clube", ClubeId = 1, Status = "Inscrições Abertas",
            SetsFaseGrupos = 1, GamesFaseGrupos = 6,
            SetsFaseMataMata = 1, GamesFaseMataMata = 6,
            SetsFaseFinal = 1, GamesFaseFinal = 6,
            // O que o formulário manda com a caixa DESMARCADA: os campos escondidos, com o
            // valor padrão deles.
            SetsSoDaFinal = 3, GamesSoDaFinal = 9,
            RestricaoCategoria = "Livre", FormaPagamento = "Externo",
        };

        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(novo, new[] { 3 }, null, null, null, null, finalSeparada: false);

        var criado = Assert.Single(ctx.Torneios);
        Assert.Equal(0, criado.GamesSoDaFinal);
        Assert.Equal(6, FormatoDaPartida.De(criado, "Final").Games);
    }

    [Fact]
    public async Task Criar_marcando_a_caixa_grava_a_regra_propria()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });
        await ctx.SaveChangesAsync();

        var novo = new Torneio
        {
            Nome = "Interno do clube", ClubeId = 1, Status = "Inscrições Abertas",
            SetsFaseGrupos = 1, GamesFaseGrupos = 6,
            SetsFaseMataMata = 1, GamesFaseMataMata = 6,
            SetsFaseFinal = 1, GamesFaseFinal = 6,
            SetsSoDaFinal = 3, GamesSoDaFinal = 9,
            PontosTieBreakSoDaFinal = TieBreakDoJogo.SuperTieBreak,
            RestricaoCategoria = "Livre", FormaPagamento = "Externo",
        };

        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(novo, new[] { 3 }, null, null, null, null, finalSeparada: true);

        var criado = Assert.Single(ctx.Torneios);
        Assert.Equal(9, FormatoDaPartida.De(criado, "Final").Games);
        Assert.Equal(3, FormatoDaPartida.De(criado, "Final").Sets);
        Assert.Equal(6, FormatoDaPartida.De(criado, "Semifinal").Games);
    }

    // ─────────────────────────── A ORDEM DO PAR CAIXA+HIDDEN ───────────────────────────

    [Fact]
    public void Na_gestao_o_hidden_da_caixa_vem_DEPOIS_dela()
    {
        // 🕳️ DEFEITO COMETIDO E CORRIGIDO EM 12/09/2026, na primeira escrita desta tela: o
        // `<input type="hidden" value="false">` foi posto ANTES da caixa. Desmarcar funcionava,
        // mas MARCAR não — chegam os dois valores no POST e o binder fica com o PRIMEIRO, então
        // ligar a regra própria da final gravava `false` e não fazia nada, calado.
        //
        // O par tem que estar na ordem do `avisarJogadores` e do `permiteMultiplasCategorias`,
        // que é a ordem que o próprio ASP.NET gera: caixa primeiro, hidden depois.
        var html = Details();

        int caixa = html.IndexOf("name=\"finalSeparada\" id=\"pdzFinalSeparada\"", StringComparison.Ordinal);
        int oculto = html.IndexOf("<input type=\"hidden\" name=\"finalSeparada\" value=\"false\" />",
                                  StringComparison.Ordinal);

        Assert.True(caixa >= 0, "não achei a caixa \"a final tem regra própria\" na tela de gestão");
        Assert.True(oculto >= 0, "sem o hidden pareado, DESMARCAR a caixa não chega no servidor");
        Assert.True(caixa < oculto,
            "o hidden \"false\" está ANTES da caixa: o binder fica com o primeiro valor, "
            + "e marcar a caixa não ligaria a regra própria da final");
    }

    [Fact]
    public void Na_criacao_a_caixa_NAO_leva_hidden_pareado()
    {
        // O contraste com a tela de gestão, e é de propósito: na criação o parâmetro é `bool`
        // com padrão `false` (o torneio está nascendo, não existe dado gravado a preservar), e
        // aí o silêncio da caixa desmarcada já é a resposta certa. Um hidden aqui só
        // acrescentaria uma peça a manter em ordem.
        var html = Create();

        Assert.Contains("name=\"finalSeparada\" value=\"true\"", html);
        Assert.DoesNotContain("<input type=\"hidden\" name=\"finalSeparada\"", html);
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string Create() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Create.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }

    [Fact]
    public void A_regra_propria_da_final_viaja_na_copia_do_torneio()
    {
        // É formato, como sets e games das outras fases: a 2ª edição do mesmo torneio joga a
        // final com a mesma regra.
        Assert.Contains(nameof(Torneio.SetsSoDaFinal), DuplicacaoDeTorneio.Copiadas);
        Assert.Contains(nameof(Torneio.GamesSoDaFinal), DuplicacaoDeTorneio.Copiadas);
        Assert.Contains(nameof(Torneio.PontosTieBreakSoDaFinal), DuplicacaoDeTorneio.Copiadas);
    }
}
