using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// A tela de fechar o mês do professor mensalista: o que ele vai cobrar de cada aluno, o que
// já foi fechado, e o que ainda não foi pago.
public class FaturamentoVM
{
    // A competência na tela — o mês das AULAS, não o mês em que se cobra.
    public int Ano { get; set; }
    public int Mes { get; set; }

    public string Titulo => new DateTime(Ano, Mes, 1).ToString("MMMM 'de' yyyy",
        new System.Globalization.CultureInfo("pt-BR"));

    // O que sairia se ele fechasse agora, aluno por aluno.
    public List<FechamentoDoMes.Previa> Previas { get; set; } = new();

    // As contas desta competência que já existem.
    public List<FaturaDoAluno> Fechadas { get; set; } = new();

    // Tudo o que está aberto, de qualquer mês: é o número que responde "quanto eu tenho pra
    // receber". Separado do mês na tela porque conta de março que ninguém pagou não pode
    // sumir só porque o professor está olhando abril.
    public List<FaturaDoAluno> EmAbertoDeTodosOsMeses { get; set; } = new();

    public decimal TotalEmAberto => FechamentoDoMes.EmAberto(EmAbertoDeTodosOsMeses);

    public bool PodeFechar { get; set; }
    public string? MotivoParaNaoFechar { get; set; }

    // Quantos alunos ainda não têm CPF de pagador. É o que decide se o professor consegue
    // cobrar pelo app ou vai ter que acertar por fora — e ele precisa ver isso ANTES de
    // fechar, não no dia da cobrança.
    public int SemCpfDoPagador => Previas.Count(p => !p.JaFechada && !p.PodeCobrarPeloApp);

    public int DiaDeVencimento { get; set; } = FechamentoDoMes.DiaDeVencimentoPadrao;
}
