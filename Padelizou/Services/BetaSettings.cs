namespace Padelizou.Services;

// Aviso de "ainda estamos testando" no topo do site. Ligado por padrão: enquanto o
// Padelizou não abrir de verdade, quem entra precisa saber que o que está vendo pode
// mudar. Cada ambiente sobrescreve o texto no systemd (prod e dev não são a mesma coisa).
public class BetaSettings
{
    public bool Habilitado { get; set; } = true;
    public string Rotulo { get; set; } = "Beta";
    public string Texto { get; set; } = "Estamos em fase de testes — coisas podem mudar de um dia pro outro.";
}
