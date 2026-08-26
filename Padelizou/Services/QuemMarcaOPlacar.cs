using Padelizou.Models;

namespace Padelizou.Services;

// Quem pode marcar o placar dos jogos — escolha do ORGANIZADOR, torneio a torneio.
//
// Nasceu em 26/08/2026 (Felipe), junto da comparação com o concorrente que só marca placar:
// tem organizador que quer a mesa controlando tudo (o padrão de sempre), tem interno de
// clube em que os próprios jogadores lançam o jogo da quadra, e tem Americano de amigos em
// que qualquer inscrito resolve.
//
// ⚠️ O que a escolha abre é SÓ marcar placar e iniciar o jogo (ControlePlacar e
// ColocarNoAr). W.O., reabrir jogo encerrado, voltar pra agendado e mexer em quadra ou
// transmissão continuam da organização em QUALQUER modo — jogador registrando W.O. contra
// o adversário, ou reabrindo o jogo que perdeu, é briga na certa. Errou o placar e
// encerrou? Reabrir é da organização: essa é a rede de segurança dos modos abertos.
//
// A organização (organizadores, marcadores, admin do Padelizou) entra SEMPRE, em qualquer
// modo — a escolha só soma gente, nunca tira. Ver PartidasController.PodeControlarPlacarAsync
// (a régua cheia da mesa) e PodeMarcarPlacarAsync (a régua que esta escolha abre).
public static class QuemMarcaOPlacar
{
    // Os nomes gravados em Torneio.QuemMarcaPlacar — mesmo desenho de
    // FormaDePagamentoDoTorneio: constantes num lugar só, nunca texto solto.
    public const string Organizacao = "Organizacao";
    public const string JogadoresEmQuadra = "JogadoresEmQuadra";
    public const string Inscritos = "Inscritos";

    // POST montado à mão pode mandar qualquer texto. Um modo desconhecido não pode abrir
    // nada — e também não pode ser gravado, senão a tela de gestão mostra um rádio vazio.
    public static bool Existe(string? modo) =>
        modo is Organizacao or JogadoresEmQuadra or Inscritos;

    // Os 4 em quadra: precisa das navegações Dupla1/Dupla2 carregadas — nas listas de jogos
    // elas sempre vêm (a tela mostra os nomes). Serve à VIEW decidir se desenha o lápis;
    // quem manda de verdade é a checagem no servidor, que refaz a pergunta no banco.
    public static bool EstaEmQuadra(int? jogadorId, Partida jogo) =>
        jogadorId != null
        && (jogo.Dupla1?.Jogador1Id == jogadorId || jogo.Dupla1?.Jogador2Id == jogadorId
         || jogo.Dupla2?.Jogador1Id == jogadorId || jogo.Dupla2?.Jogador2Id == jogadorId);

    // A régua dos modos, pura, pra view e pro teste. `ehInscritoOuAssistente` é a resposta
    // (já consultada) de "este jogador tem inscrição neste torneio fora da lista de espera,
    // ou é assistente do Padelizou?" — quem está em quadra entra no modo Inscritos mesmo sem
    // inscrição própria (dupla de TIME e chave direta jogam sem se inscrever pelo site).
    public static bool Liberado(string? modo, int? jogadorId, Partida jogo, bool ehInscritoOuAssistente) =>
        modo switch
        {
            JogadoresEmQuadra => EstaEmQuadra(jogadorId, jogo),
            Inscritos => ehInscritoOuAssistente || EstaEmQuadra(jogadorId, jogo),
            _ => false,
        };
}
