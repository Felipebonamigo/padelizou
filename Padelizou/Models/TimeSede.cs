namespace Padelizou.Models;

// Onde o time joga em casa — e são VÁRIOS lugares, não um.
//
// Substitui o antigo Time.ClubeId, que só cabia um. O time de verdade treina no Batata na
// terça e no Nata no sábado; com uma coluna só, quem cadastrasse o segundo apagaria o
// primeiro, e a tela do time contaria uma meia-verdade sem ninguém perceber.
//
// ⚠️ A coluna ANTIGA foi derrubada na mesma migration que criou esta tabela (ver
// SedesDoTimeETransferencias), e não ficou por perto "por garantia": duas fontes de verdade
// pra "onde é a sede?" é exatamente o que o Time.DonoId já custou a este projeto uma vez.
// Quem lê sede lê daqui; quem escreve escreve por Services/SedesDoTime.
public class TimeSede
{
    public int TimeId { get; set; }
    public Time Time { get; set; } = null!;

    public int ClubeId { get; set; }
    public Clube Clube { get; set; } = null!;
}
