using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O MATA-MATA DO NATA TEM QUE NASCER COMO A PRÉVIA PROMETEU — DUPLAS, HORÁRIO E QUADRA, RODADA A
// RODADA.
//
// 🗣️ Felipe, 09/10/2026, na véspera do NATA PADEL TOUR (10–11/10): *"dessa vez as chaves tem q
// seguir o que foi previsto, não pode ser igual ao último torneio"*. O último foi o ER (12–13/09):
// o cruzamento da 1ª rodada mudou depois de publicado ("era 1º da F contra o 2º da E") e as
// semifinais prometidas pra domingo de manhã foram parar no sábado. Os dois têm conserto e teste
// (ChaveRespeitaOPrevistoTests, OHorarioDoSorteioEPromessaTests) — mas SÓ NO FORMATO DO ER: 4
// grupos de 3, quadro cheio, sem bye, e conferindo só a 1ª rodada.
//
// 🕳️ OS FORMATOS DO NATA NÃO TINHAM TESTE NENHUM. Lidos da página pública em 09/10:
//   • 4ª Masculina — 9 duplas, 3 grupos de 3 → 6 classificados num quadro de 8 → DOIS BYES;
//   • 5ª Feminina  — 7 duplas, 3 grupos DESIGUAIS → dois byes escolhidos por "quem jogou menos";
//   • 5ª e 6ª Masculina — 6 duplas, 2 grupos de 3 → semifinal direta;
//   • 6ª Feminina  — 4 duplas, 2 grupos de 2.
// A prévia publicada das categorias com bye diz "Semifinal 1: Vencedor QF1 × 1º do Grupo B" e
// "Semifinal 2: Vencedor QF2 × 1º do Grupo A". O motor de verdade monta a semifinal com outra
// conta (AvancoDaChave: vencedores, depois os byes na ordem de `OrdemDosByes`, cruzando primeiro
// com último). Se as duas contas discordarem num destes formatos, o 1º do A e o 1º do B trocam de
// semifinal no domingo — e ninguém fica sabendo até a chave aparecer.
//
// ⚠️ A PROMESSA É A PRÉVIA DA PÁGINA, lida do próprio `Details` (`ViewBag.ProjecaoCompleta`), e não
// uma conta refeita aqui: é ela que o jogador viu, e uma reimplementação no teste concordaria com
// o motor pelo mesmo motivo que o motor estaria errado.
//
// ⚠️ OS RESULTADOS SÃO SORTEADOS POR SEMENTE: é a campanha desigual que reordena os colocados, e
// é ela que fez o ER discordar. Cada semente é um torneio inteiro jogado até a final.
public class AChaveDoNataSegueAPreviaTests
{
    private static readonly Regex Colocacao = new(@"^(\d+)º do (Grupo \S+)$");
    private static readonly Regex Vencedor = new(@"^Vencedor (.+?)(?: (\d+))?$");

    // Um sábado bem à frente: a reserva de horário não ressuscita horário VENCIDO (14/09/2026),
    // e um torneio no passado mediria essa outra regra em vez desta.
    private static DateTime ProximoSabado()
    {
        var dia = DateTime.Today.AddDays(14);
        while (dia.DayOfWeek != DayOfWeek.Saturday) dia = dia.AddDays(1);
        return dia;
    }

