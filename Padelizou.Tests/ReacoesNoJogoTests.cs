using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — REAGIR COM EMOJI EM CADA JOGO. 🗣️ Felipe: *"aqui, a cada jogo, permita a pessoa
// 'reagir' tipo o que tem aqui no discord, com emojis"* e, na sequência, *"e ao clicar no emoji,
// veja quem colocou o que, igual no whats app"*.
//
// Duas decisões dele, feitas antes de qualquer código (é `architectural` — gera migration):
// **teclado de emoji livre** (qualquer emoji que a pessoa digitar, não uma paleta fechada) e a
// fileira do card **nascendo só com o que já tem** mais um botão que abre o teclado.
//
// ⚠️ A PALETA LIVRE É O QUE MOVE A VALIDAÇÃO PRO SERVIDOR. Com lista fechada, "é emoji?" se
// responde comparando com a lista; sem ela, a coluna aceita o que o POST mandar — e um POST
// montado à mão grava "PAGUE AQUI: bit.ly/..." como reação de um jogo que todo mundo vê. É
// fronteira de confiança, o degrau que a escada do CLAUDE.md nunca encurta.
public class ReacoesNoJogoTests
{
    // ─────────────────── A PENEIRA DO EMOJI (Services/EmojiDeReacao) ───────────────────

    [Theory]
    [InlineData("🔥")]        // um code point só
    [InlineData("👏")]
    [InlineData("😂")]
    [InlineData("🇧🇷")]        // bandeira: DOIS indicadores regionais, um grafema só
    [InlineData("👨‍👩‍👧")]        // sequência com ZWJ
    [InlineData("👍🏽")]        // com tom de pele (modificador)
    [InlineData("1️⃣")]        // keycap: dígito + VS16 + U+20E3
    [InlineData("❤️")]        // emoji de apresentação "texto" que só vira colorido com o VS16
    public void Emoji_de_verdade_passa(string bruto) =>
        Assert.NotNull(EmojiDeReacao.Normalizar(bruto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]                      // letra
    [InlineData("legal")]                  // palavra
    [InlineData("🔥 legal")]                // emoji + texto
    [InlineData("🔥🔥")]                     // DOIS emoji: é um grafema por reação
    [InlineData("🔥👏")]
    [InlineData("7")]                      // dígito solto (sem o keycap não é emoji nenhum)
    [InlineData("+")]
    [InlineData("<script>")]
    [InlineData("PAGUE AQUI bit.ly/x")]    // o motivo de a peneira existir
    public void O_que_nao_e_UM_emoji_e_recusado(string? bruto) =>
        Assert.Null(EmojiDeReacao.Normalizar(bruto));

    // ⚠️ 👍 E 👍️ (com VS16 invisível no fim) SÃO O MESMO EMOJI NA TELA e viriam de teclados
    // diferentes. Sem normalizar, o card mostraria DUAS pílulas idênticas com 1 voto cada, e
    // quem clicasse na "outra" acharia que a sua reação sumiu. A forma gravada é a longa: o VS16
    // é inócuo em quem já é colorido (👍) e é o que faz o ❤ virar ❤️ em vez de um coração preto.
    [Fact]
    public void O_MESMO_emoji_com_e_sem_o_seletor_invisivel_vira_UMA_pilula()
    {
        Assert.Equal(EmojiDeReacao.Normalizar("👍"), EmojiDeReacao.Normalizar("👍️"));
        Assert.Equal(EmojiDeReacao.Normalizar("❤"), EmojiDeReacao.Normalizar("❤️"));
        Assert.EndsWith("️", EmojiDeReacao.Normalizar("👍"));
    }

    [Fact]
    public void Espaco_em_volta_nao_conta()
    {
        Assert.Equal(EmojiDeReacao.Normalizar("🔥"), EmojiDeReacao.Normalizar("  🔥 "));
    }

    // ─────────────────── REAGIR E TIRAR ───────────────────

    [Fact]
    public async Task Reagir_grava_a_minha_reacao_e_conta()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var eu = await NovoTorcedorAsync(ctx, "Eu", "66660000001");

        var resumo = await servico.ReagirAsync(partida.Id, eu.Id, "🔥");

        var pilula = Assert.Single(resumo.Reacoes);
        Assert.Equal(1, pilula.Total);
        Assert.True(pilula.EuReagi);
        Assert.Equal(1, resumo.Total);
        Assert.Single(ctx.ReacoesDaPartida.ToList());
    }

