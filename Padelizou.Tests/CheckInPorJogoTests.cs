using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O CHECK-IN PASSA A SER DESENHADO PELOS JOGOS QUE VÊM, e não pela lista de inscritos.
//
// 🗣️ Felipe, com a tela do 2ª Etapa ER PADEL TOUR aberta em `/Torneios/CheckIn/26` — "0 de 64
// presentes", 64 duplas em 12 categorias: *"acho que aqui teria q mudar, por próximos jogos, e ver
// se as pessoas chegaram, e nao todos"*.
//
// 🕳️ A tela respondia "quem está INSCRITO", e no sábado de manhã a pergunta é outra: "quem joga
// AGORA já chegou?". Com 64 duplas por categoria, achar as duas do jogo das 8h era rolar a lista
// inteira e cruzar de cabeça com a grade — a informação existia e não estava onde se decide o W.O.
//
// ⚠️ A FILA É A MESMA DA ABA JOGOS (Services/OrdemNoHorario). Duas contas de "quem vem antes"
// fariam a tela do dia mostrar uma ordem e a lista de jogos outra, pra mesma grade.
//
// ⚠️ E é só "Agendada": jogo AO VIVO tem gente em quadra e finalizado já acabou — nos dois a
// pergunta do check-in já foi respondida por outra via.
public class CheckInPorJogoTests
{
    private const string Parcial = "<partial name=\"_LinhaDoCheckIn\"";

    private static (Torneio torneio, Jogador organizador, int[] jogos) Montar(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6, status: "Fase de Grupos");
        torneio.UsaCheckIn = true;
        ctx.SaveChanges();

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Select(d => d.Id).ToList();
        var dia = new DateTime(2026, 9, 12);

        int Jogo(int d1, int d2, DateTime? quando, string status, int? ordem = null)
        {
            var partida = new Partida
            {
                TorneioId = torneio.Id,
                CategoriaId = categoria.Id,
                Dupla1Id = duplas[d1],
                Dupla2Id = duplas[d2],
                Fase = "Fase de Grupos",
                Status = status,
                HorarioPrevisto = quando,
                OrdemNoHorario = ordem,
                Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
            };
            ctx.Partidas.Add(partida);
            ctx.SaveChanges();
            return partida.Id;
        }

        // De propósito fora de ordem na hora de gravar: se a tela ordenasse pelo Id (ou pela
        // ordem que o banco devolve), este arranjo passaria batido.
        int dezHoras = Jogo(0, 1, dia.AddHours(10), "Agendada");
        int oitoSemOrdem = Jogo(2, 3, dia.AddHours(8), "Agendada");
        int oitoPrimeiro = Jogo(4, 5, dia.AddHours(8), "Agendada", ordem: 1);
        int aoVivo = Jogo(0, 3, dia.AddHours(7), "AoVivo");
        int finalizada = Jogo(1, 4, dia.AddHours(7), "Finalizada");

