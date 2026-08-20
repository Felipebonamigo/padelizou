using Padelizou.Models;
using Padelizou.ViewModels;
// A Aula vive em `padelizou.Models` (p minúsculo), e não no mesmo namespace de Jogador e
// Pagamento — o alias evita ter que importar os dois e conviver com a ambiguidade.
using Aula = padelizou.Models.Aula;

namespace Padelizou.Services;

// A tela /Admin/Professores: quem são, em que pé está o plano de cada um e quanto entrou por
// eles — por professor e por mês.
//
// ⚠️ A COMPOSIÇÃO MORA AQUI, E NÃO NO CONTROLLER, por um motivo específico: quase todo número
// desta tela é uma DEFINIÇÃO, não um dado guardado. "Ativo" não existe no banco; "já pagou" é
// uma consulta em outra tabela; "em atraso" é regra de PlanoDoProfessor. Definição que mora na
// tela é definição que muda sem ninguém perceber — e aqui ela vai ser lida pra decidir preço.
public static class ProfessoresNoAdmin
{
    // ⚠️ "ATIVO" É UMA ESCOLHA NOSSA, e a tela precisa dizer qual. Janela simétrica: 30 dias
    // pra trás pega quem acabou de dar aula, 30 pra frente pega quem tem agenda marcada. Só um
    // dos lados mentiria — olhando só pro passado, o professor que fechou a agenda do mês que
    // vem apareceria como sumido; só pro futuro, quem trabalhou o mês inteiro e ainda não
    // remarcou também.
    public const int JanelaDeAtividadeEmDias = 30;

    // Aula que não aconteceu e não vai acontecer não conta como movimento.
    private static readonly string[] AulasQueNaoContam = { "Cancelada", "Recusada" };

    public static bool AulaConta(Aula aula) => !AulasQueNaoContam.Contains(aula.Status);

    // ⚠️ UM RÓTULO QUE O `PlanoDoProfessor` NÃO SABE DAR, e sem ele a tela mente sobre a maior
    // fatia da base. Pra cobrar a aula, "nunca abriu o plano" e "abriu, testou e não escolheu"
    // são a mesma coisa (os dois pagam taxa cheia), e por isso `SituacaoDe` devolve
    // `TesteVencidoSemEscolha` pros dois. Mas aqui a diferença é o funil inteiro: um desistiu,
    // o outro nunca chegou lá — e só o segundo se resolve mostrando a tela pra ele.
    //
    // O relógio do teste só começa na primeira visita ao /PlanoProfessor (ver
    // PlanoProfessorController.Index), então `TesteProfessorInicio == null` é exatamente
    // "nunca viu".
    public const string NuncaAbriuOPlano = "Nunca abriu o plano";

    // ⚠️ `CortesiaConcedidaEm` — o CARIMBO, e não `CortesiaProfessorAte`, que é a data. Tirar a
    // cortesia zera só a data; sem esta quarta ponta, quem teve cortesia retirada reapareceria
    // como "nunca chegou ao plano". E pior: quem GANHOU cortesia sem nunca ter aberto a tela
    // cairia aqui e o rótulo "Cortesia" seria engolido antes de chegar na tela.
    public static bool NuncaViuOPlano(Jogador professor) =>
        professor.TesteProfessorInicio == null
        && professor.PlanoProfessor == null
        && professor.AssinaturaProfessorPagaAte == null
        && professor.CortesiaConcedidaEm == null;

    public const string RotuloDeCortesia = "Cortesia";

