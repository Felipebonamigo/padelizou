using padelizou.Models;

namespace Padelizou.Services;

// O que pode mudar numa aula JÁ MARCADA — e, principalmente, o que a mudança arrasta junto.
//
// Marcar aula errado é o erro mais comum do professor na beira da quadra: ele lança 8h e era
// 9h, lança no clube em que dá aula todo dia e essa era no outro, combina 110 e o aluno trouxe
// um amigo. Até aqui o único desfazer era APAGAR e lançar de novo — o que perde as anotações
// da aula, muda o id (o link do caderno que o aluno recebeu morre) e manda pro aluno um
// "aula apagada" seguido de nada.
//
// Mudar horário ou local é diferente de mudar preço, e é por isso que a diferença mora aqui:
// quem não é avisado de um horário novo VAI À QUADRA NA HORA ERRADA. Preço o aluno acerta
// quando chegar.
public static class EdicaoDeAula
{
    // Só se edita aula que ainda está de pé. "Realizada" e "Faltou" já viraram dinheiro no
    // financeiro, e "Cancelada"/"Recusada" são fato registrado — mexer no horário delas
    // reescreveria o passado numa tela que ninguém abre pra conferir. Pra essas o caminho
    // continua sendo apagar (ver Services/ExclusaoDeAula).
    public static bool PodeEditar(Aula aula) =>
        aula.Status == PoliticaAula.Pendente || aula.Status == PoliticaAula.Confirmada;

    public static string MotivoDeNaoPoderEditar(Aula aula) => aula.Status switch
    {
        PoliticaAula.Realizada => "Essa aula já foi dada — ela já entrou no seu financeiro.",
        PoliticaAula.Faltou => "Essa aula está registrada como falta.",
        PoliticaAula.Cancelada => "Essa aula foi cancelada.",
        PoliticaAula.Recusada => "Essa aula foi recusada.",
        _ => "Essa aula não pode mais ser alterada.",
    };

    // O aluno precisa saber? Só quem TEM CONTA (não há pra onde mandar pro avulso — esse o
    // professor avisa por fora, e a tela oferece o WhatsApp), em aula que ainda vai acontecer,
    // e quando mudou alguma coisa de fato.
    public static bool PrecisaAvisarAluno(Aula aula, MudancaDaAula mudanca, DateTime agora) =>
        aula.AlunoId.HasValue
        && mudanca.MudouAlgo
        && mudanca.QuandoDepois > agora
        && PoliticaAula.ContaComoAtiva(aula.Status);

    // Horário ou local errado leva a pessoa pra quadra errada na hora errada: é pessoal,
    // urgente e acionável — os três critérios do WhatsApp (ver Services/AlcanceDoAviso).
    // Preço sozinho não é urgente: ela acerta com o professor quando chegar.
    public static AlcanceDoAviso CanalDoAviso(MudancaDaAula mudanca) =>
        mudanca.MudouHorario || mudanca.MudouLocal
            ? AlcanceDoAviso.AppEWhatsApp
            : AlcanceDoAviso.SoApp;

    // A frase que o aluno lê. Diz o que era ANTES de propósito: "sua aula é dia 14 às 9h"
    // sozinho parece um lembrete, e quem já tinha 13 às 8h anotado não percebe que mudou.
    public static string Recado(MudancaDaAula mudanca)
    {
        var partes = new List<string>();

        if (mudanca.MudouHorario)
            partes.Add($"o horário passou de {mudanca.QuandoAntes:dd/MM 'às' HH:mm} para {mudanca.QuandoDepois:dd/MM 'às' HH:mm}");

        if (mudanca.MudouDuracao)
            partes.Add($"a duração passou de {DuracaoDaAula.Rotulo(mudanca.DuracaoAntes)} para {DuracaoDaAula.Rotulo(mudanca.DuracaoDepois)}");

        if (mudanca.MudouLocal)
            partes.Add($"o local passou de {mudanca.LocalAntes} para {mudanca.LocalDepois}");

        if (mudanca.MudouPreco)
            partes.Add($"o valor passou de {mudanca.PrecoAntes:C} para {mudanca.PrecoDepois:C}");

        return Juntar(partes);
    }

    // O mesmo resumo, do lado do professor: o que ele acabou de mudar, pra tela confirmar a
    // ação em vez de só voltar pra agenda como se nada tivesse acontecido.
    public static string ResumoParaOProfessor(MudancaDaAula mudanca)
    {
        var partes = new List<string>();
        if (mudanca.MudouHorario) partes.Add($"horário para {mudanca.QuandoDepois:dd/MM 'às' HH:mm}");
        if (mudanca.MudouDuracao) partes.Add($"duração para {DuracaoDaAula.Rotulo(mudanca.DuracaoDepois)}");
        if (mudanca.MudouLocal) partes.Add($"local para {mudanca.LocalDepois}");
        if (mudanca.MudouPreco) partes.Add($"valor para {mudanca.PrecoDepois:C}");

        return partes.Count == 0 ? "Nada mudou nessa aula." : $"Aula atualizada: {Juntar(partes)}.";
    }

    private static string Juntar(List<string> partes) => partes.Count switch
    {
        0 => "",
        1 => partes[0],
        2 => $"{partes[0]} e {partes[1]}",
        _ => $"{string.Join(", ", partes.Take(partes.Count - 1))} e {partes[^1]}",
    };
}

// O antes e o depois de uma edição, lado a lado. É um tipo próprio porque as três perguntas
// que o resto do código faz ("mudou o horário?", "avisa o aluno?", "o que escrevo pra ele?")
// dependem das duas pontas ao mesmo tempo — com os campos soltos, cada chamador refazia a
// comparação do seu jeito, e um deles ia esquecer o local.
// A duração entra com padrão de 60 min pra não obrigar quem só compara horário/local/preço a
// falar dela — era a duração implícita de toda aula antes de 10/08/2026.
public readonly record struct MudancaDaAula(
    DateTime QuandoAntes, DateTime QuandoDepois,
    string LocalAntes, string LocalDepois,
    decimal PrecoAntes, decimal PrecoDepois,
    int DuracaoAntes = 60, int DuracaoDepois = 60)
{
    public bool MudouHorario => QuandoAntes != QuandoDepois;
    public bool MudouLocal => LocalAntes != LocalDepois;
    public bool MudouPreco => PrecoAntes != PrecoDepois;

    // Aula que passou de 1h pra 2h mudou pro aluno tanto quanto se tivesse trocado de hora:
    // ele precisa segurar a agenda até mais tarde.
    public bool MudouDuracao => DuracaoAntes != DuracaoDepois;
    public bool MudouAlgo => MudouHorario || MudouLocal || MudouPreco || MudouDuracao;

    // O Google só precisa saber de horário, duração e local — o preço não vai pro evento.
    public bool MudouOQueVaiProGoogle => MudouHorario || MudouLocal || MudouDuracao;
}