        return (torneio, organizador, new[] { dezHoras, oitoSemOrdem, oitoPrimeiro, aoVivo, finalizada });
    }

    [Fact]
    public async Task A_tela_traz_os_jogos_que_vem_na_ordem_da_aba_jogos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, jogos) = Montar(ctx);

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).CheckIn(torneio.Id));
        var fila = Assert.IsAssignableFrom<List<Partida>>(view.ViewData["JogosQueVem"]);

        // hora → ordem gravada (o automático vai pro fim) → Id. A mesma de OrdemNoHorario.
        Assert.Equal(new[] { jogos[2], jogos[1], jogos[0] }, fila.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Jogo_ao_vivo_e_finalizado_saem_da_fila_de_cima()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, jogos) = Montar(ctx);

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).CheckIn(torneio.Id));
        var fila = Assert.IsAssignableFrom<List<Partida>>(view.ViewData["JogosQueVem"]);

        Assert.DoesNotContain(jogos[3], fila.Select(p => p.Id));   // ao vivo
        Assert.DoesNotContain(jogos[4], fila.Select(p => p.Id));   // finalizada
    }

    [Fact]
    public async Task O_que_ja_comecou_vai_pro_bloco_do_fim_com_o_ao_vivo_na_frente()
    {
        // 🗣️ Felipe, 11/09/2026: *"deixe apenas dos jogos que ainda não começaram, se os jogos
        // ja começaram, pode ocultar, coloca la no final da tela minimazado como ja jogaram ou
        // estão em jogo"*. Antes eles sumiam da tela inteira — quem marcasse o jogo como
        // começado perdia o caminho pro check-in daquela dupla.
        //
        // Ao vivo antes de finalizado: um está acontecendo AGORA, o outro é histórico.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, jogos) = Montar(ctx);

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).CheckIn(torneio.Id));
        var doFim = Assert.IsAssignableFrom<List<Partida>>(view.ViewData["JogosQueJaRolaram"]);

        Assert.Equal(new[] { jogos[3], jogos[4] }, doFim.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task O_bloco_do_fim_tambem_vem_com_as_duplas_carregadas()
    {
        // Ele desenha a mesma linha de check-in, com o mesmo botão: sem os Includes a tela
        // estouraria justamente no bloco que ninguém abre no ensaio.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Montar(ctx);

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).CheckIn(torneio.Id));
        var primeiro = Assert.IsAssignableFrom<List<Partida>>(view.ViewData["JogosQueJaRolaram"]).First();

        Assert.NotNull(primeiro.Dupla1?.Jogador1);
        Assert.NotNull(primeiro.Dupla2?.Jogador1);
        Assert.NotNull(primeiro.Categoria);
    }

    [Fact]
    public async Task As_duplas_do_jogo_vem_carregadas_com_os_jogadores()
    {
        // Sem os Includes a tela renderizaria vazio (ou estouraria): quem desenha a linha
        // precisa do nome, e lazy loading não está ligado aqui.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Montar(ctx);

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).CheckIn(torneio.Id));
        var primeiro = Assert.IsAssignableFrom<List<Partida>>(view.ViewData["JogosQueVem"]).First();

        Assert.NotNull(primeiro.Dupla1?.Jogador1);
        Assert.NotNull(primeiro.Dupla2?.Jogador1);
        Assert.NotNull(primeiro.Categoria);
    }

    // ── A TELA ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Os_jogos_vem_antes_do_resto_do_torneio()
    {
        var fonte = Ler("Torneios/CheckIn.cshtml");
        int jogos = fonte.IndexOf("JogosQueVem", StringComparison.Ordinal);
        int resto = fonte.IndexOf("restoDoTorneio", StringComparison.Ordinal);

        Assert.True(jogos >= 0, "A tela não lê a fila dos jogos.");
        Assert.True(resto >= 0, "Sumiu o bloco com o resto do torneio — quem chega cedo não teria como ser marcado.");
        Assert.True(jogos < resto, "Os jogos que vêm são o assunto da tela; a lista completa fica embaixo.");
    }

    [Fact]
    public void O_resto_do_torneio_nasce_fechado()
    {
        // A lista das 64 duplas continua alcançável, mas não é mais o que a tela mostra de cara.
        var fonte = Ler("Torneios/CheckIn.cshtml");
        int resto = fonte.IndexOf("id=\"restoDoTorneio\"", StringComparison.Ordinal);
        Assert.True(resto >= 0, "Não achei o bloco do resto do torneio.");

        Assert.Contains("data-bs-toggle=\"collapse\"", fonte[Math.Max(0, resto - 900)..resto]);
        Assert.DoesNotContain("show", fonte[resto..(resto + 60)]);
    }

    [Fact]
    public void O_bloco_do_que_ja_rolou_nasce_fechado_e_fica_no_fim_da_tela()
    {
        var fonte = Ler("Torneios/CheckIn.cshtml");
        int jogos = fonte.IndexOf("JogosQueVem", StringComparison.Ordinal);
        int resto = fonte.IndexOf("id=\"restoDoTorneio\"", StringComparison.Ordinal);
        int jaRolou = fonte.IndexOf("id=\"jaJogaram\"", StringComparison.Ordinal);

        Assert.True(jaRolou >= 0, "Não achei o bloco de quem já jogou / está em jogo.");
        Assert.True(jogos < jaRolou, "Ele não pode vir antes da fila dos jogos que ainda não começaram.");
        Assert.True(resto < jaRolou, "Pedido: no FIM da tela — depois do resto do torneio.");

        Assert.Contains("data-bs-toggle=\"collapse\"", fonte[Math.Max(0, jaRolou - 900)..jaRolou]);
        Assert.DoesNotContain("show", fonte[jaRolou..(jaRolou + 60)]);
    }

    [Fact]
    public void O_botao_de_presenca_mora_num_lugar_so()
    {
        // A mesma linha é desenhada em três lugares: nos jogos que vêm, no bloco do fim e na
        // lista por categoria. Duas cópias do formulário que GRAVA presença divergiriam na
        // primeira mudança — então ele existe uma vez só, no parcial da linha.
        var tela = Ler("Torneios/CheckIn.cshtml");
        var linha = Ler("Torneios/_LinhaDoCheckIn.cshtml");
        var cartao = Ler("Torneios/_JogoNoCheckIn.cshtml");

        Assert.Contains("asp-action=\"MarcarCheckIn\"", linha);
        Assert.DoesNotContain("asp-action=\"MarcarCheckIn\"", tela);
        Assert.DoesNotContain("asp-action=\"MarcarCheckIn\"", cartao);

        // O cartão do jogo desenha as DUAS duplas...
        Assert.Equal(2, cartao.Split(Parcial).Length - 1);
        // ...e o cartão é o mesmo nos dois blocos de jogo da tela (o que vem e o que já rolou).
        Assert.True(tela.Split("<partial name=\"_JogoNoCheckIn\"").Length - 1 >= 2,
            "O cartão do jogo devia ser o mesmo na fila de cima e no bloco do fim.");
        // A lista por categoria continua usando a linha direto.
        Assert.Contains(Parcial, tela);
    }

    [Fact]
    public void A_lista_nao_volta_pro_topo_a_cada_marcacao()
    {
        // Marcar 64 duplas é 64 POSTs, e cada um redesenha a página do começo. É a mesma
        // queixa que gerou o js/manter-posicao-na-lista.js, e a mesma peça resolve — por
        // opt-in no formulário, como lá.
        Assert.Contains("data-manter-posicao", Ler("Torneios/_LinhaDoCheckIn.cshtml"));
        Assert.Contains("js/manter-posicao-na-lista.js", Ler("Torneios/CheckIn.cshtml"));
    }

    [Fact]
    public void A_consulta_dos_jogos_do_check_in_vira_SQL()
    {
        // ⚠️ O InMemory do resto da suíte NÃO TRADUZ NADA: uma consulta que o Postgres recusa
        // passa lisa por tudo e estoura na primeira visita à tela (19/08/2026). Aqui o
        // provedor é o Npgsql de verdade, apontado pra lugar nenhum — o `ToQueryString`
        // compila e não conecta. O filtro é pela navegação `Categoria.TorneioId`, que é o
        // ponto onde uma consulta assim costuma não traduzir.
        var opcoes = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(opcoes);

        var sql = ctx.Partidas
            .Include(p => p.Categoria)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.Categoria.TorneioId == 1)
            .ToQueryString();

        Assert.Contains("SELECT", sql);
    }

    private static string Ler(string view) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", view));

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Padelizou.csproj não encontrado a partir do bin.");
    }
}
