using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// O que a tela /Admin/Acesso mostra: uma pessoa e o porquê de ela não estar entrando.
public class AcessoDoJogadorVM
{
    public string? Busca { get; set; }

    // Já apertou "Procurar"? Distingue a tela recém-aberta de "procurei e não achei" — sem
    // isso as duas seriam a mesma tela vazia, e a segunda é uma RESPOSTA.
    public bool Procurou { get; set; }

    // Achou uma pessoa só (ou o admin escolheu na lista).
    public Jogador? Achado { get; set; }

    // Achou várias — nome não é único, e escolher sozinho mandaria o admin responder ao
    // homônimo errado, que é o engano que uma tela de suporte não pode cometer calada.
    public List<Jogador> Candidatos { get; set; } = new();

    public SituacaoDeAcesso Situacao => DiagnosticoDeAcesso.De(Achado);
}
