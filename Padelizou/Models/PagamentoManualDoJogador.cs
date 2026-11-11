using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// "ESTE JOGADOR PAGOU" — a marca do organizador por PESSOA dentro de uma inscrição de dupla.
//
// 🗣️ Felipe, 11/11/2026: "temos que pôr o pago/não pago e cobrar para jogador
// individualmente também [...] porque normalmente a cobrança é individual".
//
// ⚠️ POR JOGADOR, E NÃO POR POSIÇÃO (1/2): trocar parceiro, aceitar convite e juntar
// inscrições mexem em Jogador1Id/Jogador2Id. Uma coluna "Jogador2Pago" passaria a marca de quem
// saiu pra quem entrou, calada. Aqui a marca fica com a pessoa — quem saiu da dupla deixa de
// contar porque a leitura só olha os dois jogadores ATUAIS.
//
// A chave composta (DuplaId, JogadorId) é o que segura o clique duplo no "Marcar pago": não há
// C# checando duplicata. `Dupla.Pago` continua sendo "a inscrição inteira está paga" — todo o
// resto do sistema (chaves, relatório, lembrete, Asaas) segue lendo só ele.
[Table("PagamentoManualDoJogador")]
public class PagamentoManualDoJogador
{
    public int DuplaId { get; set; }
    public int JogadorId { get; set; }
    public DateTime MarcadoEm { get; set; } = DateTime.Now;

    [ForeignKey("DuplaId")]
    public virtual Dupla Dupla { get; set; } = null!;
}
