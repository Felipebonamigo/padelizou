using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A enquete de depois do torneio: quem jogou dá nota pro CLUBE (estrutura, quadras), pra
// ORGANIZAÇÃO (como o torneio rodou) e pro SISTEMA (o Padelizou), e pode escrever sobre cada
// um. Uma resposta por jogador por torneio — índice único no banco, e trocar a resposta
// enquanto a janela está aberta atualiza a mesma linha.
//
// Existe por causa de 2027: o "Melhor Clube do ano" precisa de um ano de avaliações, e esse
// relógio só começa quando a coleta começa (ver Services/EnqueteDoTorneio).
[Table("AvaliacaoDoTorneio")]
public class AvaliacaoDoTorneio
{
    public int Id { get; set; }

    public int TorneioId { get; set; }
    public int JogadorId { get; set; }

    // De 1 a 5 estrelas, como as avaliações de professor — sem meia-estrela, sem 0-10.
    public int NotaClube { get; set; }
    public int NotaOrganizacao { get; set; }

    // ⚠️ NULÁVEL de propósito, e não `int` com default. A coluna nasce depois das primeiras
    // respostas, e coluna nova nasce com ZERO: as avaliações de agosto/2026 apareceriam com
    // nota 0 pro Padelizou — fora da escala 1-5 e derrubando a média de um recurso que elas
    // nunca avaliaram. Nulo diz a verdade: essa pessoa respondeu antes de existir a pergunta.
    public int? NotaSistema { get; set; }

    // O que a pessoa escreveu. Os três são opcionais — a enquete continua respondível em
    // cinco segundos só com as estrelas, que é o que faz ela ter resposta.
    //
    // ⚠️ O texto sobre o SISTEMA não mora aqui: ele vira uma linha em `FeedbackSite`, a caixa
    // que já existe pra "o que você achou do Padelizou", com a moderação e o NPS dela. Uma
    // segunda caixa de opinião sobre o sistema seria a regra duplicada que a gente já pagou
    // caro pra não ter — duas listas, e o admin lendo só uma.
    public string? ComentarioClube { get; set; }
    public string? ComentarioOrganizacao { get; set; }

    // A ESCOLHA DE QUEM ESCREVE, uma só pra resposta inteira (decisão do Felipe, 18/08/2026).
    //
    // Anônimo = o texto chega só a quem recebe — o organizador do torneio, o dono do clube e o
    // Padelizou — e não entra no mural sozinho. Assinado = vai pro mural do torneio na hora,
    // com nome e foto de quem escreveu.
    //
    // ⚠️ O nome de quem marcou anônimo NÃO aparece em tela nenhuma, nem pra quem recebe: o
    // ponto de ter a opção é poder dizer que a quadra estava ruim sem virar assunto com o
    // organizador na semana seguinte. A coluna `JogadorId` continua ali porque ela é a trava
    // de "uma resposta por pessoa" — não é permissão pra exibir.
    public bool Anonimo { get; set; }

    // Quando o texto entrou no mural público do torneio.
    //
    // Assinado nasce publicado (a pessoa escolheu isso). Anônimo nasce nulo e só é publicado
    // se o organizador, o clube ou o Padelizou achar que vale pros outros jogadores — e aí
    // sai SEM o nome, que é o que foi combinado com quem escreveu.
    public DateTime? PublicadoEm { get; set; }

    // Quem liberou (ou escondeu por último). Sem FK, como o `DenunciadoPorId` do comentário de
    // perfil: é carimbo de auditoria, e uma FK aqui faria a exclusão de conta esbarrar nele.
    public int? PublicadoPorId { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    [ForeignKey("TorneioId")]
    public virtual Torneio Torneio { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
