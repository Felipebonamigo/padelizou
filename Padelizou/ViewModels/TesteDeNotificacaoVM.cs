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

    // Mais de uma pessoa casou com o termo (acontece ao buscar por nome). A tela pede pra
    // escolher em vez de chutar — teste que foi pro João errado é pior que teste nenhum,
    // porque o admin conclui que testou.
    public List<Jogador> Candidatos { get; set; } = new();
}

// Como identificar cada candidato na lista sem expor dado desnecessário: o CPF aparece
// mascarado, o suficiente pra desempatar dois homônimos sem escrever o documento inteiro
// numa tela que alguém pode estar mostrando pra outra pessoa.
public static class LinhaDoCandidato
{
    public static string Detalhe(Jogador j)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(j.Login)) partes.Add(j.Login!);
        if (!string.IsNullOrWhiteSpace(j.Email)) partes.Add(j.Email!);
        if (!string.IsNullOrWhiteSpace(j.Cpf) && j.Cpf!.Length == 11)
            partes.Add($"CPF •••.{j.Cpf.Substring(3, 3)}.•••-••");

        return partes.Count > 0 ? string.Join(" · ", partes) : "sem login nem e-mail no cadastro";
    }
}
