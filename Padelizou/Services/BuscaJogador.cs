using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Um jeito só de procurar jogador no sistema inteiro: nome, apelido ou CPF.
// Fica num lugar só porque a busca aparece em várias telas (busca de jogadores,
// inscrição em torneio, convite de grupo, mensalista do clube) e nada garante que
// versões separadas continuariam concordando com o tempo.
public static class BuscaJogador
{
    // O termo é CPF quando, tirando pontuação, sobram exatamente 11 dígitos.
    // Exigir o CPF INTEIRO é proposital: com busca parcial, digitar "200000" listaria
    // todo mundo daquela faixa, e daria pra varrer os documentos da base aos poucos.
    // Quem procura por CPF já tem o número na mão.
    public static bool PareceCpf(string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return false;

        // Se o termo tem letra, é nome/apelido — mesmo que também tenha número
        // (tem gente cadastrada como "Ana 2").
        if (termo.Any(char.IsLetter)) return false;

        return Documentos.SomenteDigitos(termo).Length == 11;
    }

    // Aplica o termo sobre uma consulta de jogadores. Devolve a consulta intocada
    // quando o termo é vazio, pra quem chama poder combinar com outros filtros.
    public static IQueryable<Jogador> Filtrar(IQueryable<Jogador> query, string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return query;

        termo = termo.Trim();

        if (PareceCpf(termo))
        {
            // Igualdade, não Contains: CPF completo acha uma pessoa só.
            var digitos = Documentos.SomenteDigitos(termo);
            return query.Where(j => j.Cpf == digitos);
        }

        // Só números, mas não são 11 dígitos? Não é CPF válido pra busca e também não
        // é nome — devolve vazio em vez de listar meio banco por engano.
        if (!termo.Any(char.IsLetter) && Documentos.SomenteDigitos(termo).Length > 0)
        {
            return query.Where(j => false);
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

    // Quem está tentando entrar? Aceita e-mail OU login, sem diferenciar maiúsculas —
    // "Bona", "bona" e "bOnA" são a mesma pessoa. Fica aqui junto das outras buscas
    // pra não existir uma quarta regra de "achar jogador" espalhada pelo sistema.
    public static async Task<Jogador?> PorIdentificadorAsync(DbPadelContext context, string? identificador)
    {
        var alvo = (identificador ?? "").Trim().ToLower();
        if (alvo.Length == 0) return null;

        return await context.Jogadores.FirstOrDefaultAsync(j =>
            (j.Email != null && j.Email.ToLower() == alvo) ||
            (j.Login != null && j.Login.ToLower() == alvo));
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
