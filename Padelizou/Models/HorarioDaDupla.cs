using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// OS HORÁRIOS QUE O ORGANIZADOR COMBINOU COM UMA DUPLA (19/11/2026).
//
// 🗣️ Felipe: *"permita aqui colocar o intervalo de horário que a dupla pode jogar, para o sistema
// tentar encaixar"* · *"temos que pensar uma forma também para quem tem impedimento no sábado à
// noite e no domingo [...] permitir ele colocar vários horários de impedimento ou vários
// 'horários que pode jogar'"* · *"só os organizadores do evento podem fazer; para usuário normal,
// segue o impedimento padrão que já tinha"*.
//
// Dois tipos na mesma tabela, porque a tela é uma lista só:
//   • `PodeJogar = true`  — "pode jogar entre": vale só nos jogos de CHAVE (grupos), e entra na
//     grade pela mesma porta do "Pôr os 2 jogos em" (Services/ConcentracaoDeJogos.Da).
//   • `PodeJogar = false` — "não pode jogar": vale em TODA fase, e entra pela porta do
//     impedimento (Services/JanelasDeImpedimento.Da).
// Os dois CEDEM se a grade apertar, como o que já existia — jogo sem horário é o único desfecho
// que a grade não aceita.
//
// 💰 Não cobra nada: é ajuste do organizador, não o impedimento pago da inscrição.
//
// ⚠️ A coleção na Dupla é AUTO-INCLUÍDA (DbPadelContext): as duas portas acima leem a Dupla
// pronta, e são uma dúzia de caminhos que carregam duplas pra grade — um Include esquecido em
// qualquer um deles faria o horário sumir calado justamente ali.
[Table("HorarioDaDupla")]
public class HorarioDaDupla
{
    public int Id { get; set; }
    public int DuplaId { get; set; }
    public bool PodeJogar { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }

    [ForeignKey("DuplaId")]
    public virtual Dupla Dupla { get; set; } = null!;
}
