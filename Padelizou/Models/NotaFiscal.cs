using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Uma nota fiscal que o clube precisa emitir — na fila, emitida, rejeitada ou cancelada.
//
// ── POR QUE ISTO É UMA FILA, E NÃO UMA CHAMADA DIRETA ────────────────────────────────────
// É a regra de desenho mais importante de todo o módulo fiscal: **a venda nunca trava por
// causa da nota.** A comanda fecha sempre; a nota nasce aqui como Pendente e é enviada
// depois, fora do caminho de quem está no balcão.
//
// Sem isso, o balcão do clube ficaria refém da SEFAZ — que cai justamente na sexta à noite,
// que é quando o bar fatura. Um sistema que não deixa vender porque a Receita está fora do
// ar não é um sistema fiscal: é um sistema que fecha o bar do cliente.
//
// ── E POR QUE O RETORNO VEM POR WEBHOOK ──────────────────────────────────────────────────
// O provedor cobra por REQUISIÇÃO, não por nota emitida (ver FISCAL.md). Ficar perguntando
// "já autorizou?" em laço dobra o custo — no piloto chega a ser mais caro que o concorrente.
// Então quem avisa é ele: a gente manda uma vez e espera o retorno.
[Table("NotaFiscal")]
public class NotaFiscal
{
    // O que sai. Mercadoria no balcão é NFC-e; serviço (aula, quadra, mensalidade) é NFS-e.
    public const string Nfce = "NFC-e";
    public const string Nfse = "NFS-e";

    // ── Os estados, e o que cada um significa de verdade ──
    // Pendente   → na fila, ainda não foi mandada.
    // Enviada    → saiu daqui e está com o provedor; o retorno vem por webhook.
    // Autorizada → tem número, chave e XML. É nota de verdade.
    // Rejeitada  → a SEFAZ recusou. O motivo está em Mensagem, e alguém precisa OLHAR.
    // Cancelada  → foi autorizada e depois cancelada dentro do prazo legal.
    // Manual     → desistimos de tentar sozinhos (ver Tentativas). Vai pra fila humana.
    public const string Pendente = "Pendente";
    public const string Enviada = "Enviada";
    public const string Autorizada = "Autorizada";
    public const string Rejeitada = "Rejeitada";
    public const string Cancelada = "Cancelada";
    public const string Manual = "Manual";

    public int Id { get; set; }

    public int ClubeId { get; set; }
    public virtual Clube Clube { get; set; } = null!;

    public string Tipo { get; set; } = Nfce;

    // ── De onde a nota veio ──
    // Uma das duas está preenchida. São nuláveis e separadas (em vez de um par
    // "OrigemTipo/OrigemId" genérico) porque o banco consegue garantir a integridade de uma
    // chave estrangeira de verdade — e porque a pergunta "esta comanda já tem nota?" precisa
    // ser barata.
    public int? ComandaId { get; set; }
    public virtual Comanda? Comanda { get; set; }

    public int? PagamentoId { get; set; }
    public virtual Pagamento? Pagamento { get; set; }

    public string Status { get; set; } = Pendente;

    public decimal Valor { get; set; }

    // CPF do consumidor, quando ele pediu. Copiado da comanda no momento em que a nota entra
    // na fila: a comanda pode ser corrigida depois, e a nota tem que dizer o que foi emitido.
    public string? CpfConsumidor { get; set; }

    // ── O que o provedor devolve quando autoriza ──
    public string? Numero { get; set; }
    public string? Serie { get; set; }
    public string? ChaveAcesso { get; set; }

    // Onde o XML e o cupom impresso vivem — no provedor, não aqui. A guarda de 5 anos é
    // obrigação do clube e o provedor a inclui; duplicar o arquivo do lado de cá seria
    // assumir uma responsabilidade que já está resolvida.
    public string? UrlXml { get; set; }
    public string? UrlPdf { get; set; }

    // O identificador da nota no provedor — é por ele que o webhook encontra esta linha.
    public string? IdNoProvedor { get; set; }

    // O que a SEFAZ (ou o provedor) disse quando recusou. É o texto que a pessoa vai ler pra
    // consertar o cadastro — guardado inteiro, sem resumir.
    public string? Mensagem { get; set; }

    // ⚠️ Quantas vezes já tentamos. Existe porque REJEIÇÃO TAMBÉM CONSOME CRÉDITO: um produto
    // com NCM errado num clube movimentado viraria queima de crédito em laço, sem ninguém
    // perceber até a fatura. Passado o teto (ver FilaDeNotas.MaximoDeTentativas), a nota vai
    // pra fila humana em vez de continuar tentando.
    public int Tentativas { get; set; }

    public DateTime CriadaEm { get; set; } = DateTime.Now;
    public DateTime? EnviadaEm { get; set; }
    public DateTime? RespondidaEm { get; set; }

    [NotMapped]
    public bool EstaResolvida => Status is Autorizada or Cancelada;

    // Precisa de gente olhando: ou a SEFAZ recusou, ou desistimos de tentar sozinhos.
    [NotMapped]
    public bool PedeAtencao => Status is Rejeitada or Manual;
}
