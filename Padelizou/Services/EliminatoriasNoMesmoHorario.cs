namespace Padelizou.Services;

// JOGADOR QUE PODE CAIR EM DUAS ELIMINATÓRIAS AO MESMO TEMPO (22/11/2026, Wallau).
//
// 🗣️ Felipe: *"Tem que prever isso na hora do chaveamento, avisando que existe a possibilidade de
// tal jogador jogar as duas eliminatórias por categorias diferentes no mesmo horário"*. A prévia
// marca o mata-mata sem saber quem vai jogar; o Matheus Gerlach caiu na Quartas 1 da 3ª e na
// Quartas 3 da 4ª, as duas às 22:20, e o robô (até aquele dia) largou uma delas no domingo 13h.
//
// Aqui só a conta: pra cada pessoa, os jogos de mata-mata que PODEM ser dela (quem monta a lista é
// TorneiosController.EliminatoriasNoMesmoHorarioAsync, com a régua do "só meus jogos") — e cada par
// de categorias diferentes que se sobrepõe no tempo vira uma linha de aviso.
//
// Dois jogos REAIS não entram: aí não é possibilidade, é conflito, e já é o aviso de "jogador em dois
// jogos ao mesmo tempo" (AuditoriaDaGrade.PessoaEmDoisJogos).
public static class EliminatoriasNoMesmoHorario
{
    public record Jogo(string Categoria, string Rotulo, DateTime Inicio, TimeSpan Duracao, bool Real);

    public static List<string> Possiveis(IEnumerable<(string Nome, IReadOnlyList<Jogo> Jogos)> pessoas)
    {
        var avisos = new List<string>();
        foreach (var (nome, jogos) in pessoas)
        {
            var emOrdem = jogos.OrderBy(j => j.Inicio).ThenBy(j => j.Categoria, StringComparer.Ordinal).ToList();
            for (int i = 0; i < emOrdem.Count; i++)
            for (int k = i + 1; k < emOrdem.Count; k++)
            {
                var a = emOrdem[i];
                var b = emOrdem[k];
                if (a.Categoria == b.Categoria || (a.Real && b.Real)) continue;
                if (!(a.Inicio < b.Inicio + b.Duracao && b.Inicio < a.Inicio + a.Duracao)) continue;

                var quando = a.Inicio == b.Inicio
                    ? $"às {a.Inicio:dd/MM HH:mm}"
                    : $"às {a.Inicio:dd/MM HH:mm} e {b.Inicio:HH:mm}";
                avisos.Add($"{nome}: {a.Rotulo} da {a.Categoria} e {b.Rotulo} da {b.Categoria} {quando} — " +
                           "se passar nas duas, joga dois jogos ao mesmo tempo.");
            }
        }
        return avisos.Distinct().ToList();
    }
}
