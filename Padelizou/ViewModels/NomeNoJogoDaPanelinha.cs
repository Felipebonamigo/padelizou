using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// Um dos quatro nomes de um jogo de panelinha, pra tela: ou a pessoa com link pro perfil, ou a
// palavra "Convidado" sem link.
//
// ⚠️ CARREGA O ID **E** A NAVEGAÇÃO de propósito, e é por isso que este tipo existe em vez de a
// view receber só um `Jogador?`. Com só a navegação, "é convidado" e "esqueci o `.Include()`"
// viram o mesmo `null` — e o segundo passaria a imprimir "Convidado" por cima do nome de um
// membro real, sem erro e sem log. Quem separa os dois é a guarda de ConvidadoNoJogo, que estoura
// no caso do Include esquecido. Ver Views/Shared/_NomeNoJogoDaPanelinha.cshtml.
public record NomeNoJogoDaPanelinha(int? Id, Jogador? Jogador)
{
    // SÓ O APELIDO (nome curto pra quem não tem) desde 26/08/2026 — pedido do Felipe num print
    // da lista: "pode ser só o apelido". A linha tem quatro nomes disputando espaço com data,
    // placar e dois botões, e quem lê está DENTRO da turma. O porquê de isso não contradizer a
    // decisão de 06/08 (apelido sozinho saiu das telas) está em NomeBonito.ApelidoOuCurto.
    public string Texto => ConvidadoNoJogo.ApelidoNaTela(Id, Jogador);
}
