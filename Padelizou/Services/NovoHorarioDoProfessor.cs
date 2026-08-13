using Padelizou.Models;

namespace Padelizou.Services;

// Cadastro de horário do professor em VÁRIOS dias de uma vez ("segunda, quarta e sexta das
// 8h ao meio-dia"). Pedido do Felipe em 29/07/2026: um dia por vez tornava o cadastro da
// semana inteira uma novela de sete capítulos.
//
// A regra decide três coisas: quais dias criar, quais já existiam (e o que fazer com eles),
// e se o pedido faz sentido — porque a tela antiga aceitava fim antes do início e aula de
// 90 min numa janela de 60, e os dois erros só apareciam pro ALUNO, como agenda vazia.
public static class NovoHorarioDoProfessor
{
    public sealed record Plano(
        List<int> DiasParaCriar,
        List<int> DiasParaReativar,
        List<int> DiasQueJaExistiam,
        string? Erro)
    {
        public bool Valido => Erro == null;
    }

    public static Plano Planejar(
        IEnumerable<int> diasPedidos, TimeSpan horaInicio, TimeSpan horaFim, int duracaoMinutos,
        int localAulaId, IEnumerable<HorarioDisponivel> horariosDoProfessor)
    {
        // Dia fora de 0..6 não existe; repetido conta uma vez. Nada disso é erro — formulário
        // adulterado não merece mensagem boa, merece ser ignorado.
        var dias = diasPedidos.Where(d => d is >= 0 and <= 6).Distinct().OrderBy(d => d).ToList();

        if (dias.Count == 0)
            return Falha("Escolha pelo menos um dia da semana.");

        if (horaFim <= horaInicio)
            return Falha("O fim precisa ser depois do início.");

        // Aula que não cabe na janela é o erro mais traiçoeiro: o horário aparece na lista
        // do professor como se estivesse tudo certo, e o aluno nunca encontra vaga nenhuma.
        if (duracaoMinutos > (horaFim - horaInicio).TotalMinutes)
            return Falha($"Uma aula de {duracaoMinutos} min não cabe entre " +
                         $"{Hora(horaInicio)} e {Hora(horaFim)}.");

        var criar = new List<int>();
        var reativar = new List<int>();
        var jaExistiam = new List<int>();

        foreach (var dia in dias)
        {
            // Duplicata = mesmo local, mesmo dia, mesma janela. Duração diferente não cria
            // outra linha: duas linhas idênticas na tela é bug na cabeça de quem olha.
            var igual = horariosDoProfessor.FirstOrDefault(h =>
                h.LocalAulaId == localAulaId && h.DiaSemana == dia
                && h.HoraInicio == horaInicio && h.HoraFim == horaFim);

            if (igual == null) criar.Add(dia);
            else if (!igual.Ativo) reativar.Add(dia);   // estava pausado: religar é o que a pessoa quis
            else jaExistiam.Add(dia);
        }

        return new Plano(criar, reativar, jaExistiam, null);
    }

    // EDITAR um horário que já existe passa pela MESMA régua de criar — de propósito. Fim
    // antes do início e aula que não cabe na janela são erros que só aparecem PRO ALUNO, como
    // agenda vazia; uma segunda cópia da validação aqui divergiria da de cima na primeira
    // mudança, e o buraco reabriria só na edição.
    //
    // ⚠️ O PRÓPRIO HORÁRIO SAI DA COMPARAÇÃO. Sem isso ele colide consigo mesmo e nada pode
    // ser salvo — nem trocar só a duração, que é o motivo mais comum pra abrir esta tela.
    //
    // Devolve o motivo para NÃO editar, ou null quando pode.
    public static string? MotivoParaNaoEditar(
        int idQueEstaSendoEditado, int dia, TimeSpan horaInicio, TimeSpan horaFim,
        int duracaoMinutos, int localAulaId, IEnumerable<HorarioDisponivel> horariosDoProfessor)
    {
        var osOutros = horariosDoProfessor.Where(h => h.Id != idQueEstaSendoEditado);
        var plano = Planejar(new[] { dia }, horaInicio, horaFim, duracaoMinutos, localAulaId, osOutros);

        if (!plano.Valido) return plano.Erro;

        // Sobrou fora de "criar" quer dizer que já existe um igual (ativo ou pausado): mover
        // terça pra cima de uma quarta que já existe deixaria duas linhas idênticas na tela.
        if (plano.DiasParaCriar.Count == 0)
            return $"Você já tem um horário nesse local na {Nomes[dia]}, "
                 + $"das {Hora(horaInicio)} às {Hora(horaFim)}.";

        return null;
    }

    // "Horário criado pra segunda, quarta e sexta. Terça já existia e ficou como estava."
    public static string Resumo(Plano plano)
    {
        var partes = new List<string>();

        if (plano.DiasParaCriar.Count > 0)
            partes.Add($"Horário criado pra {Lista(plano.DiasParaCriar)}.");
        if (plano.DiasParaReativar.Count > 0)
            partes.Add($"Reativado em {Lista(plano.DiasParaReativar)}.");
        if (plano.DiasQueJaExistiam.Count > 0)
            partes.Add($"{Capitalizar(Lista(plano.DiasQueJaExistiam))} já tinha esse horário e ficou como estava.");

        return string.Join(" ", partes);
    }

    private static readonly string[] Nomes =
        { "domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado" };

    private static string Lista(List<int> dias)
    {
        var nomes = dias.Select(d => Nomes[d]).ToList();
        return nomes.Count == 1 ? nomes[0]
            : string.Join(", ", nomes.Take(nomes.Count - 1)) + " e " + nomes[^1];
    }

    private static string Capitalizar(string s) => char.ToUpper(s[0]) + s[1..];

    private static string Hora(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";

    private static Plano Falha(string erro) => new(new(), new(), new(), erro);
}
