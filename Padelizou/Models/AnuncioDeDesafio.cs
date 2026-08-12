using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// "Nós dois queremos jogar nesta semana" — o anúncio que a dupla publica no mural de Desafios.
//
// Regras completas em DESAFIOS.md, na raiz do repo. Mudou a regra? Muda LÁ primeiro.
//
// ⚠️ DUAS COISAS QUE PARECEM DETALHE E NÃO SÃO:
//
//  · O ANÚNCIO PRECISA DOS DOIS. Ele nasce `Rascunho` e só aparece no mural depois que o
//    parceiro entra com a PRÓPRIA conta e confirma pelo link (`ConviteToken`, mesmo mecanismo
//    do ConviteDeParceiro). Sem isso eu anunciaria o Lucas pra jogar sábado às 8h e ele
//    descobriria por um push de desafio JÁ ACEITO.
//
//  · `Expirado` NÃO É STATUS. Um anúncio vale até `ValeAte` e quem responde "acabou?" é o
//    relógio, na leitura — nunca uma coluna virada por job. É a lição de LeadsComerciais: job
//    que falha calado transforma anúncio vivo em anúncio morto sem ninguém saber. E anúncio
//    vencido NÃO cancela desafio já aceito: aquilo é compromisso entre quatro pessoas e
//    sobrevive ao anúncio que o gerou (ver Services/SemanaDoDesafio).
[Table("AnuncioDeDesafio")]
public class AnuncioDeDesafio
{
    // Publicado por um, ainda sem o "sim" do parceiro. Não aparece pra mais ninguém.
    //
    // ⚠️ Rascunho é só o caminho do CONVITE POR LINK. Quem escolhe o parceiro direto na tela
    // publica de uma vez — ver `ParceiroConfirmouEm`.
    public const string Rascunho = "Rascunho";
    public const string Publicado = "Publicado";
    public const string Cancelado = "Cancelado";

    // Achou jogo. É o fim de vida do anúncio "até alguém aceitar": ele não tem data pra
    // vencer, então quem o tira do mural é o aceite de um desafio.
    public const string Fechado = "Fechado";

    public int Id { get; set; }

    // Quem publicou. É sempre uma pessoa de verdade logada — não existe anúncio de terceiro.
    public int Jogador1Id { get; set; }

    // O parceiro. Nulo enquanto ele não aceitou o convite (caminho do link).
    public int? Jogador2Id { get; set; }

    // Quando o parceiro DISSE SIM. Nulo com `Jogador2Id` preenchido = ele foi ESCOLHIDO na
    // tela e ainda não se manifestou (decisão do Felipe, 12/08/2026: dá pra montar a dupla sem
    // pedir licença, e o parceiro tira o anúncio do ar se não quiser).
    //
    // ⚠️ Esta coluna é o que permite a tela falar a verdade pros dois lados: pro parceiro,
    // "te incluíram numa dupla — se não quiser, remova"; e pra quem publicou, "seu parceiro
    // ainda não confirmou". Sem ela, "escolhido" e "aceitou" seriam o mesmo estado, e ninguém
    // saberia a quem cobrar o quê.
    public DateTime? ParceiroConfirmouEm { get; set; }

    // O convite pendente. Limpo no aceite, pra um link usado não fechar uma segunda dupla.
    // Nulo desde o começo quando a dupla já nasceu fechada pela escolha direta.
    public string? ConviteToken { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // Fim da semana anunciada (domingo 23:59:59). Ver Services/SemanaDoDesafio.
    //
    // ⚠️ NULO = "até alguém aceitar": o anúncio não vence no relógio. Quem o tira do mural é o
    // primeiro desafio ACEITO, que o vira `Fechado`. É a terceira opção do "quando vocês querem
    // jogar" (pedido do Felipe, 12/08/2026) — e a razão de ela existir é que a semana é um
    // prazo bom pra quem tem agenda e péssimo pra quem só quer jogar quando aparecer.
    public DateTime? ValeAte { get; set; }

    public string Status { get; set; } = Rascunho;

    // Recado livre da dupla: "preferência por quadra coberta", "topamos jogar cedo".
    public string? Observacao { get; set; }

    [ForeignKey(nameof(Jogador1Id))]
    public virtual Jogador Jogador1 { get; set; } = null!;

    [ForeignKey(nameof(Jogador2Id))]
    public virtual Jogador? Jogador2 { get; set; }

    // ⚠️ As três listas abaixo são CÓPIAS das preferências do jogador, não uma leitura delas.
    // O formulário nasce marcado com o que a pessoa já respondeu no perfil (JogadorCategoria,
    // JogadorCidade, JogadorClube), mas grava aqui. Lendo do perfil, alguém mudando as
    // preferências na quinta faria o anúncio publicado na segunda passar a dizer outra coisa —
    // e a dupla receberia desafio numa categoria que nunca aceitou.
    //
    // Lista VAZIA = sem restrição (aceita qualquer uma), mesmo padrão de JogadorCategoria.
    public virtual ICollection<AnuncioDesafioCategoria> Categorias { get; set; } = new List<AnuncioDesafioCategoria>();
    public virtual ICollection<AnuncioDesafioCidade> Cidades { get; set; } = new List<AnuncioDesafioCidade>();
    public virtual ICollection<AnuncioDesafioClube> Clubes { get; set; } = new List<AnuncioDesafioClube>();
}
