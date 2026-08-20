using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O texto do palpite que valeu ponto, puro. Um texto por faixa da régua (PontosDoPalpite):
// quem cravou merece ouvir isso com todas as letras, e quem só acertou o vencedor não pode
// receber a frase de quem cravou.
public static class TextoDoPalpiteConferido
{
    public static TextoDoAviso Do(int pontos, string vencedores, string placar) => pontos switch
    {
        PontosDoPalpite.Cravou => new("Você CRAVOU o placar! 🎯",
            $"{vencedores} venceram por {placar}, exatamente como você disse. "
            + $"+{PontosDoPalpite.Cravou} pontos no Palpitrômetro."),
        PontosDoPalpite.ChegouPerto => new("Quase cravou! 🎯",
            $"Você acertou {vencedores} e o placar ({placar}) passou raspando. "
            + $"+{PontosDoPalpite.ChegouPerto} pontos no Palpitrômetro."),
        _ => new("Palpite certeiro! 🎯",
            $"{vencedores} venceram ({placar}), como você palpitou. "
            + $"+{PontosDoPalpite.SoOVencedor} ponto no Palpitrômetro."),
    };
}

// O RETORNO DO PALPITRÔMETRO: quem palpitou e ACERTOU fica sabendo na hora em que o jogo
// fecha — é o segundo ato que faltava pro palpite (a pessoa votava e nunca mais ouvia falar).
//
// Quem errou NÃO recebe nada, de propósito: "você errou" é o aviso que ensina a não palpitar
// mais. E quem estava EM QUADRA também não — é a régua do RankingDePalpiteiros (os quatro do
// jogo não pontuam no próprio jogo), e o aviso não pode dizer "+1 ponto" pra quem o ranking
// não paga.
public class AvisoDePalpiteConferido
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AvisoDePalpiteConferido(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    public async Task ConferirAsync(Partida partida, string? url)
    {
        if (partida.VencedorId == null) return;

        var palpites = await _context.PalpitesPartida
            .Where(v => v.PartidaId == partida.Id)
            .ToListAsync();
        if (palpites.Count == 0) return;

        var duplas = await _context.Duplas
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => d.Id == partida.Dupla1Id || d.Id == partida.Dupla2Id)
            .ToListAsync();

        // Quem estava em quadra — linha de TIME não exclui ninguém (o Jogador1 dela é quem
        // cadastrou o time), mesma regra do ranking.
        var emQuadra = duplas
            .Where(d => d.NomeTime == null)
            .SelectMany(d => new[] { (int?)d.Jogador1Id, d.Jogador2Id })
            .Where(id => id != null)
            .Select(id => id!.Value)
            .ToHashSet();

        var vencedora = duplas.FirstOrDefault(d => d.Id == partida.VencedorId);
        var vencedores = vencedora == null
            ? "Os vencedores"
            : vencedora.NomeTime
              ?? string.Join(" e ", new[] { vencedora.Jogador1?.ComoChamar, vencedora.Jogador2?.ComoChamar }
                  .Where(n => !string.IsNullOrWhiteSpace(n)));

        foreach (var palpite in palpites)
        {
            if (emQuadra.Contains(palpite.JogadorId)) continue;

            // ⚠️ A MOEDA É DITADA PELO PALPITE (games ou sets) — a mesma leitura do
            // RankingDePalpiteiros, senão o aviso pagaria um ponto que o ranking não paga.
            var palpitado = PlacaresPossiveis.Lido(
                palpite.GamesDupla1, palpite.GamesDupla2, palpite.SetsDupla1, palpite.SetsDupla2);

            int pontos = PontosDoPalpite.De(new PalpiteConferido
            {
                DuplaEscolhidaId = palpite.DuplaEscolhidaId,
                VencedorId = partida.VencedorId,
                PalpitouLado1 = palpitado.Lado1,
                PalpitouLado2 = palpitado.Lado2,
                PlacarLado1 = palpitado.EmSets ? partida.SetsDupla1 : partida.GamesDupla1,
                PlacarLado2 = palpitado.EmSets ? partida.SetsDupla2 : partida.GamesDupla2,
                EmSets = palpitado.EmSets,
                PorWo = partida.MotivoDoEncerramento == EncerramentoPorWo.Motivo,
            });

            if (pontos == 0) continue;

            // O placar na moeda em que o jogo fechou, do jeito da tela: games como sempre; o
            // jogo de sets mostra os sets.
            var placar = palpitado.EmSets
                ? $"{partida.SetsDupla1}x{partida.SetsDupla2}"
                : $"{partida.GamesDupla1}x{partida.GamesDupla2}";

            var texto = TextoDoPalpiteConferido.Do(pontos, vencedores, placar);
            await _push.EnviarParaJogadorAsync(palpite.JogadorId, texto.Titulo, texto.Corpo,
                url, AlcanceDoAviso.AppSemEmail);
        }
    }
}
