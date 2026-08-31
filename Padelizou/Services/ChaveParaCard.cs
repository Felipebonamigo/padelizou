using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Um confronto na chave. `Placar` nulo = jogo marcado e ainda não jogado — e ele APARECE
// assim de propósito: chave é o caminho, e "quem joga contra quem na semifinal" é justamente
// o que se posta ANTES de a semifinal acontecer.
public sealed record JogoDaChave(
    string Dupla1, string Dupla2, string? Placar, bool Dupla1Venceu, bool Dupla2Venceu);

public sealed record RodadaDaChave(string Fase, List<JogoDaChave> Jogos);

public sealed record ChaveDesenhavel(
    int CategoriaId,
    string Categoria,
    List<RodadaDaChave> Rodadas,
    string Torneio,
    string? Clube,
    DateTime? Data)
{
    public bool TemOQueMostrar => Rodadas.Any(r => r.Jogos.Count > 0);
}

// A CHAVE DO MATA-MATA, no formato que o desenho precisa.
//
// ⚠️ NO MÁXIMO TRÊS FASES, e é limite de física: 1080px divididos em quatro colunas dariam
// 250px por nome de dupla, e "Anderson / Charls" já ocupa isso. Uma chave de 16 entra a
// partir das QUARTAS — o "caminho até o título", que é o que se posta. Quem quer a chave
// inteira abre a tela, que tem rolagem; o card não tem.
//
// ⚠️ A ORDEM DOS JOGOS DENTRO DE UMA FASE É A DE CRIAÇÃO (Id crescente), que é a ordem em que
// o `RoboDoChaveamento` monta a chave. Não existe coluna de "posição no quadro" pra ler, e
// inventar uma aqui seria gravar um dado que ninguém mais mantém.
public static class ChaveParaCard
{
    public const int MaximoDeFases = 3;

    // Rede de segurança, NÃO fluxo esperado: uma caixa com um nome só e um vão embaixo parece
    // defeito de renderização, então o lado sem nome é dito por escrito.
    //
    // ⚠️ E ELE QUASE NUNCA DISPARA, por um motivo que vale saber: `Partida.Dupla1`/`Dupla2` são
    // navegações OBRIGATÓRIAS, então o `Include` delas vira INNER JOIN — a partida com dupla
    // ausente não chega aqui com um lado nulo, ela SOME da consulta inteira. Tem teste medindo
    // isso (`Partida_com_dupla_inexistente_some_da_chave`), porque o sumiço é do EF e a
    // próxima pessoa vai procurar o defeito neste arquivo.
    public const string VagaEmAberto = "A definir";

    // Da mais antiga pra final — a ordem em que a chave é lida, da esquerda pra direita.
    private static readonly string[] OrdemDasFases =
    {
        ChaveamentoMataMata.PrimeiraRodada,
        "Oitavas de Final",
        "Quartas de Final",
        "Semifinal",
        "Final",
    };

    public static async Task<ChaveDesenhavel?> DaCategoriaAsync(
        DbPadelContext contexto, int torneioId, int categoriaId)
    {
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Id, t.Nome, t.Status, t.ClubeId, t.DataInicio })
            .FirstOrDefaultAsync();

        // Mesma régua dos outros cards de torneio: cancelado não anuncia nada.
        if (torneio == null || !CampeoesDoTorneio.PodeAnunciar(torneio.Status)) return null;

        var categoria = await contexto.Categorias
            .AsNoTracking()
            .Where(c => c.Id == categoriaId && c.TorneioId == torneioId)
            .Select(c => new { c.Id, c.Nome })
            .FirstOrDefaultAsync();

        if (categoria == null) return null;

        var nomeDoClube = await contexto.Clubes
            .AsNoTracking()
            .Where(c => c.Id == torneio.ClubeId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync();

        var partidas = await contexto.Partidas
            .AsNoTracking()
            .Include(p => p.Dupla1).ThenInclude(d => d!.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d!.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d!.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d!.Jogador2)
            .Where(p => p.CategoriaId == categoriaId)
            .OrderBy(p => p.Id)
            .ToListAsync();

        // ⚠️ A PERGUNTA "É MATA-MATA?" É DO `ChaveamentoMataMata`, e não uma lista de strings
        // escrita aqui: ele é quem nomeia as fases quando o robô monta a chave, e uma segunda
        // lista ficaria pra trás no dia em que uma fase nova nascesse.
        var doMataMata = partidas
            .Where(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase))
            .ToList();

        var rodadas = OrdemDasFases
            .Select(fase => new { Fase = fase, Jogos = doMataMata.Where(p => p.Fase == fase).ToList() })
            .Where(r => r.Jogos.Count > 0)
            // As TRÊS ÚLTIMAS: `TakeLast` mantém a ordem de leitura da chave, e é a final que
            // nunca pode ficar de fora.
            .TakeLast(MaximoDeFases)
            .Select(r => new RodadaDaChave(
                r.Fase,
                r.Jogos.Select(p =>
                {
                    var venceu = QuemVenceu.Da(p);
                    return new JogoDaChave(
                        Dupla1: NomeOuVaga(p.Dupla1),
                        Dupla2: NomeOuVaga(p.Dupla2),
                        // Sem os dois lados marcados não há placar — meio placar não existe
                        // (a mesma régua do jogo de panelinha).
                        Placar: p.GamesDupla1 == null || p.GamesDupla2 == null
                            ? null
                            : $"{p.GamesDupla1} x {p.GamesDupla2}",
                        // ⚠️ Quem venceu sai de `QuemVenceu.Da`, a MESMA função que grava o
                        // `VencedorId` — nunca de `games1 > games2`, que diverge dela quando
                        // um lado está sem placar (cicatriz de 05/08/2026).
                        Dupla1Venceu: venceu != null && venceu == p.Dupla1Id,
                        Dupla2Venceu: venceu != null && venceu == p.Dupla2Id);
                }).ToList()))
            .ToList();

        return new ChaveDesenhavel(
            CategoriaId: categoria.Id,
            Categoria: categoria.Nome,
            Rodadas: rodadas,
            Torneio: torneio.Nome,
            Clube: nomeDoClube,
            Data: torneio.DataInicio);
    }

    // Nome vazio (dupla apagada) cai no mesmo texto da vaga em aberto: nos dois casos o card
    // não sabe quem é, e um espaço em branco no meio da árvore não conta isso a ninguém.
    private static string NomeOuVaga(Dupla? dupla)
    {
        if (dupla == null) return VagaEmAberto;
        var nome = NomeDaDupla.CompactoNa(dupla);
        return string.IsNullOrWhiteSpace(nome) ? VagaEmAberto : nome;
    }
}
