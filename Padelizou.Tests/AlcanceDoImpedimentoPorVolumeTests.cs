using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O IMPEDIMENTO PAGO CEDIA QUANDO MUITAS DUPLAS BLOQUEAVAM O MESMO DIA (09/09/2026).
//
// 🗣️ O impedimento é cobrado na inscrição (`Torneio.TaxaPorImpedimento`): é garantia VENDIDA,
// não preferência. Ela cedia calada — o jogo nascia dentro da janela e ninguém era avisado.
//
// 🕳️ MEDIDO, não deduzido. Torneio começando SEXTA 03/07/2026, 16 duplas numa categoria
// (2 grupos de 2 + 4 de 3 = 14 jogos de grupo), `ImpedimentoSextaNoite` marcado ANTES do
// sorteio. Varrendo o VOLUME de duplas impedidas — os 16 arranjos de cada volume, ANTES da
// correção — os furos aparecem a partir de 6 e saturam em 8:
//
//    4 de 16 impedidas →  0/16 arranjos com furo
//    6 de 16 impedidas →  9/16
//    8 de 16 impedidas → 16/16
//   12 de 16 impedidas → 16/16
//   16 de 16 impedidas → 16/16
//
// ⚠️ A CAUSA NÃO É O ENCAIXE, É O TAMANHO DA GRADE OFERECIDA A ELE.
// `VagasDaGrade.AlcanceNecessario` devolve o FIM da janela mais tardia; pro impedimento de
// sexta a janela é o dia inteiro, então o fim é SÁBADO 00:00 — antes de existir qualquer vaga
// do sábado (que abre em `HoraInicioDiasSeguintes`). O `Montar` segue uma margem além desse
// limite, mas a margem era `max(quadras,1)*3`: dimensionada por QUADRA, e não pelo volume de
// jogos que a janela empurrou pro outro lado. Com 8 duplas impedidas na sexta há muito mais
// que 3 jogos precisando de vaga no sábado, as vagas acabam, e o último recurso do
// `GradeDeJogos.Encaixar` entra — cedendo primeiro o impedimento.
//
// ✅ FALSIFICADO: devolvendo `margem` sozinha no lugar de `JogosEmpurrados + margem`
// (`VagasDaGrade.Montar`, uma linha), voltam exatamente os números da tabela acima.
//
// ⚠️ POR QUE UMA VARREDURA, E NÃO UM CENÁRIO SÓ: quem fura não é o volume sozinho, é o volume
// somado ao ARRANJO — de quais grupos as impedidas saíram. Um arranjo só passa ou falha por
// sorte do desenho, então cada volume roda com a marcação girando por todas as 16 posições de
// partida. É a mesma razão pela qual o motor precisa de mais de uma execução quando o
// desempate do sorteio for sorteado: uma rodada não é medida.
public class AlcanceDoImpedimentoPorVolumeTests
{
    // 03/07/2026 é SEXTA — o único formato em que a janela do impedimento de sexta (dia
    // inteiro) e o sábado que a recebe existem os dois dentro dos 3 dias que
    // `JanelasDeImpedimento.DiaDoTorneio` varre.
    private static readonly DateTime SextaDeAbertura = new(2026, 7, 3, 9, 0, 0);

    private const int Duplas = 16;

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public async Task Nenhum_impedimento_e_furado_seja_qual_for_o_volume(int impedidas)
    {
        var comFuro = new List<string>();

        for (int arranjo = 0; arranjo < Duplas; arranjo++)
        {
            using var ctx = TestInfra.NovoContexto();
            var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: Duplas);
            torneio.DataInicio = SextaDeAbertura;
            await ctx.SaveChangesAsync();

            // Marcado ANTES do sorteio, que é o fluxo real: a dupla escolhe (e paga) a janela
            // na inscrição, muito antes de existir chave. As impedidas saem em sequência a
            // partir de `arranjo`, dando a volta — é o que gira o arranjo sem mudar o volume.
            var todas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id)
                .OrderBy(d => d.Id).ToListAsync();
            for (int i = 0; i < impedidas; i++)
                todas[(arranjo + i) % Duplas].ImpedimentoSextaNoite = true;
            await ctx.SaveChangesAsync();

            await TestInfra.NovoTorneiosController(ctx, organizador.Id).GerarChaves(torneio.Id);

            var furos = await FurosDeImpedimentoAsync(ctx, torneio);
            if (furos.Count > 0) comFuro.Add($"arranjo {arranjo}: {string.Join("; ", furos)}");
        }

        Assert.True(comFuro.Count == 0,
            $"{comFuro.Count}/{Duplas} arranjos furaram o impedimento com {impedidas} de "
            + $"{Duplas} duplas impedidas:\n{string.Join("\n", comFuro.Take(5))}");
    }

    // Todo jogo marcado DENTRO de uma janela que a dupla pagou pra evitar.
    private static async Task<List<string>> FurosDeImpedimentoAsync(DbPadelContext ctx, Torneio torneio)
    {
        var cheio = await ctx.Torneios
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
            .FirstAsync(t => t.Id == torneio.Id);
        var janelas = JanelasDeImpedimento.PorDupla(cheio);
        var jogos = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null)
            .ToListAsync();

        return (from jogo in jogos
                from duplaId in new[] { jogo.Dupla1Id, jogo.Dupla2Id }
                where janelas.TryGetValue(duplaId, out var janelasDaDupla)
                      && janelasDaDupla.Any(j => jogo.HorarioPrevisto >= j.Inicio
                                              && jogo.HorarioPrevisto < j.Fim)
                select $"dupla {duplaId} em {jogo.HorarioPrevisto:dd/MM HH:mm}").ToList();
    }
}
