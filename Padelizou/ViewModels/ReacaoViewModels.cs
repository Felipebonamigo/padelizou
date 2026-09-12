namespace Padelizou.ViewModels;

// As reações de UM jogo, prontas pra fileira do card — 12/09/2026.
public class ReacoesDaPartidaVM
{
    public int PartidaId { get; set; }

    // Só os emoji que alguém usou, da pílula mais votada pra menos. É a decisão do Felipe sobre
    // a tela: num jogo sem reação nenhuma a lista vem VAZIA e o card mostra só o botão que abre
    // o teclado — numa lista de 97 jogos, uma paleta aberta em cada cartão é a poluição que ele
    // já tinha apontado nas fichas de placar em 11/09.
    public List<ReacaoContadaVM> Reacoes { get; set; } = new();

    public int Total => Reacoes.Sum(r => r.Total);
}

public class ReacaoContadaVM
{
    public string Emoji { get; set; } = null!;
    public int Total { get; set; }

    // ⚠️ FALSO TAMBÉM PRA QUEM NÃO ESTÁ LOGADO, e isso é resposta, não acidente: a pílula
    // marcada é o que o JS usa pra decidir entre Reagir e TirarReacao. Quem não tem conta vê a
    // contagem e nenhuma pílula marcada.
    public bool EuReagi { get; set; }
}

// QUEM COLOCOU O QUÊ — 🗣️ Felipe, 12/09/2026: *"e ao clicar no emoji, veja quem colocou o que,
// igual no whats app"*, com o print do painel de reações.
//
// Uma linha por REAÇÃO, e não por pessoa: quem pôs 😂 e 🔥 aparece nas duas, que é o que o
// painel do WhatsApp mostra.
public class QuemReagiuVM
{
    public List<QuemReagiuLinhaVM> Linhas { get; set; } = new();
}

public class QuemReagiuLinhaVM
{
    public string Emoji { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string? FotoPerfil { get; set; }
}
