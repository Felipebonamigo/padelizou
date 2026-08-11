namespace Padelizou.Services;

// O Marcar Jogo está ESCONDIDO até termos um clube. O módulo continua inteiro no código, roda
// em produção e não aparece pra ninguém — só pra quem é admin do Padelizou.
//
// Por que esconder algo que já funciona: a tela lista os clubes que ligaram a agenda de
// quadras, e hoje esse número é zero. Uma porta que abre numa lista vazia não é uma promessa
// pendente, é uma promessa quebrada — o jogador entra uma vez, não vê clube nenhum e aprende
// que aqui não se marca quadra. Melhor não oferecer do que oferecer vazio.
//
// Como ligar (systemd, uma linha no Environment= do unit):
//   MarcarJogo__Habilitado=true
//
// ⚠️ A chave é MANUAL de propósito (decisão do Felipe, 11/08): o módulo NÃO volta sozinho no
// dia em que algum clube ligar a agenda. Quem escolhe a hora de reabrir é o Felipe — o
// primeiro clube a aparecer na vitrine é o cartão de visitas dela, e isso não se decide por
// acidente de configuração de terceiro.
public class MarcarJogoSettings
{
    // false = escondido: só admin do Padelizou enxerga o menu e a vitrine de clubes.
    // true  = liberado pros jogadores.
    public bool Habilitado { get; set; }
}
