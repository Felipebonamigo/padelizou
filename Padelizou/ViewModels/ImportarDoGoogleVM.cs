using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// A tela de conferência da importação do Google Agenda (ver AulasController.Importacao).
public class ImportarDoGoogleVM
{
    // Só os candidatos: o que já virou aula (id presente em Aula.GoogleEventId) foi cortado
    // no controller, senão o professor confirmaria uma aula que já existe.
    public List<EventoDaAgenda> Eventos { get; set; } = new();

    // Os locais ATIVOS do professor — consulta própria, e não o Model.Locais da agenda, que
    // só é carregado quando há fila de reposição e viria vazio aqui.
    public List<LocalAula> Locais { get; set; } = new();

    public DateTime De { get; set; }
    public DateTime Ate { get; set; }
}
