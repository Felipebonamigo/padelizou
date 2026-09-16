using Padelizou.Models;

namespace Padelizou.Services;

// O TORNEIO DE UM TIME SÓ: quem pode entrar, e a frase que explica a quem não pode.
//
// 🗣️ Felipe, 16/09/2026: "permitir criar o torneio restrito para um time, apenas quem estiver
// em um determinado time, poderia jogar". Ver Torneio.TimeExclusivoId.
//
// ⚠️ MORA AQUI, e não copiada nos dois controllers que inscrevem, porque é regra de QUEM
// ENTRA. A inscrição em dupla e a do Americano são caminhos separados (DuplasController.Create
// e TorneiosController.InscreverIndividual), e regra copiada num e esquecida no outro é
// exatamente como este projeto já produziu seus piores buracos — o Americano seguiu pontuando
// no ranking oficial por meses assim. Pior: o esquecimento aqui não quebra tela nenhuma, só
// deixa entrar quem não podia.
public static class TimeExclusivoDoTorneio
{
    // Uma pessoa da inscrição, como o controller a conhece na hora de decidir.
    //
    // `TemPerfil` existe separado de `TimeId != null` porque os dois "sem time" pedem
    // conselhos DIFERENTES: quem já tem conta aqui precisa vestir a camisa no perfil; quem
    // não tem conta nenhuma precisa criar uma antes. Uma frase só pros dois mandaria metade
    // das pessoas pra tela errada.
    public readonly record struct Pessoa(string Nome, int? TimeId, bool TemPerfil = true);

    public static bool Vale(Torneio torneio) => torneio.TimeExclusivoId != null;

    // Devolve o motivo da recusa, ou null quando a inscrição pode seguir. Mesma forma de
    // PortaDaInscricao.PorQueNaoPodeAbrir e de CancelamentoDoTorneio: quem chama MOSTRA a
    // frase, não inventa uma — senão cada tela explica a mesma recusa com outras palavras.
    public static string? MotivoDaRecusa(Torneio torneio, string? nomeDoTime, IEnumerable<Pessoa> pessoas)
    {
        if (!Vale(torneio)) return null;

        // Sem o nome do time em mãos (consulta que não trouxe o Include), a frase ainda
        // precisa sair inteira — "este torneio é só de um time" sem dizer QUAL é uma recusa
        // que não ensina nada.
        var time = string.IsNullOrWhiteSpace(nomeDoTime) ? "do time do torneio" : $"do time {nomeDoTime}";

        foreach (var pessoa in pessoas)
        {
            // ⚠️ TODAS as pessoas passam pela checagem, e não só a primeira: a dupla é
            // indivisível, e metade dela no time não é meio autorizada. Parar no primeiro
            // deixaria o dono do time entrar com qualquer amigo de fora — o furo exato que
            // esta trava existe pra fechar.
            if (pessoa.TimeId == torneio.TimeExclusivoId) continue;

            if (!pessoa.TemPerfil)
            {
                return $"Este torneio é só {time}, e {pessoa.Nome} ainda não tem perfil no Padelizou. "
                     + "Quem vai jogar precisa criar a conta e entrar no time antes de se inscrever.";
            }

            return $"Este torneio é só {time}, e {pessoa.Nome} não está nele. "
                 + "Dá pra escolher o time no perfil, em \"Editar perfil\".";
        }

        return null;
    }
}
