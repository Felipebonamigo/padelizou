using System.Security.Cryptography;
using Padelizou.Models;

namespace Padelizou.Services;

// O convite que fecha a dupla sem ninguém precisar do CPF do outro.
//
// Por que existe: pra definir o parceiro era obrigatório digitar os 11 dígitos do CPF dele
// (e o nome, se ele ainda não tivesse cadastro). Ninguém sabe o CPF do parceiro de cabeça —
// então inscrever a dupla exigia uma conversa por fora ("me manda teu CPF") antes de o site
// conseguir ajudar. Era o maior atrito da inscrição. Com o convite, quem se inscreveu manda
// um link; quem recebe entra com a PRÓPRIA conta e aceita — o CPF dele já está lá, e ninguém
// digita o documento de outra pessoa.
//
// De quebra fecha um furo de privacidade: o formulário de CPF aceitava qualquer número, então
// dava pra inscrever alguém que nunca pediu isso — e, se o CPF não tivesse cadastro, criar uma
// conta no nome dele. No convite quem entra na dupla é sempre quem clicou.
public static class ConviteDeParceiro
{
    // 32 bytes em base64url: o link vai por WhatsApp, e um número curto seria chutável.
    public static string NovoToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    // O convite vale enquanto: existe token, a dupla ainda não tem parceiro e as inscrições
    // do torneio estão abertas.
    //
    // Não há prazo em dias de propósito: o fim das inscrições JÁ é o prazo natural, e um
    // prazo menor faria o link morrer com o torneio ainda aberto — quem tentasse usar veria
    // "convite expirado" sem entender por quê, num torneio em que ainda dá pra se inscrever.
    // Aceitar o convite limpa o token, então um link usado não fecha uma segunda dupla.
    public static bool Valido(Dupla? dupla, string? statusDoTorneio, string? token)
    {
        if (dupla == null || string.IsNullOrWhiteSpace(token)) return false;
        if (string.IsNullOrWhiteSpace(dupla.ConviteToken)) return false;
        if (dupla.Jogador2Id != null) return false;
        if (statusDoTorneio != "Inscrições Abertas") return false;

        // Comparação em tempo fixo: o token é um segredo, e comparar com == vaza o tamanho
        // do prefixo acertado pelo tempo de resposta.
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(dupla.ConviteToken),
            System.Text.Encoding.UTF8.GetBytes(token));
    }

    // Por que o convite não serve mais — a mensagem que a pessoa lê ao abrir um link velho.
    // Distinguir os motivos importa: "já tem parceiro" e "inscrições encerradas" levam a
    // ações diferentes (falar com quem convidou × procurar outro torneio).
    public static string MotivoDeNaoValer(Dupla? dupla, string? statusDoTorneio)
    {
        if (dupla == null) return "Esse convite não existe mais.";
        if (dupla.Jogador2Id != null) return "Essa dupla já está completa — alguém aceitou antes.";
        if (statusDoTorneio != "Inscrições Abertas") return "As inscrições deste torneio já foram encerradas.";
        return "Esse convite não vale mais. Peça um link novo pra quem te convidou.";
    }
}