    // ⚠️ IDEMPOTENTE, e a trava de verdade é a CHAVE COMPOSTA (PartidaId, JogadorId, Emoji) do
    // banco — não este `if`. O toque duplo num alvo de 32px manda dois POSTs, e foi assim que o
    // `DbUpdateException em POST /Partidas/Votar` apareceu em produção em 10/09.
    [Fact]
    public async Task Reagir_DUAS_VEZES_no_mesmo_emoji_nao_duplica_nem_estoura()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var eu = await NovoTorcedorAsync(ctx, "Eu", "66660000002");

        await servico.ReagirAsync(partida.Id, eu.Id, "🔥");
        var resumo = await servico.ReagirAsync(partida.Id, eu.Id, "🔥");

        Assert.Equal(1, Assert.Single(resumo.Reacoes).Total);
        Assert.Single(ctx.ReacoesDaPartida.ToList());
    }

    // A diferença com o palpitômetro, e é de propósito: lá o voto é UM (trocar de dupla troca a
    // linha); aqui reagir com 🔥 não tira o 👏, igual ao Discord e ao WhatsApp.
    [Fact]
    public async Task A_mesma_pessoa_pode_por_emoji_DIFERENTES_no_mesmo_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var eu = await NovoTorcedorAsync(ctx, "Eu", "66660000003");

        await servico.ReagirAsync(partida.Id, eu.Id, "🔥");
        var resumo = await servico.ReagirAsync(partida.Id, eu.Id, "👏");

        Assert.Equal(2, resumo.Reacoes.Count);
        Assert.All(resumo.Reacoes, r => Assert.True(r.EuReagi));
        Assert.Equal(2, resumo.Total);
    }

    [Fact]
    public async Task Reagir_com_o_que_nao_e_emoji_e_RECUSADO_e_nao_grava_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var eu = await NovoTorcedorAsync(ctx, "Eu", "66660000004");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ReagirAsync(partida.Id, eu.Id, "PAGUE AQUI bit.ly/x"));

        Assert.Empty(ctx.ReacoesDaPartida.ToList());
    }

    [Fact]
    public async Task Reagir_num_jogo_que_NAO_EXISTE_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var servico = new ReacaoService(ctx);
        var eu = await NovoTorcedorAsync(ctx, "Eu", "66660000005");

        // O `partidaId` vem congelado no HTML, e o jogo não: regerar a chave APAGA partidas
        // (a mesma lição dos três 500 do `VerVotos` em 11/09). Sem esta guarda, a FK estoura
        // como erro do sistema em vez de "este jogo saiu da lista".
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ReagirAsync(9999, eu.Id, "🔥"));
    }

    [Fact]
    public async Task Tirar_apaga_SO_a_minha_reacao_e_SO_aquele_emoji()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var eu = await NovoTorcedorAsync(ctx, "Eu", "66660000006");
        var outro = await NovoTorcedorAsync(ctx, "Outro", "66660000007");

        await servico.ReagirAsync(partida.Id, eu.Id, "🔥");
        await servico.ReagirAsync(partida.Id, eu.Id, "👏");
        await servico.ReagirAsync(partida.Id, outro.Id, "🔥");

        var resumo = await servico.TirarReacaoAsync(partida.Id, eu.Id, "🔥");

        // ⚠️ A CHECAGEM DE DONO É ESTRUTURAL: o serviço acha a linha por (partida, jogador,
        // emoji), e o jogador vem da claim. Não há parâmetro por onde pedir a reação de outro.
        var fogo = resumo.Reacoes.Single(r => r.Emoji == EmojiDeReacao.Normalizar("🔥"));
        Assert.Equal(1, fogo.Total);
        Assert.False(fogo.EuReagi);
        Assert.True(resumo.Reacoes.Single(r => r.Emoji == EmojiDeReacao.Normalizar("👏")).EuReagi);
        Assert.Equal(2, ctx.ReacoesDaPartida.Count());
    }

    [Fact]
    public async Task Tirar_o_que_nao_existe_nao_estoura()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var ninguem = await NovoTorcedorAsync(ctx, "Nunca Reagiu", "66660000008");

        // Mesma lição do "retirar o palpite": o toque duplo manda dois POSTs e o segundo chega
        // com a linha já apagada. Estourar ali daria alerta vermelho a quem conseguiu o que quis.
        var resumo = await servico.TirarReacaoAsync(partida.Id, ninguem.Id, "🔥");

        Assert.Empty(resumo.Reacoes);
        Assert.Equal(0, resumo.Total);
    }

    // ─────────────────── A FILEIRA DO CARD ───────────────────

    [Fact]
    public async Task As_pilulas_vem_da_MAIS_usada_pra_menos()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var a = await NovoTorcedorAsync(ctx, "A", "66660000010");
        var b = await NovoTorcedorAsync(ctx, "B", "66660000011");
        var c = await NovoTorcedorAsync(ctx, "C", "66660000012");

        await servico.ReagirAsync(partida.Id, a.Id, "🔥");   // o 🔥 chega PRIMEIRO, com 1
        await servico.ReagirAsync(partida.Id, b.Id, "👏");
        await servico.ReagirAsync(partida.Id, c.Id, "👏");   // e o 👏 termina com 2

        var resumo = (await servico.ObterResumosAsync(new[] { partida.Id }, null))[partida.Id];

        Assert.Equal(EmojiDeReacao.Normalizar("👏"), resumo.Reacoes[0].Emoji);
        Assert.Equal(2, resumo.Reacoes[0].Total);
        Assert.Equal(EmojiDeReacao.Normalizar("🔥"), resumo.Reacoes[1].Emoji);
    }

    // ⚠️ CARGA EM LOTE, e não jogo a jogo: a lista do torneio tem 97 jogos, e é assim que uma
    // tela vira 97 idas ao banco (é o mesmo desenho do `PalpiteService.ObterResumosAsync`).
    [Fact]
    public async Task O_resumo_sai_em_LOTE_e_cada_jogo_com_o_que_e_dele()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogo1 = await MontarJogoAsync(ctx);
        var jogo2 = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var eu = await NovoTorcedorAsync(ctx, "Eu", "66660000013");

        await servico.ReagirAsync(jogo1.Id, eu.Id, "🔥");

        var resumos = await servico.ObterResumosAsync(new[] { jogo1.Id, jogo2.Id }, eu.Id);

        Assert.Single(resumos[jogo1.Id].Reacoes);
        Assert.Empty(resumos[jogo2.Id].Reacoes);
    }

    // ⚠️ QUEM NÃO ESTÁ LOGADO NÃO É "QUEM NÃO REAGIU": sem isto, `EuReagi` viria de uma
    // comparação com nulo e a pílula nasceria marcada ou não por acidente.
    [Fact]
    public async Task Sem_login_nenhuma_pilula_vem_marcada()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var alguem = await NovoTorcedorAsync(ctx, "Alguém", "66660000014");
        await servico.ReagirAsync(partida.Id, alguem.Id, "🔥");

        var resumo = (await servico.ObterResumosAsync(new[] { partida.Id }, null))[partida.Id];

        Assert.False(Assert.Single(resumo.Reacoes).EuReagi);
    }

    // ─────────────────── QUEM COLOCOU O QUÊ (o segundo pedido) ───────────────────

    [Fact]
    public async Task Quem_reagiu_lista_nome_e_emoji_de_cada_um()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = await MontarJogoAsync(ctx);
        var servico = new ReacaoService(ctx);
        var ana = await NovoTorcedorAsync(ctx, "Ana Dumoncel", "66660000020");
        var bia = await NovoTorcedorAsync(ctx, "Bibiana Ritter", "66660000021");

        await servico.ReagirAsync(partida.Id, ana.Id, "😂");
        await servico.ReagirAsync(partida.Id, bia.Id, "😂");
        await servico.ReagirAsync(partida.Id, ana.Id, "🔥");

        var quem = await servico.ObterQuemReagiuAsync(partida.Id);

        Assert.NotNull(quem);
        Assert.Equal(3, quem!.Linhas.Count);
        // Agrupado por emoji, na mesma ordem das pílulas do card: o 😂 tem 2, o 🔥 tem 1.
        Assert.Equal(EmojiDeReacao.Normalizar("😂"), quem.Linhas[0].Emoji);
        Assert.Equal(EmojiDeReacao.Normalizar("😂"), quem.Linhas[1].Emoji);
        Assert.Equal(EmojiDeReacao.Normalizar("🔥"), quem.Linhas[2].Emoji);
        Assert.Contains("Ana Dumoncel", quem.Linhas.Select(l => l.Nome));
        Assert.Contains("Bibiana Ritter", quem.Linhas.Select(l => l.Nome));
    }

    [Fact]
    public async Task Quem_reagiu_num_jogo_apagado_e_NULO_pro_controller_virar_404()
    {
        using var ctx = TestInfra.NovoContexto();
        var servico = new ReacaoService(ctx);

        // 11/09/2026: o mesmo botão do `VerVotos` gerou três 500 no vigia porque o jogo tinha
        // sido apagado embaixo da lista aberta. 404 é resposta, 500 é defeito.
        Assert.Null(await servico.ObterQuemReagiuAsync(9999));
    }

    // ─────────────────── O QUE SÓ EXISTE NA TELA ───────────────────

    [Fact]
    public void A_fileira_de_reacoes_esta_nas_DUAS_apresentacoes_do_jogo()
    {
        // O card do AO VIVO e a linha (Agendadas/Finalizadas) são markups diferentes, e é assim
        // que um botão nasce só numa das duas — foi o que aconteceu com o "desfazer o play".
        // 🗣️ "a cada jogo": as três abas.
        foreach (var arquivo in new[] { "_JogosDoTorneio.cshtml", "_JogoEmLinha.cshtml" })
        {
            var fonte = Ler("Views", "Torneios", arquivo);
            Assert.Contains("_ReacoesDoJogo", fonte);
        }
    }

    [Fact]
    public void O_card_mostra_so_o_que_JA_TEM_mais_o_botao_que_abre_o_teclado()
    {
        var fonte = Ler("Views", "Torneios", "_ReacoesDoJogo.cshtml");

        // A decisão do Felipe: a fileira nasce com as pílulas que existem — num jogo sem reação
        // sobra só o botão. É a mesma régua do palpitômetro, que não aparece sem voto.
        Assert.Contains("pdz-reacoes", fonte);
        Assert.Contains("pdz-reacao-abrir", fonte);
        Assert.Contains("pdz-reacao-pilula", fonte);
    }

    [Fact]
    public void O_JS_fala_com_as_tres_rotas_e_com_o_modal()
    {
        var js = Ler("wwwroot", "js", "reacoes-do-jogo.js");

        Assert.Contains("/Partidas/Reagir", js);
        Assert.Contains("/Partidas/TirarReacao", js);
        Assert.Contains("/Partidas/QuemReagiu", js);
    }

    // ⚠️ O NOME VEM DO CADASTRO — texto de gente, montado com innerHTML no modal. Sem escapar,
    // um nome com "<" quebra a lista e um nome montado de propósito injeta marcação. É a mesma
    // guarda que o modal de votos já tem (palpitometro.js).
    [Fact]
    public void O_modal_escapa_o_nome_de_quem_reagiu()
    {
        var js = Ler("wwwroot", "js", "reacoes-do-jogo.js");

        Assert.Contains("&lt;", js);
        Assert.Contains("&amp;", js);
    }

    // ─────────────────── ISSO VIRA SQL DE VERDADE? ───────────────────
    //
    // ⚠️ O EF InMemory do resto da suíte NÃO TRADUZ NADA: uma consulta que o Postgres recusa
    // passa lisa pelos 6.800 testes e só estoura na primeira visita de verdade (aconteceu em
    // 19/08/2026). A do "quem reagiu" navega pro Jogador DENTRO da projeção, que é exatamente
    // a forma que o CLAUDE.md manda conferir. `ToQueryString()` compila sem abrir conexão —
    // ver o cabeçalho de TraducaoDasConsultasDePalpiteTests pro padrão.

    [Fact]
    public void A_consulta_de_QUEM_REAGIU_vira_SQL()
    {
        using var ctx = ContextoPostgres();

        // Mesma forma exata de ReacaoService.ObterQuemReagiuAsync.
        var sql = ctx.ReacoesDaPartida
            .Where(r => r.PartidaId == 42)
            .Select(r => new { r.Emoji, r.CriadoEm, r.Jogador.Nome, r.Jogador.FotoPerfil })
            .ToQueryString();

        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void A_consulta_do_LOTE_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        var ids = new List<int> { 1, 2, 3 };

        // Mesma forma exata de ReacaoService.ObterResumosAsync.
        var sql = ctx.ReacoesDaPartida
            .Where(r => ids.Contains(r.PartidaId))
            .Select(r => new { r.PartidaId, r.JogadorId, r.Emoji, r.CriadoEm })
            .ToQueryString();

        Assert.Contains("SELECT", sql);
    }

    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    // ─────────────────── INFRA ───────────────────

    private static async Task<Partida> MontarJogoAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Finalizada",
            Fase = FasesTorneio.FaseDeGrupos,
            Codigo = "P1",
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();
        return partida;
    }

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string nome, string cpf)
    {
        var torcedor = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    private static string Ler(params string[] caminho) =>
        File.ReadAllText(Path.Combine(new[] { PastaDoProjeto() }.Concat(caminho).ToArray()));

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
}
