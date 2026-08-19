using Padelizou.Models;
using padelizou.Models;   // Aula ficou no namespace legado, em minúsculo

namespace Padelizou.ViewModels;

// A "Minha Agenda" do professor: calendário (como o do Google) ou lista de eventos em
// ordem, nos dois casos filtrando por dia, semana ou mês.
public class AgendaProfessorVM
{
    public string Vista { get; set; } = "calendario";   // calendario | lista
    public string Periodo { get; set; } = "semana";     // dia | semana | mes
    public DateTime Referencia { get; set; }            // dia âncora da janela na tela

    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }                   // exclusivo
    public string Titulo { get; set; } = "";

    // Aulas dentro da janela, já em ordem cronológica.
    public List<Aula> NoPeriodo { get; set; } = new();

    // Pendências de qualquer data. Ficam FORA da janela de propósito: uma solicitação de
    // aula pro mês que vem some se o professor estiver olhando esta semana, e ele perderia
    // o prazo de responder sem nunca ver.
    public List<Aula> Pendentes { get; set; } = new();

    // A fila de reposição: aulas que o aluno vai recuperar e que ainda não têm dia marcado
    // (ver Services/Reposicao). Fora da janela pelo mesmo motivo das pendentes — a aula que
    // ficou devendo é quase sempre de outra semana, e sumiria da tela justamente de quem
    // precisa encaixá-la.
    public List<Aula> ARecuperar { get; set; } = new();

    // Os locais ativos do professor, só pro formulário de encaixe. Vem vazio quando não há
    // fila: é a única coisa na tela que precisa deles.
    public List<LocalAula> Locais { get; set; } = new();

    public bool GoogleConectado { get; set; }

    // Quantas aulas futuras confirmadas ainda não têm evento no Google. Só faz sentido com a
    // conta conectada — desconectado, a resposta seria "todas", e isso não é notícia.
    //
    // Existe porque a falha de envio era MUDA: o evento nascia junto com a aula, e quando
    // aquele instante dava errado (conta ainda não conectada, chamada recusada) nada tentava
    // de novo e nada aparecia na tela.
    public int AulasForaDoGoogle { get; set; }

    public List<Aula> Do(DateTime dia) =>
        NoPeriodo.Where(a => a.DataHora.Date == dia.Date).OrderBy(a => a.DataHora).ToList();

    public List<Aula> Em(DateTime dia, int hora) =>
        NoPeriodo.Where(a => a.DataHora.Date == dia.Date && a.DataHora.Hour == hora)
                 .OrderBy(a => a.DataHora).ToList();

    public IEnumerable<IGrouping<DateTime, Aula>> PorDia() =>
        NoPeriodo.GroupBy(a => a.DataHora.Date).OrderBy(g => g.Key);
}
