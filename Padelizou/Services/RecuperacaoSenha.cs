using System.Security.Cryptography;
using Padelizou.Models;

namespace Padelizou.Services;

// Regras do "esqueci minha senha", separadas do controller pra poderem ser testadas.
//
// Até 27/07/2026 não havia recuperação nenhuma: quem esquecia a senha se cadastrava de novo
// com o mesmo CPF, e o cadastro sobrescrevia a senha. Isso "funcionava" e era um buraco —
// CPF não é segredo no Brasil, então qualquer um que soubesse o seu assumia a sua conta.
// Fechar aquilo exigia criar isto aqui no mesmo movimento.
public static class RecuperacaoSenha
{
    // Curto de propósito: é tempo de abrir o e-mail, não de deixar o link vivo no histórico.
    public static readonly TimeSpan Validade = TimeSpan.FromHours(1);

    // 32 bytes de aleatoriedade criptográfica, em base64 seguro pra URL.
    public static string GerarToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static void Emitir(Jogador jogador, DateTime agora)
    {
        jogador.TokenRecuperacao = GerarToken();
        jogador.TokenRecuperacaoExpiraEm = agora.Add(Validade);
    }

    // Token válido é: existe, confere, e ainda não venceu.
    public static bool TokenValido(Jogador jogador, string? token, DateTime agora) =>
        !string.IsNullOrWhiteSpace(token)
        && !string.IsNullOrWhiteSpace(jogador.TokenRecuperacao)
        && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(jogador.TokenRecuperacao!),
            System.Text.Encoding.UTF8.GetBytes(token!))
        && jogador.TokenRecuperacaoExpiraEm.HasValue
        && jogador.TokenRecuperacaoExpiraEm.Value > agora;

    // Uso único: depois de trocar a senha, o link não serve mais.
    public static void Consumir(Jogador jogador)
    {
        jogador.TokenRecuperacao = null;
        jogador.TokenRecuperacaoExpiraEm = null;
    }

    // Regra mínima de senha, a mesma do cadastro e da redefinição.
    public static string? ProblemaComASenha(string? senha) => senha switch
    {
        null or "" => "Digite a nova senha.",
        { Length: < 6 } => "A senha precisa ter pelo menos 6 caracteres.",
        _ => null,
    };
}
