using Padelizou.Models;

namespace Padelizou.Services;

// "CONVIDAR" — o link de inscrição pronto pra mandar no privado.
//
// Pedido do Felipe (04/09/2026), num print da página do torneio com a área ao lado do "Cartaz
// pra divulgar" marcada de verde: *"crie um botão 'convidar' e que é um botão que direciona o
// link para a pessoa se inscrever no torneio"*.
//
// ⚠️ NÃO É O CARTAZ DE NOVO, e é por isso que existe. O cartaz resolve a divulgação ABERTA —
// story, feed, mural do clube — e para em imagem: pra chamar UMA pessoa o organizador teria
// que baixar a arte, achar a conversa e anexar. O convite é a divulgação DIRIGIDA, a que ele
// faz hoje na mão colando o link no WhatsApp. Os dois convivem no mesmo bloco da página, e o
// cartaz continua carregando o QR (`CartoesController.CartazImagem`).
//
// ⚠️ ESTE ARQUIVO NÃO MANDA NADA. Ele monta o texto que o `wa.me` abre na conversa do próprio
// organizador — mesma régua de `ConviteDaAulaMarcada` e `CobrancaDasAulasEmAberto`: disparo
// automático pra número que nunca consentiu é como um chip pré-pago é denunciado e morre.
public static class ConviteProTorneio
{
    // Este torneio ganha o botão "Convidar"?
    //
    // ⚠️ A RÉGUA NÃO É `DivulgacaoDoTorneio.PodeDivulgar`, e reusar aquela seria o defeito.
    // O cartaz vale pro torneio EM ANDAMENTO — ele só troca de conversa ("ACOMPANHE AO VIVO"),
    // porque assistir também é convite. O botão de convidar promete INSCRIÇÃO, e a aba
    // "Inscreva-se" só existe enquanto o status é "Inscrições Abertas" (ver Details.cshtml).
    // Oferecê-lo fora disso manda a pessoa pra uma página onde o botão prometido não está.
    public static bool PodeConvidar(string? status) => status == PortaDaInscricao.Aberta;

    // O texto pronto. Curto porque é WhatsApp: o organizador ainda edita antes de mandar, já
    // que o `wa.me` só abre a conversa com o texto no campo.
    //
    // `local` vem de fora (o Details lê o clube por ViewBag, não por navegação — ver o
    // comentário lá) e pode faltar sem estragar a frase.
    public static string Texto(Torneio torneio, string? local, string link)
    {
        // ⚠️ "Data a definir" NÃO entra aqui, e a diferença é de contexto: no card da listagem
        // ela é informação (a pessoa está comparando torneios e precisa saber que este ainda
        // não tem dia); numa mensagem de WhatsApp ela lê como "ainda não vale a pena olhar".
        // Sem data, o convite não fala de data — o link leva pra página, que diz tudo.
        var contexto = new List<string>();
        if (DataDoTorneioNaTela.TemData(torneio)) contexto.Add(DataDoTorneioNaTela.Frase(torneio));
        if (!string.IsNullOrWhiteSpace(local)) contexto.Add(local.Trim());

        // A linha do meio SOME INTEIRA quando não há nem data nem local. O `string.Join` já não
        // deixa separador pendurado — o que sobraria é o `\n`, e uma linha vazia entre o nome do
        // torneio e o link faz a mensagem parecer cortada no envio.
        var quandoEOnde = contexto.Count > 0 ? string.Join(" · ", contexto) + "\n" : "";

        return $"Bora jogar? {torneio.Nome} está com inscrições abertas no Padelizou.\n"
             + quandoEOnde
             + $"Dá pra se inscrever por aqui: {link}";
    }
}