    // ⚠️ O `_ =>` DEVOLVE "Teste vencido, sem escolha". Esquecer a linha da Cortesia faria o
    // professor de permuta aparecer com esse rótulo, cair no balde amarelo do resumo — e
    // COMPILAR SEM UM AVISO. Por isso ela é explícita, e não fica por conta do default.
    public static string RotuloDaSituacao(PlanoDoProfessor.Situacao situacao) => situacao switch
    {
        PlanoDoProfessor.Situacao.EmTeste => "Em teste",
        PlanoDoProfessor.Situacao.AssinanteEmDia => "Assinante em dia",
        PlanoDoProfessor.Situacao.AssinanteEmAtraso => "Assinante em atraso",
        PlanoDoProfessor.Situacao.Avulso => "Avulso",
        PlanoDoProfessor.Situacao.Cortesia => RotuloDeCortesia,
        _ => "Teste vencido, sem escolha",
    };

    // Uma linha por professor. As três coleções chegam JÁ materializadas: são poucos
    // professores, e a régua do plano (PlanoDoProfessor.SituacaoDe) é C# puro que o EF não
    // traduziria — tentar fazer isso na consulta passaria no teste InMemory e estouraria em
    // produção, que é o defeito de sempre.
    public static List<ProfessorNoAdmin> Montar(
        IEnumerable<Jogador> professores,
        IEnumerable<Pagamento> assinaturasConfirmadas,
        IEnumerable<Aula> aulas,
        DateTime agora,
        PlanoProfessorSettings cfg,
        // Id -> nome de quem concedeu a cortesia. Dicionário e não navegação: a FK seria
        // auto-referência em Jogador, e um Include de navegação obrigatória vira INNER JOIN.
        IReadOnlyDictionary<int, string>? nomeDeQuemDeuCortesia = null)
    {
        var porProfessor = assinaturasConfirmadas
            .GroupBy(p => p.JogadorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var aulasPorProfessor = aulas
            .Where(AulaConta)
            .GroupBy(a => a.ProfessorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inicioDaJanela = agora.AddDays(-JanelaDeAtividadeEmDias);
        var fimDaJanela = agora.AddDays(JanelaDeAtividadeEmDias);

        return professores
            .Select(prof =>
            {
                var pagamentos = porProfessor.GetValueOrDefault(prof.Id) ?? new List<Pagamento>();
                var suasAulas = aulasPorProfessor.GetValueOrDefault(prof.Id) ?? new List<Aula>();

                return new ProfessorNoAdmin(
                    prof.Id,
                    prof.Nome,
                    prof.Email,
                    prof.Celular,
                    prof.CriadoEm,
                    prof.PlanoProfessor,
                    NuncaViuOPlano(prof)
                        ? NuncaAbriuOPlano
                        : RotuloDaSituacao(PlanoDoProfessor.SituacaoDe(prof, agora, cfg)),
                    PlanoDoProfessor.FimDoTeste(prof, cfg),
                    prof.AssinaturaProfessorPagaAte,
                    pagamentos.Sum(p => p.Valor),
                    pagamentos.Count,
                    pagamentos.Select(QuandoEntrou).DefaultIfEmpty(null).Max(),
                    suasAulas.Count,
                    suasAulas.Count(a => a.DataHora >= inicioDaJanela && a.DataHora <= fimDaJanela),
                    suasAulas.Select(a => (DateTime?)a.DataHora).DefaultIfEmpty(null).Max(),
                    prof.CortesiaProfessorAte,
                    prof.MotivoDaCortesia,
                    prof.CortesiaConcedidaEm,
                    prof.CortesiaConcedidaPorJogadorId is int quemDeu
                        ? nomeDeQuemDeuCortesia?.GetValueOrDefault(quemDeu)
                        : null);
            })
            // Quem move dinheiro primeiro, depois quem move aula, depois o resto em ordem de
            // nome: a tela existe pra achar quem importa, e ordem alfabética pura enterraria
            // os assinantes no meio de quem só criou conta.
            .OrderByDescending(p => p.TotalPago)
            .ThenByDescending(p => p.AulasNosUltimos30Dias)
            .ThenBy(p => p.Nome)
            .ToList();
    }

    // ⚠️ `meses` entra porque a receita DO MÊS não sai das linhas de professor: somar o
    // `TotalPago` de quem pagou este mês somaria o histórico inteiro dele junto. O valor do mês
    // é o do mês — vem da série, que é onde ele foi calculado uma vez só.
    public static ResumoDeProfessores Resumir(
        IEnumerable<ProfessorNoAdmin> linhas, IEnumerable<MesDeAssinatura> meses, DateTime agora)
    {
        var lista = linhas.ToList();

        return new ResumoDeProfessores(
            Cadastrados: lista.Count,
            Ativos: lista.Count(p => p.AulasNosUltimos30Dias > 0),
            EmTeste: lista.Count(p => p.Situacao == RotuloDaSituacao(PlanoDoProfessor.Situacao.EmTeste)),
            AssinantesEmDia: lista.Count(p => p.Situacao == RotuloDaSituacao(PlanoDoProfessor.Situacao.AssinanteEmDia)),
            AssinantesEmAtraso: lista.Count(p => p.Situacao == RotuloDaSituacao(PlanoDoProfessor.Situacao.AssinanteEmAtraso)),
            Avulsos: lista.Count(p => p.Situacao == RotuloDaSituacao(PlanoDoProfessor.Situacao.Avulso)),
            SemEscolha: lista.Count(p => p.Situacao == RotuloDaSituacao(PlanoDoProfessor.Situacao.TesteVencidoSemEscolha)),
            NuncaAbriramOPlano: lista.Count(p => p.Situacao == NuncaAbriuOPlano),
            // ⚠️ "Já pagou alguma vez" é uma pergunta DIFERENTE de "é assinante hoje": quem
            // pagou três meses e parou continua contando aqui, e é justamente ele que a gente
            // quer enxergar — cliente perdido não aparece em nenhuma outra contagem.
            JaPagaramAlgumaVez: lista.Count(p => p.QuantidadeDePagamentos > 0),
            ReceitaTotal: lista.Sum(p => p.TotalPago),
            ReceitaNoMes: meses
                .Where(m => m.Ano == agora.Year && m.Mes == agora.Month)
                .Sum(m => m.Valor),
            // ⚠️ NENHUM número de dinheiro acima muda com a cortesia, e isso é o desenho, não
            // sorte: cortesia não cria `Pagamento`, e todo valor desta tela sai de `Pagamento`.
            EmCortesia: lista.Count(p => p.Situacao == RotuloDeCortesia));
    }

    // Quanto entrou por mês, do mais recente pro mais antigo.
    //
    // ⚠️ É CAIXA, não competência: o plano anual de R$ 499,90 aparece INTEIRO no mês em que foi
    // pago, e não R$ 41,66 por doze meses. É a mesma régua do /Admin/Financeiro (o corte é pela
    // data em que o dinheiro entrou), e a tela avisa disso — senão o mês de uma anuidade parece
    // um mês excepcional que não se repete.
    public static List<MesDeAssinatura> PorMes(IEnumerable<Pagamento> assinaturasConfirmadas)
    {
        return assinaturasConfirmadas
            .Select(p => new { Quando = QuandoEntrou(p) ?? p.CriadoEm, p.JogadorId, p.Valor })
            .GroupBy(x => new { x.Quando.Year, x.Quando.Month })
            .Select(g => new MesDeAssinatura(
                g.Key.Year,
                g.Key.Month,
                Professores: g.Select(x => x.JogadorId).Distinct().Count(),
                Cobrancas: g.Count(),
                Valor: g.Sum(x => x.Valor)))
            .OrderByDescending(m => m.Ano).ThenByDescending(m => m.Mes)
            .ToList();
    }

    // A data que vale é a da CONFIRMAÇÃO — o dinheiro entrou ali. `CriadoEm` é reserva pras
    // linhas antigas que foram confirmadas antes de a coluna existir.
    private static DateTime? QuandoEntrou(Pagamento pagamento) =>
        pagamento.ConfirmadoEm ?? pagamento.CriadoEm;
}
