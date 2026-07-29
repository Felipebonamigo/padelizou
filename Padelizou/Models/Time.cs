namespace Padelizou.Models;

public class Time
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty; // Ex: "Nata Padel"

    // Clube "sede" do time — opcional (o dono pode ou não informar).
    public int? ClubeId { get; set; }
    public Clube? Clube { get; set; }

    // Caminho da imagem do logo (ex: "/uploads/logos-time/xxx.png").
    public string? Logo { get; set; }

    // Lista de jogadores que compõem esse grupo/time
    public ICollection<Jogador> Jogadores { get; set; } = new List<Jogador>();

    // Quem administra o time — pode ser mais de um, e todos com o mesmo poder.
    // Time sem nenhum (os 44 importados do ranking) só ganha o primeiro pela mão de um
    // admin do sistema; daí em diante um administrador convida o próximo.
    public ICollection<TimeAdministrador> Administradores { get; set; } = new List<TimeAdministrador>();

}
