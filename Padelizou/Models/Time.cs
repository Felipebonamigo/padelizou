namespace Padelizou.Models;

public class Time
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty; // Ex: "Nata Padel"

    // Onde o time joga em casa. Pode ser MAIS DE UM clube, e pode ser nenhum (o dono informa
    // se quiser). Era uma coluna `ClubeId` só — ver o comentário de TimeSede sobre por que ela
    // não sobreviveu. Leia/escreva por Services/SedesDoTime.
    public ICollection<TimeSede> Sedes { get; set; } = new List<TimeSede>();

    // Caminho da imagem do logo (ex: "/uploads/logos-time/xxx.png").
    public string? Logo { get; set; }

    // Lista de jogadores que compõem esse grupo/time
    public ICollection<Jogador> Jogadores { get; set; } = new List<Jogador>();

    // Quem administra o time — pode ser mais de um, e todos com o mesmo poder.
    // Time sem nenhum (os 44 importados do ranking) só ganha o primeiro pela mão de um
    // admin do sistema; daí em diante um administrador convida o próximo.
    public ICollection<TimeAdministrador> Administradores { get; set; } = new List<TimeAdministrador>();

}
