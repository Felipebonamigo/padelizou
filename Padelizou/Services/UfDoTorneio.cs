using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// "Em que estado este torneio acontece?" — pergunta que o Padelizou nunca fez ao organizador,
// e que agora precisa de resposta pra mirar o aviso de torneio novo.
//
// ⚠️ NÃO EXISTE um campo de UF no torneio. Conferido em produção (10/08/2026): os 3 torneios
// reais têm clube, mas o `Clube.CidadeId` é NULO em todos os três — os clubes nasceram pelo
// "não achou? escreva o nome do seu clube", que não pergunta cidade. Então a corrente de
// respostas abaixo não é preciosismo: sem o segundo elo, a mira não funcionaria pra nenhum
// torneio que existe hoje.
public static class UfDoTorneio
{
    // Devolve a sigla, ou **null quando não dá pra saber** — e null aqui obriga quem chama a
    // mandar o aviso pra todo mundo. Encolher alcance por falta de dado é o defeito silencioso
    // que essa função existe pra não cometer.
    public static async Task<string?> DescobrirAsync(DbPadelContext context, int torneioId)
    {
        // 1) O clube onde o torneio acontece. É a resposta mais confiável quando existe: é o
        //    lugar de verdade, não onde alguém mora.
        var ufDoClube = await context.Torneios
            .Where(t => t.Id == torneioId && t.Clube != null && t.Clube.CidadeId != null)
            .Select(t => t.Clube!.Cidade!.Estado)
            .FirstOrDefaultAsync();

        if (UnidadeFederativa.Normalizar(ufDoClube) is { } peloClube) return peloClube;

        // 2) Quem organiza. Um torneio é quase sempre organizado por quem mora perto dele, e
        //    hoje é o ÚNICO elo que responde — os organizadores dos 3 torneios reais têm o
        //    estado preenchido, os clubes deles não têm cidade.
        //
        //    ⚠️ Um torneio pode ter vários organizadores (o 23 tem nove). Pega o primeiro que
        //    tem estado preenchido, em ordem estável de JogadorId — a tabela tem chave
        //    composta e nenhuma coluna de ordem. Sem o ORDER BY, duas execuções poderiam
        //    mirar estados diferentes pro mesmo torneio.
        var ufsDosOrganizadores = await context.TorneioOrganizadores
            .Where(o => o.TorneioId == torneioId)
            .OrderBy(o => o.JogadorId)
            .Select(o => o.Jogador.Estado)
            .ToListAsync();

        foreach (var escrito in ufsDosOrganizadores)
            if (UnidadeFederativa.Normalizar(escrito) is { } peloOrganizador)
                return peloOrganizador;

        return null;
    }
}
