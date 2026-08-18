using Padelizou.Models;

namespace Padelizou.Services;

// QUEM VENCEU UM JOGO DE PANELINHA — e por que isso deixou de ser uma conta.
//
// Até 18/08/2026 `JogoSemanal` não tinha coluna de vencedor: quem venceu era DEDUZIDO do
// placar (`GamesDupla1 > GamesDupla2`). Funcionava enquanto o placar era obrigatório.
//
// ⚠️ A MUDANÇA VEIO DE UM PEDIDO DE USUÁRIO: registrar jogo sem precisar digitar placar
// ("montar dupla 1 → dupla 2 → marcar vencedor → salvar"). Com o vencedor deduzido, salvar
// sem placar gravaria 0 x 0 — e 0 x 0 **é empate**, 2 pontos pra cada um. O ranking da
// panelinha iria se enchendo de empates que ninguém jogou, sem erro nenhum na tela, e o
// sintoma apareceria semanas depois como "o ranking está estranho".
//
// Então o fato inverteu de lugar: o VENCEDOR passa a ser gravado, e o placar virou o detalhe
// opcional. É a decisão contrária à do W.O. do torneio, tomada no mesmo dia — e de propósito:
// lá tudo lê o placar (classificação de grupo, chaveamento), então tirar o placar quebraria a
// leitura; aqui quem lê é só a pontuação, e ela passa a ler o vencedor direto.
public static class ResultadoDoJogoSemanal
{
    public const int Empate = 0;
    public const int Dupla1 = 1;
    public const int Dupla2 = 2;

    // O vencedor DEDUZIDO do placar — o que a propriedade calculada fazia antes.
    //
    // Continua existindo por dois motivos: é a régua do backfill da migração (todo jogo antigo
    // tem placar, então o vencedor de todos eles é dedutível sem perda), e é com ela que se
    // confere se um placar informado contradiz o vencedor marcado.
    public static int LadoPeloPlacar(int? games1, int? games2)
    {
        if (games1 == null || games2 == null) return Empate;
        return games1 == games2 ? Empate : games1 > games2 ? Dupla1 : Dupla2;
    }

    public static bool LadoValido(int lado) => lado is Empate or Dupla1 or Dupla2;

    // ⚠️ PLACAR QUE CONTRADIZ O VENCEDOR É RECUSADO, não "corrigido em silêncio".
    //
    // A tentação é deixar um dos dois mandar. Mas os dois vêm da mesma tela e da mesma pessoa:
    // se ela marcou "Dupla 1 venceu" e digitou 3 x 6, um dos dois cliques foi errado, e o
    // sistema não tem como saber qual. Escolher sozinho grava um resultado que ninguém pediu —
    // e esse tipo de "conserto" silencioso já deixou uma final de torneio com o campeão errado
    // neste projeto (ver Services/QuemVenceu). Devolver a pergunta custa um toque e não mente.
    public static string? MotivoParaNaoSalvar(int vencedorLado, int? games1, int? games2)
    {
        if (!LadoValido(vencedorLado))
            return "Marque quem venceu antes de salvar.";

        // Placar é opcional, mas é opcional INTEIRO: um lado só não diz nada.
        if ((games1 == null) != (games2 == null))
            return "Informe os games das duas duplas — ou deixe o placar em branco.";

        if (games1 == null) return null;

        if (games1 < 0 || games2 < 0)
            return "Games não podem ser negativos.";

        var peloPlacar = LadoPeloPlacar(games1, games2);
        if (peloPlacar == vencedorLado) return null;

        return peloPlacar == Empate
            ? $"O placar {games1} x {games2} está empatado, mas você marcou uma dupla como vencedora. Ajuste um dos dois."
            : "O placar diz que a OUTRA dupla venceu. Ajuste o placar ou o vencedor.";
    }

    // Grava o resultado no jogo. Usado pelo registrar E pelo editar — os dois, sempre.
    //
    // ⚠️ Editar tem que passar por aqui também. Enquanto o vencedor era calculado, corrigir o
    // placar acertava o vencedor de graça; agora ele é um campo, e um editar que só escrevesse
    // os games deixaria o vencedor GRAVADO apontando pro lado antigo. O jogo mostraria 6x3 com
    // a outra dupla marcada como vencedora, e o ranking seguiria o vencedor.
    public static void Aplicar(JogoSemanal jogo, int vencedorLado, int? games1, int? games2)
    {
        jogo.VencedorLado = vencedorLado;
        jogo.GamesDupla1 = games1;
        jogo.GamesDupla2 = games2;
    }

    // O placar pra tela: "6 x 3", ou nulo quando o jogo foi registrado sem placar.
    public static string? Placar(JogoSemanal jogo) =>
        jogo.GamesDupla1 == null || jogo.GamesDupla2 == null
            ? null
            : $"{jogo.GamesDupla1} x {jogo.GamesDupla2}";
}
