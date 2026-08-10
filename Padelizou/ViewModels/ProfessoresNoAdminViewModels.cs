namespace Padelizou.ViewModels;

// O que a tela /Admin/Professores mostra. Tipos próprios (e não ViewBag solto) porque são
// números de negócio que vão ser lidos pra tomar decisão — e número sem nome vira número
// errado na terceira vez que alguém mexe na tela.

// Uma linha da tabela: quem é, em que pé está o plano dele e quanto já entrou por ele.
public record ProfessorNoAdmin(
    int Id,
    string Nome,
    string? Email,
    string? Celular,
    DateTime? Desde,

    // O plano ESCOLHIDO (pode ser nulo — quem nunca decidiu) e a situação REAL de hoje,
    // que é outra coisa: quem escolheu Assinante e não pagou aparece como "em atraso".
    string? PlanoEscolhido,
    string Situacao,
    DateTime? FimDoTeste,
    DateTime? AssinaturaPagaAte,

    // Dinheiro de assinatura que este professor já trouxe — confirmado, em qualquer forma
    // (Pix direto, gateway ou registrado à mão).
    decimal TotalPago,
    int QuantidadeDePagamentos,
    DateTime? UltimoPagamentoEm,

    // Uso de verdade: é o que separa "cadastrou e sumiu" de "está tocando aulas aqui".
    int AulasNoTotal,
    int AulasNosUltimos30Dias,
    DateTime? UltimaAulaEm);

// Uma linha do "quanto entrou por mês". `Professores` conta gente distinta, e não cobranças:
// duas mensalidades do mesmo professor no mesmo mês são um cliente, não dois.
public record MesDeAssinatura(
    int Ano,
    int Mes,
    int Professores,
    int Cobrancas,
    decimal Valor);

public record ResumoDeProfessores(
    int Cadastrados,
    int Ativos,
    int EmTeste,
    int AssinantesEmDia,
    int AssinantesEmAtraso,
    int Avulsos,
    int SemEscolha,
    // Separado do `SemEscolha` de propósito: pra cobrança são iguais (taxa cheia nos dois),
    // mas aqui um desistiu e o outro nunca chegou na tela — e só o segundo se resolve
    // mostrando o plano pra ele.
    int NuncaAbriramOPlano,
    int JaPagaramAlgumaVez,
    decimal ReceitaTotal,
    decimal ReceitaNoMes);
