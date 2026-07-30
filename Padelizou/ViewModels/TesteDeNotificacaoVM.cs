using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// A tela de teste de aviso do painel. O formulário volta PREENCHIDO junto do resultado: quem
// testa raramente testa uma vez só — troca um canal, manda de novo, troca a pessoa.
public class TesteDeNotificacaoVM
{
    public string? Identificador { get; set; }
    public bool PorPush { get; set; } = true;
    public bool PorWhatsApp { get; set; } = true;
    public string? Mensagem { get; set; }

    public string? Erro { get; set; }

    // Quem recebeu. Mostrado de volta na tela pra o admin conferir que acertou a pessoa —
    // digitar login na mão erra fácil.
    public Jogador? Alvo { get; set; }

    public ResultadoTesteNotificacao? Resultado { get; set; }
}
