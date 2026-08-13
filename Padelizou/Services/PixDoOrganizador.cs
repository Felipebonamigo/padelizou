using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Services;

// O "por fora": o torneio não cobra pelo site, o organizador põe a chave Pix dele na tela e o
// dinheiro passa longe do Padelizou — a gente não recebe e não confere nada.
//
// Aqui mora quem responde DUAS perguntas que a tela sozinha respondia pela metade: quando o
// bloco aparece, e PRA QUEM o jogador manda o comprovante depois de pagar.
public static class PixDoOrganizador
{
    // A chave só existe no "por fora". Nas formas pelo site o jogador paga no checkout, e uma
    // chave na tela seria um segundo caminho pro mesmo dinheiro.
    //
    // ⚠️ Esta condição também guarda a consulta do contato no controller. Ela mora aqui pra
    // não existir uma segunda cópia: a view mostrando o bloco sem o controller ter buscado o
    // contato daria exatamente o que esta mudança veio consertar — o pedido de comprovante
    // sem ninguém pra quem mandar.
    public static bool Aparece(Torneio torneio) =>
        !torneio.CobraPeloSite
        && !string.IsNullOrWhiteSpace(torneio.ChavePixOrganizador)
        && torneio.PrecoInscricao > 0;

    // Quem recebe o comprovante é quem tem o caixa: o CRIADOR do torneio (ver
    // AcessoAoDinheiroDoTorneio). Ajudante organiza, mas não é pra quem o dinheiro vai.
    //
    // ⚠️ UM TORNEIO PODE TER MAIS DE UM "Criador" — nada no cadastro impede, e o painel admin
    // já registra isso. Sem uma ordem TOTAL, qual deles aparece mudaria de um carregamento
    // pro outro, e o jogador mandaria o comprovante pra pessoas diferentes a cada vez.
    // Ordena por Id: é estável e é o mais antigo, isto é, quem criou de fato.
    public static async Task<Jogador?> QuemRecebeOComprovanteAsync(DbPadelContext db, int torneioId) =>
        await db.TorneioOrganizadores
            .Where(o => o.TorneioId == torneioId && o.NivelAcesso == "Criador")
            .OrderBy(o => o.JogadorId)
            .Select(o => o.Jogador)
            .FirstOrDefaultAsync();

    // O texto que abre no WhatsApp já escrito. O comprovante em si é uma IMAGEM, que o
    // jogador anexa na mão — o wa.me não carrega arquivo. Por isso a frase avisa que ele vem
    // em seguida: sem isso o organizador recebe um "oi" solto e não sabe o que esperar.
    public static string Mensagem(string nomeDoTorneio, string? nomeDoJogador)
    {
        var quem = string.IsNullOrWhiteSpace(nomeDoJogador) ? "" : $" Sou {nomeDoJogador.Trim()}.";
        return $"Olá! Acabei de pagar a inscrição do {nomeDoTorneio} pelo Pix.{quem} "
             + "Estou mandando o comprovante aqui.";
    }
}
