namespace Padelizou.Services;

public class AcessoAntecipadoSettings
{
    public bool Habilitado { get; set; }
    public string Usuario { get; set; } = null!;
    public string Senha { get; set; } = null!;

    // CPF do jogador em que o visitante entra logado automaticamente depois de acertar a
    // senha do gate — modo demonstração, pra mostrar o sistema cheio sem pedir cadastro.
    // **Vazio desliga**, e aí quem entra navega deslogado e cria a própria conta se quiser:
    // é o que a gente quer em dev, onde os testes são de gente de verdade.
    public string? LoginAutomaticoCpf { get; set; }
}
