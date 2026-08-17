using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// Duas duplas marcando um jogo que vale ranking. Regras completas em DESAFIOS.md.
//
// ⚠️ A DUPLA DAQUI NÃO É `Dupla`. A entidade `Dupla` é casada com Torneio, Categoria,
// GrupoTorneio e o motor de mata-mata — reusá-la aqui criaria a segunda cópia de meia dúzia
// de regras de torneio, e regra de torneio duplicada é a origem dos bugs graves deste sistema.
// O desafio guarda os quatro JogadorId direto; a identidade da dupla, pro ranking, é a chave
// canônica de Services/ChaveDaDupla.
//
// ⚠️ `Expirado` também NÃO é status aqui: proposta sem resposta em 48h morre pelo relógio
// (Services/EstadoDoDesafio), não por job.
[Table("Desafio")]
public class Desafio
{
    // ---- Os estados ----
    public const string Proposto = "Proposto";
    public const string Aceito = "Aceito";
    public const string Recusado = "Recusado";
    public const string Cancelado = "Cancelado";

    // Alguém lançou o placar e a outra dupla ainda não respondeu. Vira Confirmado sozinho
    // depois do prazo — ver EstadoDoDesafio.
    public const string PlacarLancado = "Placar lançado";

    // Contou. É o único estado que vale ponto.
    public const string Confirmado = "Confirmado";

    // As duas duplas discordam do placar. NINGUÉM pontua e a linha vai pra /Admin/DesafiosEmDisputa:
    // duas duplas discordando é problema humano, e o sistema não escolhe um lado sozinho.
    public const string EmDisputa = "Em disputa";

    // O admin olhou a disputa e decidiu que o jogo não vale. Ninguém pontua, e a linha fica no
    // histórico com quem decidiu e por quê.
    //
    // ⚠️ Não é `Cancelado`: cancelado é jogo que NÃO ACONTECEU (alguém desmarcou). Aqui o jogo
    // aconteceu e o placar é que não pôde ser estabelecido — misturar os dois faria o histórico
    // do jogador dizer que ele furou um compromisso que na verdade ele jogou.
    public const string Anulado = "Anulado";

    // Marcaram e alguém não apareceu. Zero ponto, e o selo fica no histórico.
    public const string SemComparecimento = "Sem comparecimento";

    public int Id { get; set; }

    // De qual anúncio do mural este desafio nasceu. Nulo = desafio direto (dupla desafiando
    // dupla sem passar pelo mural). O anúncio pode vencer ou ser cancelado depois — por isso
    // é `SetNull`, e não cascade: apagar o anúncio não pode levar junto o jogo combinado.
    public int? AnuncioDeDesafioId { get; set; }

    public int DesafianteJogador1Id { get; set; }
    public int DesafianteJogador2Id { get; set; }
    public int DesafiadoJogador1Id { get; set; }
    public int DesafiadoJogador2Id { get; set; }

    // Em que categoria o jogo vale. Sai da interseção do que as duas duplas aceitam.
    public int CategoriaPadraoId { get; set; }

    // Onde. Obrigatório de propósito: é o que permite o ranking por clube e a reserva de
    // quadra sair daqui depois (clube com MarcacaoHorariosAtiva).
    public int ClubeId { get; set; }

    public DateTime DataHora { get; set; }

    // "1 set" | "Melhor de 3 sets" | "Games combinados" — ver Services/FormatoDoDesafio.
    public string Formato { get; set; } = null!;

    public string Status { get; set; } = Proposto;

    public DateTime PropostoEm { get; set; } = DateTime.Now;
    public DateTime? RespondidoEm { get; set; }

    // Recado de quem propôs ("a gente reserva a quadra", "topam 20h também?").
    public string? Observacao { get; set; }

    // ---- O placar ----
    // Mesma forma da Partida de torneio (sets + games, nuláveis) pra que a régua de quem
    // venceu seja LITERALMENTE a mesma — ver Services/QuemVenceu.Lado.
    public int? SetsDesafiante { get; set; }
    public int? SetsDesafiado { get; set; }
    public int? GamesDesafiante { get; set; }
    public int? GamesDesafiado { get; set; }

    public int? LancadoPorId { get; set; }
    public DateTime? LancadoEm { get; set; }
    public int? ConfirmadoPorId { get; set; }
    public DateTime? ConfirmadoEm { get; set; }

    // Por que a outra dupla contestou. Vai pra tela do admin.
    public string? MotivoDaDisputa { get; set; }

    // ---- Como a disputa foi resolvida ----
    //
    // ⚠️ Existe porque decisão humana sobre briga entre usuários PRECISA ter autor e
    // justificativa. "Por que esse placar virou 6x4 se eu contestei?" é a primeira pergunta que
    // chega, e responder "o admin mudou" sem saber QUEM nem POR QUÊ é como se perde a confiança
    // no ranking inteiro.
    //
    // `MotivoDaDisputa` NÃO é sobrescrito: ele é o que a dupla alegou, e continua ao lado do
    // que o admin decidiu. Guardar os dois é o que permite reler a história depois.
    public int? ResolvidoPorId { get; set; }
    public string? ResolucaoDaDisputa { get; set; }

    // ---- Os pontos, congelados ----
    // Gravados no encerramento, nunca recalculados na leitura — mesmo padrão do
    // PrecoPorJogoCotado. Sem isso, mexer na fórmula em novembro reescreveria o ranking de
    // agosto inteiro, em silêncio. Nulos enquanto o desafio não fecha.
    //
    // Nascem na fase 1 sem ninguém preencher (o ranking é a fase 2), de propósito: coluna
    // nova depois obrigaria migration num módulo que já tem dado real.
    public int? PontosDesafiante { get; set; }
    public int? PontosDesafiado { get; set; }

    [ForeignKey(nameof(AnuncioDeDesafioId))]
    public virtual AnuncioDeDesafio? Anuncio { get; set; }

    [ForeignKey(nameof(DesafianteJogador1Id))]
    public virtual Jogador DesafianteJogador1 { get; set; } = null!;

    [ForeignKey(nameof(DesafianteJogador2Id))]
    public virtual Jogador DesafianteJogador2 { get; set; } = null!;

    [ForeignKey(nameof(DesafiadoJogador1Id))]
    public virtual Jogador DesafiadoJogador1 { get; set; } = null!;

    [ForeignKey(nameof(DesafiadoJogador2Id))]
    public virtual Jogador DesafiadoJogador2 { get; set; } = null!;

    [ForeignKey(nameof(CategoriaPadraoId))]
    public virtual CategoriaPadrao CategoriaPadrao { get; set; } = null!;

    [ForeignKey(nameof(ClubeId))]
    public virtual Clube Clube { get; set; } = null!;

    // Os quatro envolvidos, na ordem em que a tela mostra. Serve pra "sou parte disto?" sem
    // repetir a mesma cadeia de `||` em cada ação do controller.
    [NotMapped]
    public IEnumerable<int> Envolvidos =>
        new[] { DesafianteJogador1Id, DesafianteJogador2Id, DesafiadoJogador1Id, DesafiadoJogador2Id };

    [NotMapped]
    public IEnumerable<int> LadoDesafiante => new[] { DesafianteJogador1Id, DesafianteJogador2Id };

    [NotMapped]
    public IEnumerable<int> LadoDesafiado => new[] { DesafiadoJogador1Id, DesafiadoJogador2Id };
}
