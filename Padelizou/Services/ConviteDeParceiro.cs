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

    // O convite vale enquanto: existe token, a dupla ainda não tem parceiro e a janela de
    // definir parceiro não fechou (ver Services/JanelaDoParceiro).
    //
    // Não há prazo em dias de propósito: a janela JÁ é o prazo natural, e um prazo menor faria
    // o link morrer com o torneio ainda aberto — quem tentasse usar veria "convite expirado"
    // sem entender por quê. Aceitar o convite limpa o token, então um link usado não fecha uma
    // segunda dupla.
    //
    // ⚠️ A JANELA DEIXOU DE SER "INSCRIÇÕES ABERTAS" (09/09/2026): a dupla sem parceiro passou
    // a entrar na chave, e o link precisa continuar valendo até a bola rolar pra ela — é o
    // caminho mais comum de fechar a vaga em cima da hora (o dono manda o link no WhatsApp).
    //
    // ⚠️ `jaComecouAJogar` NÃO TEM VALOR PADRÃO, pelo mesmo motivo escrito no MuralDeParceiros:
    // `false` é o valor PERMISSIVO, e um chamador esquecido abriria o convite sozinho, calado.
    public static bool Valido(Dupla? dupla, string? statusDoTorneio, string? token, bool jaComecouAJogar)
    {
        if (dupla == null || string.IsNullOrWhiteSpace(token)) return false;
        if (string.IsNullOrWhiteSpace(dupla.ConviteToken)) return false;
        if (JanelaDoParceiro.MotivoParaNaoDefinir(dupla, statusDoTorneio, jaComecouAJogar) != null) return false;

        return TokenConfere(dupla.ConviteToken, token);
    }

    // Comparação em tempo fixo: o token é um segredo, e comparar com == vaza o tamanho do
    // prefixo acertado pelo tempo de resposta.
    //
    // Público porque o convite do anúncio de desafio usa o MESMO token e precisa da MESMA
    // comparação (ver ConviteDoAnuncio). A regra de validade é outra lá — o que não pode
    // virar segunda cópia é a criptografia.
    public static bool TokenConfere(string? guardado, string? recebido)
    {
        if (string.IsNullOrWhiteSpace(guardado) || string.IsNullOrWhiteSpace(recebido)) return false;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(guardado),
            System.Text.Encoding.UTF8.GetBytes(recebido));
    }

    // Por que o convite não serve mais — a mensagem que a pessoa lê ao abrir um link velho.
    // Distinguir os motivos importa: "já tem parceiro" e "a dupla já jogou" levam a ações
    // diferentes (falar com quem convidou × procurar outro torneio).
    public static string MotivoDeNaoValer(Dupla? dupla, string? statusDoTorneio, bool jaComecouAJogar)
    {
        if (dupla == null) return "Esse convite não existe mais.";
        if (dupla.Jogador2Id != null) return "Essa dupla já está completa — alguém aceitou antes.";
        if (JanelaDoParceiro.MotivoParaNaoDefinir(dupla, statusDoTorneio, jaComecouAJogar) is { } foraDaJanela)
            return foraDaJanela;
        return "Esse convite não vale mais. Peça um link novo pra quem te convidou.";
    }
}
