using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// QUEM FOI POSTO NUMA INSCRIÇÃO POR OUTRA PESSOA — uma linha por (inscrição, pessoa).
//
// 🗣️ Felipe, 16/09/2026: *"'Você foi inscrito para um torneio por Maickel' — para quando alguem
// inscrever um parceiro no torneio, avisar o parceiro e permitir recusar, ao recusar o primeiro
// fica sozinho no torneio e o avisa"*.
//
// POR QUE ISTO EXISTE, e não duas colunas na Dupla: a pergunta é POR PESSOA, e a Dupla guarda
// duas pessoas em duas colunas. Qualquer atributo por pessoa vira ou duas colunas amarradas ao
// SLOT, ou uma tabela. E o slot ANDA: quando o titular sai, o parceiro é promovido a
// `Jogador1Id` (ver DesistenciaDeInscricao) — uma coluna `Jogador1ConfirmouEm` passaria, calada,
// a falar de outra pessoa. Aqui a chave é o JOGADOR, e promoção nenhuma mexe nela.
//
// ⚠️ A CHAVE COMPOSTA É A REGRA, não índice de enfeite: "uma pergunta por pessoa por inscrição"
// mora aqui, e não num `if` de C# que o clique duplo escapa — degrau 4 da escada do CLAUDE.md,
// a mesma forma da PK de TorneioMarcador.
//
// ⚠️ SEM LINHA = ninguém a inscreveu: ou ela mesma clicou, ou a inscrição é anterior a esta
// tabela, ou nasceu sem autor (importação, cobrança antiga). Nos três casos a tela não mostra
// faixa nenhuma e a porta da recusa não abre — quem quer sair usa o "sair da dupla" de sempre.
[Table("InscritoPorOutro")]
public class InscritoPorOutro
{
    public int DuplaId { get; set; }

    // QUEM foi posto na inscrição. É a chave da pergunta, e é por ela que a tela decide mostrar
    // a faixa — nunca pelo slot (Jogador1/Jogador2), que muda de dono quando alguém sai.
    public int JogadorId { get; set; }

    // QUEM inscreveu. Coluna simples, SEM FK pra Jogador — o mesmo motivo escrito no
    // `ImpedimentoAlteradoPorId` da Dupla e no `SolicitadoPorId` do pedido de equipe: já existe
    // caminho de cascade demais saindo de Jogador, e o que importa aqui é o registro histórico
    // de quem respondeu por esta inscrição.
    public int InscritoPorId { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // Nulo = ainda não respondeu. Preenchido pelo "está certo" da tela — é só o que TIRA A
    // FAIXA, e não um contrato: quem confirmou e mudou de ideia continua podendo recusar
    // enquanto as inscrições estiverem abertas.
    public DateTime? ConfirmadoEm { get; set; }

    public virtual Dupla Dupla { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}
