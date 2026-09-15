namespace Padelizou.Models;

// Uma reação com emoji num jogo — 12/09/2026.
//
// 🗣️ Felipe: *"aqui, a cada jogo, permita a pessoa 'reagir' tipo o que tem aqui no discord, com
// emojis"*, com um print do Discord; e na sequência *"e ao clicar no emoji, veja quem colocou o
// que, igual no whats app"*, com o print do painel de reações do WhatsApp.
//
// ⚠️ A CHAVE COMPOSTA (PartidaId, JogadorId, Emoji) **É** A REGRA "uma reação por pessoa por
// emoji" — ela não está num `if` de C#. O toque duplo num alvo de 32px manda dois POSTs, e foi
// exatamente assim que o `DbUpdateException em POST /Partidas/Votar` apareceu em produção em
// 10/09. É o degrau 4 da escada do CLAUDE.md, o mesmo que a PK de `TorneioMarcador` já usa pra
// segurar o clique duplo no "adicionar marcador".
//
// ⚠️ E O EMOJI ENTRA NA CHAVE de propósito, diferente do palpite: lá o voto é UM (trocar de
// dupla troca a linha); aqui reagir com 🔥 não tira o 👏 — é o que Discord e WhatsApp fazem, e é
// o que o Felipe pediu.
public class ReacaoDaPartida
{
    public int PartidaId { get; set; }
    public virtual Partida Partida { get; set; } = null!;

    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    // O emoji já peneirado e normalizado por Services/EmojiDeReacao — UM grafema, na forma
    // longa. Nada chega aqui sem passar por lá: a paleta é livre (escolha do Felipe), então a
    // peneira do servidor é a única coisa entre o POST e o que o torneio inteiro lê no card.
    public string Emoji { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
