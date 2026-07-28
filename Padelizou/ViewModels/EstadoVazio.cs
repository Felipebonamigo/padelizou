namespace Padelizou.ViewModels;

// "Nenhum aluno ainda." aparecia como um texto cinza solto num canto, e havia 14 variações disso
// espalhadas pelas telas. Como a produção acabou de ser zerada, é o que mais gente vai ver esta
// semana — vale ser uma tela pensada, não uma sobra.
//
// O botão é opcional porque nem todo vazio tem uma saída: "nenhum aluno ainda" o professor não
// resolve clicando, mas "nenhuma cidade cadastrada" sim.
public record EstadoVazio(
    string Mensagem,
    string? TextoDoBotao = null,
    string? Controller = null,
    string? Acao = null)
{
    public bool TemBotao =>
        !string.IsNullOrWhiteSpace(TextoDoBotao) &&
        !string.IsNullOrWhiteSpace(Controller) &&
        !string.IsNullOrWhiteSpace(Acao);
}
