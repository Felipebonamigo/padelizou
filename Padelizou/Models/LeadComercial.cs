namespace Padelizou.Models;

// O contato que um parceiro comercial registra ANTES de falar com a pessoa.
//
// Existe por uma razão só, e ela é comercial e não técnica: no Programa de Parceiros
// (PARCEIROS.md) quem indica ganha percentual do que aquele cliente gerar, e a pergunta que
// derruba o programa é "quem trouxe esse cliente?". Respondida depois da venda, ela vira a
// palavra de um contra a do outro — e os parceiros são amigos do Felipe. Respondida ANTES,
// pelo relógio do banco, ela não tem discussão.
//
// ⚠️ POR ISSO O TELEFONE É OBRIGATÓRIO. Ele é a chave de duplicata: nome de grupo de torneio
// muda ("Loberos" / "Los Loberos" / "Turma do Lobero") e não serve pra dizer que duas pessoas
// registraram o MESMO contato. Sem telefone a regra do "quem registra primeiro leva" não tem
// como ser aplicada — é a única coisa que a tela realmente garante.
//
// Nada aqui calcula comissão. Este registro só responde de quem é o cliente; quanto ele rende
// sai do Pagamento (Comissao/RecebedorId), quando essa parte for construída.
public class LeadComercial
{
    public int Id { get; set; }

    // O parceiro que indicou. É um Jogador, pela mesma razão que organizador e professor são:
    // papel comercial mora no Jogador, não em entidade própria (a entidade `Organizador` já
    // provou que separar isso só cria uma tabela morta).
    public int ParceiroId { get; set; }
    public Jogador Parceiro { get; set; } = null!;

    // Como o contato é conhecido: o nome da pessoa, do grupo que organiza ou do clube.
    public string Contato { get; set; } = null!;

    // Só dígitos, normalizado na gravação — é o que permite comparar dois registros.
    public string Telefone { get; set; } = null!;

    // "Torneio", "Professor" ou "Clube" — a régua de comissão é diferente pra cada um.
    public string Tipo { get; set; } = null!;

    public string? Observacao { get; set; }

    public DateTime RegistradoEm { get; set; } = DateTime.Now;

    // Quem digitou. Hoje é sempre um admin (a tela é do painel), e fica guardado porque
    // quando alguém reclamar de atribuição a primeira pergunta é "quem lançou isso?".
    public int? RegistradoPorId { get; set; }

    // "Aberto", "Ganho" ou "Perdido". ⚠️ "Vencido" NÃO é gravado aqui: ele é derivado da data
    // (Services/LeadsComerciais). Gravar exigiria um job pra virar a chave de todo lead às
    // 00h — e um job que falha calado transformaria contato livre em contato preso.
    public string Status { get; set; } = "Aberto";

    public DateTime? FechadoEm { get; set; }

    // Em quem o contato virou, quando ganho. É o elo que a conta de comissão vai seguir:
    // Lead → Jogador cliente → Pagamento.RecebedorId.
    public int? ClienteId { get; set; }
    public Jogador? Cliente { get; set; }

    // Por que não fechou. Serve pro Felipe saber se o produto perdeu por preço, por
    // concorrente ou por nada — três problemas diferentes.
    public string? MotivoPerda { get; set; }
}
