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

    // Aplica o termo sobre uma consulta de jogadores. Sem termo, devolve a consulta só com
    // o filtro de conta excluída, pra quem chama poder combinar com outros filtros.
    public static IQueryable<Jogador> Filtrar(IQueryable<Jogador> query, string? termo)
    {
        // Quem pediu exclusão da conta (LGPD) nunca aparece numa busca — a linha continua
        // existindo pro histórico dos jogos, mas a pessoa não pode ser encontrada,
        // convidada nem inscrita de novo. Vale mesmo sem termo: é o contrário do que ela
        // pediu que ela apareça numa lista.
        query = query.Where(j => j.ExcluidoEm == null);

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

    // Quem está pedindo senha nova? Aceita e-mail, login OU **CPF** — e a entrada, de
    // propósito, NÃO aceita o CPF. A assimetria é o ponto:
    //
    //   · no login, aceitar o CPF transformaria um número que não é segredo no Brasil em
    //     metade das credenciais de todo mundo;
    //   · aqui ele é inofensivo, porque o que sai desta ação é um link mandado pro e-mail JÁ
    //     gravado na conta. Quem sabe o seu CPF e pede recuperação não recebe nada — só faz
    //     chegar um e-mail na SUA caixa.
    //
    // Existe porque até 17/08/2026 quem só sabia o próprio CPF não tinha caminho nenhum: a
    // busca devolvia null e a tela respondia "link a caminho" do mesmo jeito, então a pessoa
    // ficava esperando um e-mail que nunca foi mandado. E o CPF é justamente a identidade de
    // quem entrou por pré-cadastro — é o único dado que ELA não escolheu, porque foi o
    // organizador que digitou por ela.
    public static async Task<Jogador?> ParaRecuperarSenhaAsync(
        DbPadelContext context, string? identificador)
    {
        // E-mail e login primeiro: são o que a pessoa escolheu, e um login que por acaso seja
        // só números não pode ser atropelado pela busca de CPF.
        var pelaIdentidade = await PorIdentificadorAsync(context, identificador);
        if (pelaIdentidade != null) return pelaIdentidade;

        // CPF COMPLETO, nunca parcial (ver PareceCpf): sem isso, "esqueci minha senha" viraria
        // uma varredura de documentos da base, um pedaço por vez.
        if (!PareceCpf(identificador)) return null;

        // Conta excluída (LGPD) não chega aqui, e não é por sorte: a anonimização troca o CPF
        // por "EX000000123" (ver ExclusaoDeConta.CpfDeQuemSaiu), que tem letra — PareceCpf
        // recusa acima, e a igualdade abaixo nunca casaria.
        var digitos = Documentos.SomenteDigitos(identificador);
        return await context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == digitos);
    }

    // Acha quem vai receber uma ação administrativa dirigida a UMA pessoa (hoje: o teste de
    // aviso do painel). Aceita o que o admin tem na mão — login, e-mail, nome, apelido ou CPF.
    //
    // Devolve LISTA, não um jogador: login e e-mail são únicos, mas nome não é, e "mandei pro
    // João errado" é exatamente o engano que uma tela de teste não pode cometer em silêncio.
    // Quando o termo casa exato com login/e-mail, ele ganha e a lista vem com um só — senão
    // digitar um login que também é o apelido de outra pessoa viraria uma escolha inútil.
    public static async Task<List<Jogador>> ParaAcaoAdministrativaAsync(
        DbPadelContext context, string? termo, int limite = 10)
    {
        var exato = await PorIdentificadorAsync(context, termo);
        if (exato != null) return new List<Jogador> { exato };

        return await BuscarAsync(context, termo, limite);
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
