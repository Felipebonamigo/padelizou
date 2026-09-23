using padelizou.Models;

namespace Padelizou.Models;

// "Nós registramos os resultados para você" — o organizador contrata o Padelizou pra mandar
// alguém lançar os jogos durante o torneio.
//
// É uma SOLICITAÇÃO, não uma compra. O botão diz "verificar disponibilidade" porque é isso
// que acontece: pode não haver ninguém livre naquela data, naquela cidade. Cobrar primeiro e
// descobrir depois que não temos gente seria vender o que não existe.
public class SolicitacaoRegistroResultados
{
    public const string Solicitada = "Solicitada";
    public const string Confirmada = "Confirmada";
    public const string SemDisponibilidade = "Sem disponibilidade";
    public const string Cancelada = "Cancelada";
    public const string Concluida = "Concluída";

    public int Id { get; set; }

    public int TorneioId { get; set; }
    public Torneio Torneio { get; set; } = null!;

    public string Status { get; set; } = Solicitada;

    // Fotografia do torneio no momento do pedido. O organizador pode mudar quadras e datas
    // depois, e a conta que a gente respondeu tem que continuar fazendo sentido — senão
    // ninguém sabe mais o que foi combinado.
    public int QuadrasNaSolicitacao { get; set; }
    public int DiasNaSolicitacao { get; set; }
    public int PessoasSugeridas { get; set; }

    // Quantos jogos o torneio tinha no dia do pedido. Fica NULO enquanto ninguém se
    // inscreveu — aí a tela mostra só a regra em vez de um total inventado.
    //
    // ⚠️ É FOTOGRAFIA, não cotação. O pedido sai com no mínimo 7 dias de antecedência, ou
    // seja, com as inscrições abertas: este número quase sempre nasce vazio e envelhece a cada
    // inscrição. Quem multiplica por R$ 12 é o painel da resposta, contando AO VIVO
    // (Services/JogosDoTorneio) — cotar por este aqui cobraria o mínimo de todo mundo.
    public int? JogosPrevistos { get; set; }

    // A regra vigente no dia do pedido, congelada aqui. Se amanhã o preço mudar, quem pediu
    // ontem continua valendo pelo que leu na tela. Já valeu nos dois sentidos: protegeu quem
    // tinha pedido por jogo quando o percentual entrou, e protege agora quem pediu no
    // percentual.
    //
    // A régua é POR JOGO (`PrecoPorJogoCotado`, com `PercentualCotado` nulo), menos na janela
    // de 20/08 a 23/09/2026, em que foi percentual sobre o valor das inscrições — 5% até 26/08,
    // 10% depois. As telas leem a régua que o pedido carrega, nunca a atual.
    public decimal? PercentualCotado { get; set; }
    public decimal PrecoPorJogoCotado { get; set; }
    public decimal ValorMinimoCotado { get; set; }

    // Recado do organizador: horários reais, contato, particularidades do local.
    public string? Observacoes { get; set; }

    public int SolicitadoPorId { get; set; }
    public DateTime SolicitadaEm { get; set; }

    // ── A resposta do Padelizou ───────────────────────────────────────────────────────
    public DateTime? RespondidaEm { get; set; }
    public int? RespondidaPorId { get; set; }

    // Quantas pessoas conseguimos de fato, e por quanto. O valor sai só na resposta: antes
    // de saber quem vai e de onde vem, qualquer preço na tela seria chute.
    public int? PessoasConfirmadas { get; set; }
    public decimal? ValorCombinado { get; set; }

    // Quem vai (na confirmação) ou por que não dá (na recusa). Vai pro organizador.
    public string? Resposta { get; set; }
}