    private static async Task<(Torneio torneio, Categoria categoria, Jogador org)> NataAsync(
        DbPadelContext ctx, int qtdDuplas)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        var sabado = ProximoSabado();
        torneio.DataInicio = sabado.AddHours(9);
        torneio.DataFim = sabado.AddDays(1);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "RADAR 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "RADAR 2" });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);
        return (torneio, categoria, org);
    }

    // A prévia como a página a mostra, no instante em que a chave é publicada.
    private static async Task<List<ProximasFasesDaChave.JogoQueVem>> PreviaPublicadaAsync(
        DbPadelContext ctx, int torneioId, int orgId)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, orgId);
        await controller.Details(torneioId, null, null);
        return (controller.ViewBag.ProjecaoCompleta as List<ProximasFasesDaChave.JogoQueVem>)
               ?? new List<ProximasFasesDaChave.JogoQueVem>();
    }

    private static bool EhDeGrupo(Partida p) => FasesTorneio.EhFaseDeGrupos(p.Fase);

    // Traduz um rótulo da prévia ("2º do Grupo C", "Vencedor Quartas de Final 1") na dupla que
    // ele designa AGORA, com os jogos que já terminaram.
    private static async Task<int> QuemEAsync(DbPadelContext ctx, Categoria categoria, string rotulo)
    {
        var partidas = await ctx.Partidas.AsNoTracking()
            .Where(p => p.CategoriaId == categoria.Id).ToListAsync();

        if (Colocacao.Match(rotulo) is { Success: true } col)
        {
            var duplas = await ctx.Duplas.AsNoTracking()
                .Where(d => d.CategoriaId == categoria.Id).ToListAsync();
            var deGrupo = partidas.Where(EhDeGrupo).Where(p => p.Status == "Finalizada").ToList();
            var pontos = await ClassificacaoDeGrupos.PontosSePrecisarAsync(duplas, deGrupo, TestInfra.SemPontosDoRanking);
            var classificados = ClassificacaoDeGrupos.Calcular(
                duplas, deGrupo, pontos, ClassificacaoDeGrupos.VagasPorGrupo(categoria));

            int posicao = int.Parse(col.Groups[1].Value);
            string grupo = col.Groups[2].Value;
            return classificados.Single(c => c.Posicao == posicao
                && (c.Grupo == grupo || $"Grupo {c.Grupo}" == grupo)).DuplaId;
        }

        if (Vencedor.Match(rotulo) is { Success: true } ven)
        {
            string fase = ven.Groups[1].Value;
            int numero = ven.Groups[2].Success ? int.Parse(ven.Groups[2].Value) : 1;
            // A régua OFICIAL de número na fase (o gravado manda; sem ele, a posição por Id) — a
            // mesma que o robô usa pra casar jogo com reserva e promessa. Tratar nulo como "1"
            // foi o primeiro erro deste teste: as duas semifinais viravam a Semifinal 1.
            var numeros = ReservasDeHorario.NumeroNaFase(partidas);
            var jogo = partidas.Single(p => p.Fase == fase && numeros[p.Id] == numero);
            Assert.Equal("Finalizada", jogo.Status);
            return jogo.GamesDupla1 > jogo.GamesDupla2 ? jogo.Dupla1Id : jogo.Dupla2Id;
        }

        throw new Xunit.Sdk.XunitException($"Rótulo da prévia que o teste não sabe ler: \"{rotulo}\"");
    }

    // O retrato que a mensagem de falha carrega: classificação de cada grupo e os jogos de grupo
    // com placar. Sem ele, "nasceu com outras duplas" não diz se o errado é o motor ou o teste.
    private static async Task<string> RetratoAsync(DbPadelContext ctx, Categoria categoria)
    {
        var partidas = await ctx.Partidas.AsNoTracking().Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        var duplas = await ctx.Duplas.AsNoTracking().Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        var deGrupo = partidas.Where(EhDeGrupo).Where(p => p.Status == "Finalizada").ToList();
        var pontos = await ClassificacaoDeGrupos.PontosSePrecisarAsync(duplas, deGrupo, TestInfra.SemPontosDoRanking);
        var classificados = ClassificacaoDeGrupos.Calcular(duplas, deGrupo, pontos, ClassificacaoDeGrupos.VagasPorGrupo(categoria));
        var tabela = string.Join(" ", classificados.OrderBy(c => c.Grupo).ThenBy(c => c.Posicao)
            .Select(c => $"{c.Grupo}/{c.Posicao}º=d{c.DuplaId}"));
        var jogos = string.Join(" ", partidas.Where(EhDeGrupo).OrderBy(p => p.Id)
            .Select(p => $"[{p.Fase}] d{p.Dupla1Id} {p.GamesDupla1}x{p.GamesDupla2} d{p.Dupla2Id} ({p.Status})"));
        var mata = string.Join(" ", partidas.Where(p => !EhDeGrupo(p)).OrderBy(p => p.Id)
            .Select(p => $"[{p.Fase} {p.NumeroNaFase}] d{p.Dupla1Id}×d{p.Dupla2Id}"));
        return $"Classificação: {tabela} | Grupos: {jogos} | Mata-mata: {mata}";
    }

    // Joga o torneio inteiro, um jogo por vez, na ordem da grade. A cada jogo que termina, todo
    // jogo de mata-mata que acabou de NASCER é comparado com o que a prévia publicada prometia.
    private static async Task<int> JogarConferindoAsync(
        DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador org,
        List<ProximasFasesDaChave.JogoQueVem> promessa, int semente, TimeSpan? atraso = null)
    {
        var sorte = new Random(semente);
        var conferidos = new HashSet<int>();

        for (int passo = 0; passo < 200; passo++)
        {
            // Primeiro: o que nasceu desde a última volta.
            var nascidos = await ctx.Partidas.AsNoTracking()
                .Where(p => p.CategoriaId == categoria.Id)
                .ToListAsync();
            var numeros = ReservasDeHorario.NumeroNaFase(nascidos);
            foreach (var jogo in nascidos.Where(p => !EhDeGrupo(p) && !conferidos.Contains(p.Id)))
            {
                int numero = numeros[jogo.Id];
                var prometido = promessa.SingleOrDefault(j => j.CategoriaId == categoria.Id
                    && j.Fase == jogo.Fase && j.Numero == numero);
                Assert.True(prometido != null,
                    $"[semente {semente}] {jogo.Fase} {numero} nasceu sem estar na prévia publicada.");

                var esperadas = new[]
                {
                    await QuemEAsync(ctx, categoria, prometido!.Lado1.Rotulo),
                    await QuemEAsync(ctx, categoria, prometido.Lado2.Rotulo),
                }.OrderBy(x => x).ToArray();
                var reais = new[] { jogo.Dupla1Id, jogo.Dupla2Id }.OrderBy(x => x).ToArray();

                Assert.True(esperadas.SequenceEqual(reais),
                    $"[semente {semente}] {prometido.FaseNumerada}: a prévia prometia "
                    + $"\"{prometido.Lado1.Rotulo} × {prometido.Lado2.Rotulo}\" e o jogo nasceu com outras duplas. "
                    + $"Esperadas [{string.Join(",", esperadas)}], reais [{string.Join(",", reais)}]. "
                    + await RetratoAsync(ctx, categoria));
                Assert.True(prometido.Horario == jogo.HorarioPrevisto,
                    $"[semente {semente}] {prometido.FaseNumerada}: prometido {prometido.Horario:dd/MM HH:mm}, "
                    + $"nasceu {jogo.HorarioPrevisto:dd/MM HH:mm}.");
                Assert.True(prometido.Quadra == jogo.NomeQuadra,
                    $"[semente {semente}] {prometido.FaseNumerada}: prometido na quadra \"{prometido.Quadra}\", "
                    + $"nasceu na \"{jogo.NomeQuadra}\".");
                conferidos.Add(jogo.Id);
            }

            // Depois: o próximo jogo da grade termina, com placar sorteado.
            var proximo = await ctx.Partidas
                .Where(p => p.CategoriaId == categoria.Id && p.Status != "Finalizada")
                .OrderBy(p => p.HorarioPrevisto).ThenBy(p => p.Id)
                .FirstOrDefaultAsync();
            if (proximo == null) break;

            // O ATRASO DE VERDADE: o jogo começa e termina horas depois do marcado. É só isso
            // que o sistema sabe de um atraso — o horário previsto não anda sozinho.
            if (atraso is TimeSpan quanto && proximo.HorarioPrevisto is DateTime marcado)
            {
                proximo.HorarioInicioReal = marcado + quanto;
                proximo.HorarioFimReal = marcado + quanto + TimeSpan.FromMinutes(50);
                await ctx.SaveChangesAsync();
            }

            int perdedor = sorte.Next(0, 5);
            bool ganhaODe1 = sorte.Next(2) == 0;
            await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, org.Id),
                proximo, ganhaODe1 ? 6 : perdedor, ganhaODe1 ? perdedor : 6);
        }

        return conferidos.Count;
    }

    // Mais sementes onde há bye: é lá que a campanha pode reordenar quem joga as quartas.
    public static IEnumerable<object[]> FormatosDoNata()
    {
        // 4ª Masculina: 9 duplas → 3 grupos de 3 → quartas + 2 byes → semis → final.
        // 5ª Feminina: 7 duplas → 3 grupos desiguais → quartas + 2 byes por "quem jogou menos".
        foreach (var duplas in new[] { 9, 7 })
            for (int semente = 1; semente <= 30; semente++) yield return new object[] { duplas, semente };
        // 5ª e 6ª Masculina: 6 duplas → 2 grupos de 3. 6ª Feminina: 4 duplas → 2 grupos de 2.
        foreach (var duplas in new[] { 6, 4 })
            for (int semente = 1; semente <= 5; semente++) yield return new object[] { duplas, semente };
    }

    [Theory]
    [MemberData(nameof(FormatosDoNata))]
    public async Task Cada_jogo_do_mata_mata_nasce_com_as_duplas_o_horario_e_a_quadra_da_previa(
        int qtdDuplas, int semente)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await NataAsync(ctx, qtdDuplas);
        var promessa = await PreviaPublicadaAsync(ctx, torneio.Id, org.Id);
        var doMataMata = promessa.Where(j => j.CategoriaId == categoria.Id).ToList();

        Assert.NotEmpty(doMataMata);
        Assert.Contains(doMataMata, j => j.Fase == "Final");

        int conferidos = await JogarConferindoAsync(ctx, torneio, categoria, org, promessa, semente);

        // Todo jogo prometido nasceu — nenhum ficou pelo caminho, nenhum a mais.
        Assert.Equal(doMataMata.Count, conferidos);
    }

    // 🗣️ Felipe, 10/10/2026, no dia do NATA: *"mesmo que atrase muito, não pode mexer nos
    // horários previstos"*. O robô que cria quartas, semis e final NÃO LÊ O RELÓGIO: a
    // validade da reserva mede o "relógio do torneio" pelo horário PREVISTO do último jogo
    // jogado (ReservasDeHorario.RelogioDoTorneio), não pela hora real. Este teste trava isso:
    // cinco horas de atraso em TODO jogo, e cada eliminatória continua nascendo no prometido.
    [Theory]
    [InlineData(9, 1)] [InlineData(9, 2)] [InlineData(9, 3)]
    [InlineData(7, 1)] [InlineData(7, 2)] [InlineData(7, 3)]
    [InlineData(6, 1)] [InlineData(4, 1)]
    public async Task Atraso_grande_nao_mexe_no_horario_previsto_do_mata_mata(int qtdDuplas, int semente)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await NataAsync(ctx, qtdDuplas);
        var promessa = await PreviaPublicadaAsync(ctx, torneio.Id, org.Id);
        var doMataMata = promessa.Where(j => j.CategoriaId == categoria.Id).ToList();

        int conferidos = await JogarConferindoAsync(ctx, torneio, categoria, org, promessa, semente,
            atraso: TimeSpan.FromHours(5));

        Assert.Equal(doMataMata.Count, conferidos);
    }
}
