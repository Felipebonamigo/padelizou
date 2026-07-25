namespace Padelizou.Models;

public class Time
{
    public int Id { get; set; }
    public string Nome { get; set; } // Ex: "Nata Padel"

    // Clube "sede" do time — opcional (o dono pode ou não informar).
    public int? ClubeId { get; set; }
    public Clube? Clube { get; set; }

    // Dono/criador do time (JogadorId). Só ele edita nome/logo/clube do time.
    // Coluna simples (sem FK) pra não criar caminho de cascade duplo com Jogador.TimeId.
    public int? DonoId { get; set; }

    // Caminho da imagem do logo (ex: "/uploads/logos-time/xxx.png").
    public string? Logo { get; set; }

    // Lista de jogadores que compõem esse grupo/time
    public ICollection<Jogador> Jogadores { get; set; } = new List<Jogador>();

}
