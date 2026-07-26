using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Um jeito só de procurar jogador no sistema inteiro: nome, apelido ou CPF.
// Fica num lugar só porque a busca aparece em várias telas (busca de jogadores,
// inscrição em torneio, convite de grupo, mensalista do clube) e nada garante que
// versões separadas continuariam concordando com o tempo.
public static class BuscaJogador
{
    // O termo é CPF quando, tirando pontuação, sobram só dígitos (e pelo menos 3).
    // Assim "111.444.777-35", "11144477735" e "111444" caem na busca por documento,
    // e "Zeca" não.
    public static bool PareceCpf(string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return false;

        var digitos = Documentos.SomenteDigitos(termo);
        if (digitos.Length < 3) return false;

        // Se o termo tem letra, é nome/apelido — mesmo que também tenha número
        // (tem gente cadastrada como "Ana 2").
        return !termo.Any(char.IsLetter);
    }

    // Aplica o termo sobre uma consulta de jogadores. Devolve a consulta intocada
    // quando o termo é vazio, pra quem chama poder combinar com outros filtros.
    public static IQueryable<Jogador> Filtrar(IQueryable<Jogador> query, string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return query;

        termo = termo.Trim();

        if (PareceCpf(termo))
        {
            var digitos = Documentos.SomenteDigitos(termo);
            return query.Where(j => j.Cpf.Contains(digitos));
        }

        // Nome OU apelido, ignorando maiúsculas. O ToLower dos dois lados é proposital:
        // Contains sozinho vira LIKE no PostgreSQL, que É sensível a maiúscula — quem
        // digitasse "zeca" não acharia "Zeca". Custa não usar índice, mas com poucos
        // milhares de jogadores isso não pesa, e a busca errada pesaria muito mais.
        var alvo = termo.ToLower();

        return query.Where(j =>
            j.Nome.ToLower().Contains(alvo) ||
            (j.Apelido != null && j.Apelido.ToLower().Contains(alvo)));
    }

    // Busca direta, já ordenada com o mais relevante primeiro: quem começa com o termo
    // vem antes de quem só o contém no meio ("Ana" antes de "Mariana").
    public static async Task<List<Jogador>> BuscarAsync(
        DbPadelContext context, string? termo, int limite = 30)
    {
        if (string.IsNullOrWhiteSpace(termo)) return new List<Jogador>();

        var achados = await Filtrar(context.Jogadores, termo)
            .Take(limite * 2)   // folga pra ordenar por relevância na memória
            .ToListAsync();

        var t = termo.Trim();

        return achados
            .OrderByDescending(j => (j.Apelido ?? "").StartsWith(t, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(j => j.Nome.StartsWith(t, StringComparison.OrdinalIgnoreCase))
            .ThenBy(j => j.Nome)
            .Take(limite)
            .ToList();
    }
}
