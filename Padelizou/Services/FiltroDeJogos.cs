using Padelizou.Models;

namespace Padelizou.Services;

// A SEQUÊNCIA DE JOGOS POR CLUBE, POR QUADRA E POR FASE (10/09/2026).
//
// 🗣️ Felipe, num print da aba Jogos do 2º Etapa ER Padel Tour — 88 jogos em dois clubes:
// *"Crie uma função aqui, para poder [ver] a sequencia de jogos, Por clube, por quadra, por
// fase (quartas, semi, etc)"*. A aba já filtrava por time e por categoria; faltava o recorte de
// quem OPERA o dia: o balcão do Radar quer só o que acontece no Radar, quem cuida da Arena 1
// quer a fila DELA, e quem acompanha o mata-mata quer só as quartas.
//
// É um filtro de TELA, aplicado em memória (TorneiosController.CarregarViewBagJogosAsync)
// depois dos filtros que viram SQL, e pelo mesmo motivo do "meus jogos": ele recorta o que a
// lista mostra, não o que a projeção das próximas fases e a classificação precisam.
//
// ⚠️ A MESMA RÉGUA PROS DOIS TIPOS DE LINHA da aba: o jogo real (Partida) e a prévia
// (ProximasFasesDaChave.JogoQueVem). Sem isto, filtrar por "Semifinal" mostraria as duas
// semifinais reais e, logo abaixo, a Final prevista — a lista discordando do próprio filtro.
//
// ⚠️ O CLUBE de um jogo é o que a ETIQUETA da linha escreve (LugarDoJogo.ClubeDoJogo): a quadra
// manda; sem quadra, o carimbo do motor (Partida.ClubeId); sem os dois, o que a categoria já
// determina. Reescrever a precedência aqui criaria duas verdades — filtrar por "Radar" e ver
// uma linha etiquetada "Er Padel".
public sealed record FiltroDeJogos(int? ClubeId = null, string? Quadra = null, string? Fase = null)
{
    public static readonly FiltroDeJogos Nenhum = new();

    // O select manda "" quando ninguém escolheu; espaço em branco também não é escolha.
    public bool Ativo => ClubeId != null || !string.IsNullOrWhiteSpace(Quadra) || !string.IsNullOrWhiteSpace(Fase);

    public bool Aceita(SedesDoTorneio? sedes, Partida jogo) =>
        Aceita(sedes, jogo.NomeQuadra, jogo.CategoriaId, jogo.ClubeId, jogo.Fase);

    // A prévia não tem carimbo: o motor só grava o clube quando o jogo nasce.
    public bool Aceita(SedesDoTorneio? sedes, ProximasFasesDaChave.JogoQueVem previa) =>
        Aceita(sedes, previa.Quadra, previa.CategoriaId, clubeCarimbado: null, previa.Fase);

    private bool Aceita(SedesDoTorneio? sedes, string? nomeQuadra, int? categoriaId, int? clubeCarimbado, string? fase)
    {
        if (ClubeId is { } clube && LugarDoJogo.ClubeDoJogo(sedes, nomeQuadra, categoriaId, clubeCarimbado) != clube)
            return false;

        // Jogo por ordem (sem quadra) não está em quadra nenhuma — não passa por nenhuma.
        if (!string.IsNullOrWhiteSpace(Quadra)
            && !string.Equals(Quadra.Trim(), (nomeQuadra ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(Fase) && !MesmaFase(Fase, fase))
            return false;

        return true;
    }

    // "Fase de Grupos" é UMA escolha e cobre "Grupo A", "Grupo B" e o nome antigo dos seeds —
    // ver FasesTorneio, o único lugar que conhece as duas formas.
    private static bool MesmaFase(string escolhida, string? fase) =>
        escolhida.Trim() == FasesTorneio.FaseDeGrupos
            ? FasesTorneio.EhFaseDeGrupos(fase)
            : string.Equals(escolhida.Trim(), fase?.Trim(), StringComparison.OrdinalIgnoreCase);

    // As fases que a tela oferece, a partir das que o torneio TEM (jogos reais e prévias): os
    // grupos viram uma escolha só, o mata-mata sai na ordem da chave, e o que não se reconhece
    // (as rodadas do Americano) vem depois, por nome. Com uma só, a tela não mostra o select.
    public static IReadOnlyList<(string Valor, string Rotulo)> FasesParaEscolher(IEnumerable<string?> fases) =>
        fases
            .OfType<string>()
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => FasesTorneio.EhFaseDeGrupos(f) ? FasesTorneio.FaseDeGrupos : f.Trim())
            .Distinct()
            .OrderBy(Ordem)
            // O NÚMERO NO FIM DO NOME ORDENA COMO NÚMERO. As rodadas do Americano ("Americano
            // Rodada 1".."Rodada 11") não estão na corrente do mata-mata e caíam no desempate
            // por nome, que põe a "Rodada 10" antes da "Rodada 2" — quem opera a mesa procurando
            // a rodada seguinte a achava no meio da lista.
            .ThenBy(NumeroNoFim)
            .ThenBy(f => f, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => (Valor: f, Rotulo: f == FasesTorneio.FaseDeGrupos ? "Fase de grupos" : f))
            .ToList();

    // O inteiro que termina o nome, ou int.MaxValue quando não termina em número — aí quem
    // desempata é o nome, como antes.
    private static int NumeroNoFim(string fase)
    {
        var fim = fase.Length;
        while (fim > 0 && char.IsAsciiDigit(fase[fim - 1])) fim--;

        return fim < fase.Length && int.TryParse(fase.AsSpan(fim), out var numero) ? numero : int.MaxValue;
    }

    // Grupos antes de tudo; depois a corrente do mata-mata (ChaveamentoMataMata.ProximaFase),
    // que é a única que sabe que "Primeira Rodada" vem antes de "Oitavas de Final".
    private static int Ordem(string fase)
    {
        if (fase == FasesTorneio.FaseDeGrupos) return 0;

        var posicao = 1;
        for (string? f = ChaveamentoMataMata.PrimeiraRodada; f != null; f = ChaveamentoMataMata.ProximaFase(f), posicao++)
            if (f == fase) return posicao;

        return int.MaxValue;
    }
}
