using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// SALVAR O PREÇO NA GESTÃO LEVA JUNTO QUEM AINDA NÃO PAGOU (18/11/2026).
//
// 🗣️ Rafael (3ª ETAPA WALLAU), pelo Felipe: a cobrança saiu com R$ 110,00 por pessoa num
// torneio de R$ 140,00 — *"alterei lá e não mudou nada"*. Cada inscrição guarda o valor do dia
// em que foi feita (`ValorInscricao`), e trocar o preço não mexia nela. Decisão do Felipe:
// "Atualizar sozinho ao salvar".
//
// ⚠️ A RÉGUA: quem NÃO pagou nada passa pro preço atual; quem pagou fica com o combinado.
// "Pagou" é qualquer sinal de dinheiro: `Pago`, um jogador marcado à mão
// (PagamentoManualDoJogador), ou cobrança do site viva — confirmada, pendente ou em estorno
// (CobrancaDaDupla.AtivaDe). Mexer no total de quem pagou metade cobraria a diferença de quem
// já acertou; mexer em quem tem Pix do site aberto faria a tela e o Pix pedirem números diferentes.
//
// Roda a CADA salvamento, não só quando o número muda: na Wallau o preço já estava em 140 antes
// do conserto, e quem ficou pra trás só se alinha se salvar de novo alinhar. A conta é a mesma
// da inscrição (PrecoDaInscricao.Total), então salvar duas vezes não muda nada na segunda.
public static class ReajusteDoPrecoDasInscricoes
{
    public static async Task<int> AplicarAsync(DbPadelContext ctx, Torneio torneio)
    {
        int mudaram = 0;

        // Time é cadastrado pelo organizador e não paga pelo sistema (a mesma exceção de
        // QuemJaEstaNoTorneio). Valor nulo é inscrição anterior à coluna: quem soma já usa o
        // preço do torneio pra ela, não há o que alinhar.
        var duplas = await ctx.Duplas
            .Where(d => d.Categoria.TorneioId == torneio.Id && d.NomeTime == null)
            .OrderBy(d => d.Id)
            .ToListAsync();

        var ids = duplas.Select(d => d.Id).ToList();
        var comPagamentoManual = (await ctx.PagamentosManuaisDeJogador
            .Where(p => ids.Contains(p.DuplaId)).Select(p => p.DuplaId).ToListAsync()).ToHashSet();
        var comCobrancaDoSite = (await ctx.Pagamentos
            .Where(p => p.ReferenciaId != null && ids.Contains(p.ReferenciaId.Value)
                && (p.Tipo == "TorneioDupla" || p.Tipo == "TorneioPagarDepois")
                && (p.Status == "Confirmado" || p.Status == "Pendente" || p.Status == "AguardandoEstorno"))
            .Select(p => p.ReferenciaId!.Value).ToListAsync()).ToHashSet();

        // A SEGUNDA INSCRIÇÃO: quem já estava no torneio quando esta foi feita. A ordem de
        // criação (Id) responde isso sem gravar nada novo — é a mesma pergunta que a inscrição
        // fez na hora, refeita sobre quem continua inscrito.
        var jaVistos = new HashSet<int>();
        foreach (var dupla in duplas)
        {
            bool pagou = dupla.Pago || comPagamentoManual.Contains(dupla.Id) || comCobrancaDoSite.Contains(dupla.Id);
            if (!pagou && dupla.ValorInscricao is decimal atual)
            {
                var quemPaga = dupla.Jogador2Id is int j2
                    ? new[] { jaVistos.Contains(dupla.Jogador1Id), jaVistos.Contains(j2) }
                    : new[] { jaVistos.Contains(dupla.Jogador1Id) };
                int impedimentos = (dupla.ImpedimentoQuintaNoite ? 1 : 0) + (dupla.ImpedimentoSextaNoite ? 1 : 0)
                                 + (dupla.ImpedimentoSabadoManha ? 1 : 0) + (dupla.ImpedimentoSabadoTarde ? 1 : 0);

                var novo = PrecoDaInscricao.Total(torneio, quemPaga, impedimentos);
                if (novo != atual)
                {
                    dupla.ValorInscricao = novo;
                    mudaram++;
                }
            }

            jaVistos.Add(dupla.Jogador1Id);
            if (dupla.Jogador2Id is int j) jaVistos.Add(j);
        }

        // Americano individual. atalho: a ordem é só dentro desta tabela — um torneio com as duas
        // portas de inscrição ao mesmo tempo não existe hoje; se passar a existir, a pessoa que
        // está nas duas precisa de uma ordem comum (CriadoEm) pra saber qual foi a primeira.
        var individuais = await ctx.InscricoesAmericanas
            .Where(i => i.Categoria.TorneioId == torneio.Id)
            .OrderBy(i => i.Id)
            .ToListAsync();
        var individuaisComCobranca = (await ctx.Pagamentos
            .Where(p => p.TorneioId == torneio.Id && p.Tipo == "TorneioAmericano"
                && (p.Status == "Confirmado" || p.Status == "Pendente" || p.Status == "AguardandoEstorno"))
            .Select(p => p.JogadorId).ToListAsync()).ToHashSet();

        var individuaisVistos = new HashSet<int>();
        foreach (var inscricao in individuais)
        {
            if (!inscricao.Pago && !individuaisComCobranca.Contains(inscricao.JogadorId)
                && inscricao.ValorInscricao is decimal atual)
            {
                var novo = PrecoDaInscricao.PorPessoa(torneio, individuaisVistos.Contains(inscricao.JogadorId));
                if (novo != atual)
                {
                    inscricao.ValorInscricao = novo;
                    mudaram++;
                }
            }
            individuaisVistos.Add(inscricao.JogadorId);
        }

        return mudaram;
    }

    public static string Frase(int mudaram) => mudaram switch
    {
        0 => "",
        1 => " 1 inscrição que ainda não foi paga passou pro valor novo.",
        _ => $" {mudaram} inscrições que ainda não foram pagas passaram pro valor novo.",
    };
}
