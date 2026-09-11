using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.IO;
using Xunit;

namespace Padelizou.Tests;

// A BOLINHA DO SAQUE SEMPRE TEM DONO (11/09/2026).
//
// 🗣️ Felipe, num print do card AO VIVO sem bolinha nenhuma: *"Nao esta exibindo a bolinha
// verde de quem esta sacando, é algum erro?"* — e, depois do diagnóstico: *"normalmente como
// funciona, tanto faz em quem começar a bolinha, mas tem q ter em alguem, quando colocarmos
// para iniciar o jogo, deixe a bolinha com qualquer dupla, mas permita o organizador/marcador
// alterar a bolinha."*
//
// 🕳️ O DEFEITO NÃO ERA DE DESENHO. O card sempre soube desenhar a bolinha; o campo é que
// nascia vazio e nunca era preenchido — o ÚNICO lugar do site que gravava `DuplaSacandoId`
// era a tela cheia do lápis, que já nasce marcada em "Não mostrar" e que ninguém abre com
// cinco quadras rolando. Feature no ar desde 05/08, invisível desde 05/08.
//
// Duas regras, e as duas moram em Services/SaqueDoJogo:
//   1. Jogo que ENTRA EM QUADRA sai com alguém sacando. Qual dupla é indiferente (é sorteio
//      de quadra, o servidor não tem como saber) — o que não pode é ficar sem.
//   2. Quem marca placar troca de dupla com UM toque, no próprio card.
//
// ⚠️ "Não mostrar" continua existindo pro jogo JÁ no ar: é a saída de quem não sabe quem
// está sacando e prefere não mentir pro torcedor. O que a largada não aceita é o vazio que
// vem do formulário só porque o campo nunca foi tocado.
public class SaqueSempreDefinidoTests
{
    private static Partida NovaPartida(DbPadelContext ctx, Torneio torneio, Categoria categoria,
        int dupla1, int dupla2, string status = "Agendada", int? sacando = null)
    {
        var partida = new Partida
        {
            CategoriaId = categoria.Id,
            TorneioId = torneio.Id,
            Codigo = "P1",
            Fase = "Grupo A",
            Dupla1Id = dupla1,
            Dupla2Id = dupla2,
            Status = status,
            DuplaSacandoId = sacando,
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();
        return partida;
    }

    private static (Partida partida, Jogador organizador, int dupla1, int dupla2) Cenario(
        DbPadelContext ctx, string status = "Agendada", int? sacando = null)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Fase de Grupos");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        var partida = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, status,
            sacando == null ? null : (sacando == 1 ? duplas[0].Id : duplas[1].Id));
        return (partida, organizador, duplas[0].Id, duplas[1].Id);
    }

    // ── 1. A LARGADA NUNCA DEIXA O JOGO SEM SAQUE ──────────────────────────────────────────

    [Fact]
    public async Task O_play_deixa_a_bolinha_com_alguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, dupla1, _) = Cenario(ctx);

        await TestInfra.NovoPartidasController(ctx, organizador.Id).ColocarNoAr(partida.Id);

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", salva!.Status);
        Assert.Equal(dupla1, salva.DuplaSacandoId);
    }

    // Qual dupla é indiferente — o que NÃO pode é a largada passar por cima de uma escolha que
    // já existia. Jogo que voltou pra agendado e foi chamado de novo, com o saque marcado no
    // meio, manteria a bolinha onde o organizador pôs.
    [Fact]
    public async Task O_play_nao_mexe_na_bolinha_que_ja_tinha_dono()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _, dupla2) = Cenario(ctx, sacando: 2);

        await TestInfra.NovoPartidasController(ctx, organizador.Id).ColocarNoAr(partida.Id);

        Assert.Equal(dupla2, (await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
    }

    // A OUTRA PORTA DA LARGADA: o <select> de status do Controle de Partida. Sem a regra aqui,
    // quem começa o jogo por essa tela cai no vazio de novo — e o formulário AJUDA a cair,
    // porque o "Não mostrar" vem pré-marcado em todo jogo que nunca teve saque.
    [Fact]
    public async Task A_largada_pelo_controle_de_placar_tambem_define_o_saque()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, dupla1, _) = Cenario(ctx);

        await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ControlePlacar(partida.Id, "AoVivo", 0, 0, null, null, duplaSacandoId: null);

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", salva!.Status);
        Assert.Equal(dupla1, salva.DuplaSacandoId);
    }

    // ⚠️ E O "NÃO MOSTRAR" CONTINUA VALENDO com o jogo JÁ no ar. É a saída de quem não sabe
    // quem está sacando: bolinha errada na tela do torcedor é pior que bolinha nenhuma. A
    // regra da largada só vale na TRANSIÇÃO — aqui não há largada, há uma escolha.
    [Fact]
    public async Task Nao_mostrar_continua_apagando_a_bolinha_do_jogo_no_ar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _, _) = Cenario(ctx, status: "AoVivo", sacando: 1);

        await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ControlePlacar(partida.Id, "AoVivo", 3, 2, null, null, duplaSacandoId: null);

        Assert.Null((await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
    }

    // Reabrir devolve o jogo PRA QUADRA — e jogo em quadra tem alguém sacando. Sem isto o
    // placar corrigido volta ao vivo com o card sem bolinha, que é a queixa do print.
    [Fact]
    public async Task Reabrir_um_jogo_devolve_a_bolinha_junto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        foreach (var jogo in grupos)
            await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, jogo, 9, 3);

        var ultimo = grupos.Last();
        await TestInfra.NovoPartidasController(ctx, org.Id).ReabrirPartida(ultimo.Id);

        var reaberto = await ctx.Partidas.FindAsync(ultimo.Id);
        Assert.Equal("AoVivo", reaberto!.Status);
        Assert.NotNull(reaberto.DuplaSacandoId);
    }

    // ── 2. O TOQUE QUE TROCA A BOLINHA DE LADO ─────────────────────────────────────────────

    [Fact]
    public async Task O_toque_passa_o_saque_pra_outra_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, dupla1, dupla2) = Cenario(ctx, status: "AoVivo", sacando: 1);

        var resposta = await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .TrocarSaque(partida.Id, dupla2);

        Assert.IsNotType<ForbidResult>(resposta);
        Assert.Equal(dupla2, (await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
        Assert.NotEqual(dupla1, (await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
    }

    // ⚠️ A DUPLA TEM QUE SER DESTE JOGO. Um POST montado à mão apontaria a bolinha pra uma
    // dupla que não está em quadra, e a tela mostraria um nome de outra partida. É a mesma
    // checagem que o Controle de Partida já fazia — agora numa régua só (Services/SaqueDoJogo).
    [Fact]
    public async Task O_toque_recusa_dupla_de_outro_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, dupla1, _) = Cenario(ctx, status: "AoVivo", sacando: 1);
        var deOutraPartida = ctx.Duplas.Select(d => d.Id).ToList().Except(new[] { partida.Dupla1Id, partida.Dupla2Id }).First();

        var resposta = await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .TrocarSaque(partida.Id, deOutraPartida);

        Assert.IsType<BadRequestResult>(resposta);
        Assert.Equal(dupla1, (await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
    }

    // ⚠️ SÓ JOGO EM QUADRA. A bolinha responde "quem está sacando AGORA" — num jogo agendado
    // ela promete uma informação que não existe, e é exatamente o estado que o
    // Services/DesfazerDoJogo limpa quando o jogo sai da quadra.
    [Fact]
    public async Task O_toque_recusa_jogo_que_nao_esta_em_quadra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _, dupla2) = Cenario(ctx);

        var resposta = await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .TrocarSaque(partida.Id, dupla2);

        Assert.IsType<BadRequestResult>(resposta);
        Assert.Null((await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
    }

    // Regra 0: ação que grava dado confere o dono. O gate mecânico já cobra [HttpPost] e
    // [Authorize]; quem está em quadra é julgamento, e é este teste.
    [Fact]
    public async Task Estranho_nao_troca_o_saque()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, dupla1, dupla2) = Cenario(ctx, status: "AoVivo", sacando: 1);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "12312312399" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var resposta = await TestInfra.NovoPartidasController(ctx, estranho.Id)
            .TrocarSaque(partida.Id, dupla2);

        Assert.IsType<ForbidResult>(resposta);
        Assert.Equal(dupla1, (await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
    }

    // O MARCADOR do torneio troca: a bolinha é trabalho de quem está na quadra marcando, e
    // barrar ele aqui seria a Mesa abrindo com um botão que responde 403.
    [Fact]
    public async Task O_marcador_do_torneio_troca_o_saque()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _, dupla2) = Cenario(ctx, status: "AoVivo", sacando: 1);
        var marcador = new Jogador { Nome = "Marcador", Cpf = "45645645699" };
        ctx.Jogadores.Add(marcador);
        ctx.SaveChanges();
        ctx.TorneioMarcadores.Add(new TorneioMarcador { TorneioId = partida.TorneioId!.Value, JogadorId = marcador.Id });
        ctx.SaveChanges();

        var resposta = await TestInfra.NovoPartidasController(ctx, marcador.Id)
            .TrocarSaque(partida.Id, dupla2);

        Assert.IsNotType<ForbidResult>(resposta);
        Assert.Equal(dupla2, (await ctx.Partidas.FindAsync(partida.Id))!.DuplaSacandoId);
    }

    // ── 3. A TELA ──────────────────────────────────────────────────────────────────────────
    // A suíte não renderiza Razor: guarda de ARQUIVO, e sempre sem os comentários — o
    // comentário acima do código cita a classe procurada e o Assert.Contains acharia ali, com
    // o código apagado (TestInfra.SemComentarios).

    [Fact]
    public void O_card_ao_vivo_desenha_a_bolinha_dos_DOIS_lados()
    {
        var card = TestInfra.SemComentarios(LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml"));

        // Um parcial só, usado nas duas linhas: duas cópias do mesmo desenho seriam duas
        // verdades sobre título, tamanho e alvo de toque, e a segunda envelhece calada.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(card, "_BolinhaDoSaque").Count);
        Assert.Contains("Dupla1Id", card);
        Assert.Contains("Dupla2Id", card);
    }

    [Fact]
    public void A_bolinha_apagada_e_o_alvo_de_quem_pode_trocar()
    {
        var parcial = TestInfra.SemComentarios(LerDaWeb("Views", "Torneios", "_BolinhaDoSaque.cshtml"));

        // Formulário de verdade: sem JS o toque ainda troca o saque (POST + volta), como o
        // play e o finalizar. O js/saque-ao-vivo.js só intercepta pra não recarregar a página.
        Assert.Contains("TrocarSaque", parcial);
        Assert.Contains("pdz-bolinha-apagada", parcial);
        // Quem não marca placar não ganha alvo nenhum — só a bolinha de quem está sacando.
        Assert.Contains("PodeTrocar", parcial);
    }

    [Fact]
    public void A_bolinha_apagada_tem_CSS()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        Assert.Contains(".pdz-bolinha-apagada", css);
        Assert.Contains(".pdz-saque-toque", css);
    }

    // ⚠️ O TOQUE PAUSA A ATUALIZAÇÃO AUTOMÁTICA. Ela troca o cabeçalho do card pelo HTML do
    // servidor a cada 20s, e o servidor ainda não sabe do saque que acabou de ser tocado — a
    // bolinha voltaria pro lado velho na frente de quem acabou de mover.
    //
    // ⚠️ E AS DUAS PONTAS PRECISAM ANDAR JUNTAS: levantar a bandeira sem ninguém lendo, ou ler
    // uma que ninguém levanta, falha CALADO — a tela só pisca de vez em quando, e só quando há
    // jogo ao vivo com alguém marcando. É o tipo de defeito que só aparece no dia do torneio.
    [Fact]
    public void O_toque_segura_a_atualizacao_automatica_enquanto_salva()
    {
        var levanta = LerDaWeb("wwwroot", "js", "saque-ao-vivo.js");
        var le = TestInfra.SemComentarios(LerDaWeb("wwwroot", "js", "jogos-ao-vivo-atualiza.js"));

        // ⚠️ Bandeira PRÓPRIA, e não a `pdzSalvandoPlacar` do placar: compartilhar abriria uma
        // corrida em que quem termina primeiro baixa a bandeira do outro, e a atualização
        // automática entra no meio do salvamento que continua em pé.
        Assert.Contains("window.pdzTrocandoSaque = true", levanta);
        Assert.Contains("window.pdzTrocandoSaque = false", levanta);
        Assert.Contains("pdzTrocandoSaque", le);
        Assert.DoesNotContain("pdzSalvandoPlacar = ", levanta);

        // Só JSON conta como salvo: sessão vencida responde 302 pra tela de login, o fetch
        // segue o desvio e entrega 200 com o HTML do login.
        Assert.Contains("json", levanta);
    }

    // O parcial do card vive em DUAS telas (a página do torneio e a lista de jogos), e cada
    // uma registra os scripts do ao vivo por conta própria. Servir o arquivo só numa delas é
    // defeito MUDO: a outra continua desenhando o alvo de toque, o toque vira POST com recarga
    // — a tela pula pro topo e o <iframe> da transmissão reinicia, sem erro em log nenhum.
    [Theory]
    [InlineData("Details.cshtml")]
    [InlineData("jogos.cshtml")]
    public void As_duas_telas_do_card_ao_vivo_servem_o_saque(string tela)
    {
        var view = LerDaWeb("Views", "Torneios", tela);

        Assert.Contains("saque-ao-vivo.js", view);
        // Ele depende da bandeira que o placar levanta, então nasce junto dos outros dois.
        Assert.Contains("jogos-ao-vivo-atualiza.js", view);
    }

    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
