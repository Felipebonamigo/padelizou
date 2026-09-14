using Padelizou.Models;

namespace Padelizou.ViewModels;

// Primeiros passos do jogador novo. Some sozinho quando tudo está feito — não é uma tela,
// é um cartão no painel que guia até o jogador estar realmente usando o app.
public class OnboardingVM
{
    public List<PassoOnboardingVM> Passos { get; set; } = new();

    public int Concluidos => Passos.Count(p => p.Concluido);
    public int Total => Passos.Count;
    public int Percentual => Total == 0 ? 100 : (int)Math.Round(Concluidos * 100.0 / Total);

    // Terminou tudo: o cartão não aparece mais.
    public bool Completo => Concluidos == Total;

    // O próximo passo a fazer — é ele que ganha destaque no cartão.
    public PassoOnboardingVM? Proximo => Passos.FirstOrDefault(p => !p.Concluido);
}

public class PassoOnboardingVM
{
    // A identidade do passo, estável entre versões — é por ela que "pulei este" é gravado.
    // O título não serve: ele muda, e um passo pulado não pode virar outro por causa disso.
    public PassoDoOnboarding Chave { get; set; }

    // Nem todo passo tem saída. A régua é uma só (Services/PulosDoOnboarding), lida pela tela
    // E pelo POST — esconder o botão não fecha o endpoint.
    public bool PodeSerPulado => Padelizou.Services.PulosDoOnboarding.PodeSerPulado(Chave);

    public string Titulo { get; set; } = "";
    public string Explicacao { get; set; } = "";
    public string Icone { get; set; } = "bi-check-circle";
    public string TextoBotao { get; set; } = "Fazer agora";

    // Para onde o botão leva. Nulo = passo sem ação direta (ex: instalar o app).
    public string? Controller { get; set; }
    public string? Action { get; set; }

    public bool Concluido { get; set; }
}
