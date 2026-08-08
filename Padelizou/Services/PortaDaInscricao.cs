using Padelizou.Models;

namespace Padelizou.Services;

// A porta de inscrição do torneio: abrir e FECHAR, nos dois sentidos.
//
// Encerrar inscrição era de mão única. O organizador clicava uma vez e não havia caminho de
// volta — nem por engano, nem por motivo legítimo: o clube liberou mais uma quadra, apareceu
// mais uma dupla, ou ele só clicou antes de anunciar. A única saída era apagar o torneio e
// criar outro, perdendo o link já compartilhado.
//
// Aconteceu de verdade em 08/08/2026, com o **NATA PADEL TOUR** (id 22 de produção, do Lucas
// "Foka"): ficou parado em "Chaves em Sorteio" com **ninguém inscrito ainda** — inscrições
// encerradas antes de a primeira pessoa entrar, e sem botão pra desfazer.
//
// ⚠️ NÃO existe coluna nova aqui, de propósito. "Chaves em Sorteio" JÁ significa "inscrição
// fechada" — é isso que o formulário público consulta hoje. Uma flag `InscricoesAbertas` ao
// lado do Status criaria duas respostas pra mesma pergunta, e um dia elas discordariam: o
// torneio diria "abertas" numa tela e recusaria a inscrição na outra.
public static class PortaDaInscricao
{
    public const string Aberta = "Inscrições Abertas";

    // Fechada, mas antes do sorteio. O nome do status é histórico: ele nasceu descrevendo o
    // que vem DEPOIS de fechar (sortear), e não o que ele é (fechado).
    public const string Fechada = "Chaves em Sorteio";

    public static bool EstaAberta(Torneio torneio) => torneio.Status == Aberta;

    // Devolve o motivo da recusa, ou null quando dá pra ABRIR. Mesma forma de
    // CancelamentoDoTorneio.MotivoParaNaoCancelar: quem chama mostra a frase, não inventa uma.
    //
    // `jaSorteou` é "existe partida deste torneio". Vem de fora porque é consulta ao banco, e
    // uma propriedade calculada devolveria `false` calado em quem não trouxe a navegação —
    // aqui isso reabriria um torneio com a chave já publicada.
    public static string? PorQueNaoPodeAbrir(Torneio torneio, bool jaSorteou)
    {
        if (CancelamentoDoTorneio.EstaCancelado(torneio.Status))
            return "Este torneio está cancelado.";

        if (EstaAberta(torneio))
            return "As inscrições deste torneio já estão abertas.";

        // ⚠️ A trava que importa. Depois do sorteio a chave está publicada, os jogos existem e
        // tem gente sabendo contra quem joga: aceitar mais uma dupla aí dentro não é "reabrir
        // inscrição", é refazer o torneio por baixo de quem já se organizou.
        if (jaSorteou)
            return "As chaves já foram sorteadas — reabrir agora mudaria um torneio que já tem jogos definidos. "
                 + "Se ainda falta gente, apague as chaves antes.";

        // Qualquer outra fase (Fase de Grupos, Mata-Mata, Finalizado) só se alcança tendo
        // sorteado, então o caso acima já cobriu. Sobra "Chaves em Sorteio": pode abrir.
        return null;
    }

    public static string? PorQueNaoPodeFechar(Torneio torneio)
    {
        if (CancelamentoDoTorneio.EstaCancelado(torneio.Status))
            return "Este torneio está cancelado.";

        if (!EstaAberta(torneio))
            return "As inscrições deste torneio já estão fechadas.";

        return null;
    }

    // Torneio fechado que NUNCA recebeu ninguém não teve inscrição "encerrada" — ele ainda
    // não abriu. A tela dizia "Inscrições Encerradas! Agora você pode sortear os grupos" pra
    // um torneio com zero inscritos, que é a frase mais confusa possível na hora exata em que
    // o organizador está tentando entender por que ninguém consegue entrar.
    public static bool NuncaAbriu(Torneio torneio, bool temInscrito, bool jaSorteou) =>
        !EstaAberta(torneio) && !temInscrito && !jaSorteou;
}
