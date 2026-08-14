namespace Padelizou.Services;

// Pra quem a mensagem sai DE VERDADE neste ambiente — e-mail, WhatsApp e push.
//
// Existe por causa do dev. Pra ensaiar um torneio inteiro sem inventar gente, o dev roda com
// uma cópia dos dados de produção; e aí cada inscrição, cada chave publicada e cada placar
// lançado vira e-mail, WhatsApp e push no aparelho de gente de verdade — que não se inscreveu
// em nada, não vai jogar nada, e recebe o ensaio como se fosse o torneio de sábado.
//
// É uma LISTA DE PERMITIDOS, não um "desligar tudo": quem está ensaiando precisa receber os
// avisos do próprio ensaio, senão a metade do sistema que só existe no celular fica sem prova.
//
// ⚠️ VAZIO = TODO MUNDO RECEBE, e isso é de propósito. A lista mora na configuração de cada
// ambiente, e um dia sobe um ambiente sem ela: errar pro lado do silêncio calaria a PRODUÇÃO
// inteira sem uma linha no log — o tipo de falha muda que este projeto passou meses caçando
// (a cota do Gmail, o chip restringido, a chave do Asaas virando string vazia). Errar pro lado
// do envio é barulhento, e barulho a gente descobre no mesmo dia.
//
// Como ligar no dev (systemd, no Environment= do unit — a mesma prateleira do Site__Url):
//   Entrega__SoPara__0=felipe@exemplo.com
//   Entrega__SoPara__1=51999998888
public class EntregaSettings
{
    // E-mails e celulares que continuam recebendo. Vazio = ambiente sem restrição nenhuma.
    //
    // Os dois formatos na mesma lista de propósito: quem escreve isto é gente, não vai manter
    // duas colunas em dia, e o "@" separa um do outro sem ambiguidade nenhuma.
    public string[] SoPara { get; set; } = [];
}
