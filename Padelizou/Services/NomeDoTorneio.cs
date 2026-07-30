namespace Padelizou.Services;

// Dois torneios com o MESMO nome, do mesmo organizador, ao mesmo tempo.
//
// Aconteceu de verdade no primeiro dia com gente real (30/07/2026): o organizador criou
// "Amigos do Eder" duas vezes. Formulário longo, botão apertado de novo por não ter certeza
// se salvou — e o sistema aceitou os dois em silêncio. Depois nem ele nem os jogadores sabem
// em qual se inscrever, e as inscrições se dividem entre os dois.
public static class NomeDoTorneio
{
    // A mesma normalização usada pra cidade (minúsculo, sem acento, espaços colapsados). Ela
    // não tem nada de específico de cidade — é "comparar como uma PESSOA compararia", e é
    // exatamente o que se quer aqui: "Amigos do Eder" e "amigos do eder " são o mesmo torneio.
    public static string Chave(string? nome) => NomeDeCidade.Chave(nome);

    public static bool Mesmo(string? a, string? b) => NomeDeCidade.Mesma(a, b);

    // Só conta como impedimento o torneio que ainda está de pé: quem organiza o "Amigos do
    // Eder" todo mês precisa poder criar a edição seguinte depois que a anterior acabou.
    // Por isso a regra olha o status, e não a existência do nome pra sempre.
    public const string StatusQueLibera = "Finalizado";

    public static bool AindaEstaDePe(string? status) => status != StatusQueLibera;

    // Devolve o nome do torneio que impede, ou null quando pode criar.
    public static string? JaExisteEntre(
        IEnumerable<(string Nome, string? Status)> torneiosDoOrganizador, string? nomeNovo)
    {
        foreach (var (nome, status) in torneiosDoOrganizador)
        {
            if (AindaEstaDePe(status) && Mesmo(nome, nomeNovo)) return nome;
        }
        return null;
    }
}
