using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Fechar o mês: transformar as aulas de um mês nas contas de cada aluno (ver
// Models/FaturaDoAluno). É o modelo do professor mensalista — "fecha as aulas do mês e cobra
// no mês subsequente".
//
// Fica aqui, e não no controller, porque a pergunta "quais aulas entram nesta conta" tem três
// respostas erradas fáceis e cada uma custa dinheiro de alguém: contar a aula cancelada cobra
// o que não foi combinado, esquecer a falta cobrável perde o dinheiro do mensalista, e contar
// a reposição cobra duas vezes a MESMA aula.
public static class FechamentoDoMes
{
    public const string Aberta = "Aberta";
    public const string Paga = "Paga";
    public const string Cancelada = "Cancelada";

    // Dia do mês seguinte em que a conta vence, quando o professor não escolhe outro. Dez
    // porque é depois do quinto dia útil — o mensalista costuma pagar o professor com o
    // salário já na conta.
    public const int DiaDeVencimentoPadrao = 10;

    // A aula entra na conta do mês? São dois casos, e o segundo é o que o mensalista precisa:
    //
    //   ACONTECEU        — Realizada, o caso normal.
    //   NÃO ACONTECEU E É COBRADA — falta cobrável e "vai recuperar" (ver Services/Reposicao),
    //                      que marcam `CobrarMesmoFaltando`. Quem paga o mês não desconta a
    //                      falta; deixar de fora seria devolver dinheiro que ninguém pediu.
    //
    // ⚠️ REPOSIÇÃO FICA DE FORA. Ela aconteceu, mas o dinheiro dela já entrou na conta do mês
    // da aula original — foi por isso que ela nasceu sem preço. Contá-la aqui cobraria a mesma
    // aula duas vezes, em dois meses diferentes, que é o erro mais caro possível nesta tela.
    public static bool EntraNaConta(Aula aula) =>
        aula.RecuperaAulaId == null
        && (aula.Status == PoliticaAula.Realizada || aula.CobrarMesmoFaltando);

    public static bool DoMes(Aula aula, int ano, int mes) =>
        aula.DataHora.Year == ano && aula.DataHora.Month == mes;

    // O vencimento: dia escolhido do MÊS SEGUINTE ao das aulas. O dia é preso ao último dia do
    // mês porque fevereiro não tem 31 — pedir 31 devolveria 28 em vez de estourar.
    //
    // ⚠️ E NUNCA NO PASSADO. Professor que fecha julho no dia 19 de agosto pedindo "vence dia
    // 10" receberia uma conta nascida vencida em 10/08 — vermelha na tela, cobrando atraso de
    // quem nunca teve como pagar. Nesse caso o dia escolhido rola pro mês seguinte: ele
    // escolheu O DIA, e é o dia que se mantém. Quem quer receber antes escolhe outro.
    //
    // Descoberto rodando a tela de verdade, não nos testes: no teste a competência é sempre
    // o mês passado e o vencimento cai à frente por sorte do calendário.
    public static DateTime Vencimento(int ano, int mes, int diaEscolhido, DateTime agora)
    {
        var mesDeCobranca = new DateTime(ano, mes, 1).AddMonths(1);

        // Rola mês a mês, e não "soma um mês se passou": fechar uma competência de três meses
        // atrás precisa de mais de um pulo pra sair do passado.
        while (true)
        {
            var dia = Math.Clamp(diaEscolhido, 1, DateTime.DaysInMonth(mesDeCobranca.Year, mesDeCobranca.Month));
            var candidato = new DateTime(mesDeCobranca.Year, mesDeCobranca.Month, dia);

            if (candidato >= agora.Date) return candidato;

            mesDeCobranca = mesDeCobranca.AddMonths(1);
        }
    }

    // O que UMA conta vai dizer, antes de existir. A tela mostra isto pro professor conferir,
    // e o fechamento grava exatamente o mesmo — uma conta só, calculada num lugar só.
    public record Previa(
        int? AlunoId,
        string? NomeAvulso,
        string NomeDoAluno,
        int QuantidadeAulas,
        decimal Valor,
        string PagadorNome,
        string? PagadorCelular,
        string? PagadorCpf,
        bool JaFechada)
    {
        // Dá pra cobrar pelo app? Sem CPF do pagador o gateway recusa a cobrança (ver
        // AsaasService.ObterOuCriarClienteAsync). A conta continua valendo — o professor
        // acerta por fora, como já faz hoje —, mas a tela precisa dizer isso ANTES.
        public bool PodeCobrarPeloApp => !string.IsNullOrWhiteSpace(PagadorCpf);
    }

    // As contas do mês, uma por aluno, na ordem em que o professor quer conferir: quem deve
    // mais primeiro. `jaFechadas` são as chaves que já têm conta desta competência — elas
    // aparecem marcadas em vez de sumirem, senão o professor abriria a tela depois de fechar
    // e veria o mês vazio, sem entender se deu certo.
    public static List<Previa> Previas(
        IEnumerable<Aula> aulasDoProfessor,
        IEnumerable<CadastroDoAluno> cadastros,
        IEnumerable<Jogador?> contas,
        int ano, int mes,
        ISet<string> jaFechadas)
    {
        var porChave = CadastrosDeAlunos.PorChave(cadastros);
        var contaPorId = contas.Where(c => c != null).ToDictionary(c => c!.Id, c => c!);

        return aulasDoProfessor
            .Where(a => DoMes(a, ano, mes) && EntraNaConta(a))
            .GroupBy(a => PrecoDaAula.Chave(a))
            .Select(g =>
            {
                var amostra = g.First();
                porChave.TryGetValue(g.Key, out var cadastro);

                var conta = amostra.AlunoId is int id && contaPorId.TryGetValue(id, out var achada)
                    ? achada
                    : null;

                // O mesmo nome que o resto do sistema mostra: o que o professor escreveu ganha
                // do nome do cadastro — ele chama a pessoa como a conhece na quadra.
                var nomeDoAluno = amostra.NomeAlunoAvulso
                                  ?? amostra.NomeCompletoAluno
                                  ?? conta?.ComoChamar
                                  ?? "Aluno";

                return new Previa(
                    AlunoId: amostra.AlunoId,
                    NomeAvulso: amostra.NomeAlunoAvulso,
                    NomeDoAluno: nomeDoAluno,
                    QuantidadeAulas: g.Count(),
                    Valor: g.Sum(a => a.Preco),
                    PagadorNome: CadastrosDeAlunos.QuemPaga(cadastro, nomeDoAluno),
                    PagadorCelular: CadastrosDeAlunos.CelularDeCobranca(cadastro, conta?.Celular),
                    PagadorCpf: CadastrosDeAlunos.CpfDoPagador(cadastro, conta),
                    JaFechada: jaFechadas.Contains(g.Key));
            })
            .OrderByDescending(p => p.Valor)
            .ToList();
    }

    // A conta pronta pra gravar. Nasce Aberta: pagar é outro momento, e o professor pode
    // receber por fora (Pix na mão) sem o app nunca saber — por isso "Paga" é um estado que
    // ele marca, e não só o que o gateway confirma.
    public static FaturaDoAluno Fechar(int professorId, Previa previa, int ano, int mes,
        int diaVencimento, DateTime agora) =>
        new()
        {
            ProfessorId = professorId,
            AlunoId = previa.AlunoId,
            NomeAvulso = previa.AlunoId == null ? previa.NomeAvulso : null,
            Ano = ano,
            Mes = mes,
            Valor = previa.Valor,
            QuantidadeAulas = previa.QuantidadeAulas,
            PagadorNome = previa.PagadorNome,
            PagadorCelular = previa.PagadorCelular,
            PagadorCpf = previa.PagadorCpf,
            FechadaEm = agora,
            Vencimento = Vencimento(ano, mes, diaVencimento, agora),
            Status = Aberta,
        };

    // O mês só fecha depois de acabar. Fechar março no dia 12 de março geraria uma conta pela
    // metade — e o professor só descobriria quando o aluno reclamasse do valor.
    public static bool PodeFechar(int ano, int mes, DateTime agora) =>
        new DateTime(ano, mes, 1).AddMonths(1) <= agora.Date;

    public static string MotivoParaNaoFechar(int ano, int mes) =>
        $"O mês {mes:00}/{ano} ainda não terminou. Fechar agora geraria uma conta pela metade — "
        + "espere virar o mês.";

    // Quanto ainda está em aberto de tudo o que já foi fechado. É o número que o professor
    // procura: "quanto eu tenho pra receber".
    public static decimal EmAberto(IEnumerable<FaturaDoAluno> faturas) =>
        faturas.Where(f => f.Status == Aberta).Sum(f => f.Valor);

    public static bool EstaVencida(FaturaDoAluno fatura, DateTime agora) =>
        fatura.Status == Aberta && fatura.Vencimento.Date < agora.Date;
}
