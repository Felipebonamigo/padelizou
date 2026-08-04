using padelizou.Models;

namespace Padelizou.Services;

// Um aluno na lista de atalho do professor.
//
// `AlunoId` nulo = pessoa sem conta no Padelizou; ela existe só como nome escrito nas aulas
// dele. Quando tem conta, marcar a aula grava o vínculo de verdade — e é isso que faz o
// aluno enxergar a aula no próprio app, coisa que a marcação por nome solto nunca deu.
public record AlunoDoProfessor(
    int? AlunoId,
    string Nome,
    int TotalDeAulas,
    DateTime UltimaAula,
    string? Celular)
{
    // "Aluno recorrente" é quem já voltou. Uma aula só é um teste; duas já é rotina, e é
    // essa a informação que o professor quer ver do lado do nome.
    public bool Recorrente => TotalDeAulas > 1;

    public bool TemConta => AlunoId.HasValue;
}

// A lista de quem já fez aula com ESTE professor.
//
// Existe porque o professor marca a aula PELO aluno — ele é quem senta e preenche — e até
// aqui digitava nome e telefone de novo a cada vez, mesmo pro aluno de toda terça. O telefone
// era obrigatório e ele nem sempre tem o número na mão na beira da quadra.
public static class AlunosDoProfessor
{
    // Aula cancelada ou recusada não faz de alguém aluno do professor: ela nunca aconteceu.
    private static bool Valeu(Aula aula) =>
        aula.Status != PoliticaAula.Cancelada && aula.Status != "Recusada";

    // Agrupa pela mesma chave que o resto do sistema usa pra dizer "esta pessoa" — conta
    // quando existe, nome normalizado quando não (ver Services/PrecoDaAula.Chave).
    public static List<AlunoDoProfessor> Montar(IEnumerable<Aula> aulasDoProfessor)
    {
        return aulasDoProfessor
            .Where(Valeu)
            .GroupBy(a => PrecoDaAula.Chave(a))
            .Select(g =>
            {
                var maisRecente = g.OrderByDescending(a => a.DataHora).First();
                return new AlunoDoProfessor(
                    AlunoId: maisRecente.AlunoId,
                    // O nome que o professor escreveu ganha do nome do cadastro: ele chama a
                    // pessoa como a conhece na quadra, não como ela assinou o cadastro.
                    Nome: PrimeiroNaoVazio(
                        maisRecente.NomeAlunoAvulso,
                        maisRecente.NomeCompletoAluno,
                        maisRecente.Aluno?.ComoChamar,
                        "Aluno"),
                    TotalDeAulas: g.Count(),
                    UltimaAula: maisRecente.DataHora,
                    Celular: PrimeiroNaoVazio(maisRecente.TelefoneAlunoAvulso, maisRecente.Aluno?.Celular, null));
            })
            // Quem veio por último primeiro: a aula que o professor vai marcar agora é quase
            // sempre pra alguém que ele acabou de ver.
            .OrderByDescending(a => a.UltimaAula)
            .ToList();
    }

    private static string PrimeiroNaoVazio(params string?[] valores) =>
        valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";

    // Busca sem acento e sem caixa: o professor digita "jonatas" e precisa achar "Jônatas".
    public static bool Casa(string nome, string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return true;
        return SemAcento(nome).Contains(SemAcento(termo), StringComparison.OrdinalIgnoreCase);
    }

    public static string SemAcento(string texto) =>
        new(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
}
