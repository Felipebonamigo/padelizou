using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// A tela de teste de aviso do painel, em DOIS tempos: primeiro acha a pessoa e mostra quem
// achou, depois manda. Numa etapa só, o admin descobria pra quem tinha mandado só depois de
// já ter mandado — e num teste isso é tarde.
public class TesteDeNotificacaoVM
{
    public string? Identificador { get; set; }
    public bool PorPush { get; set; } = true;
    public bool PorWhatsApp { get; set; } = true;
    public string? Mensagem { get; set; }

    public string? Erro { get; set; }

    // Passo 1: quem a busca achou. Com mais de um, a tela pede pra escolher — nome não é
    // único, e mandar pro homônimo errado é pior que não mandar, porque o admin conclui
    // que testou.
    public List<Jogador> Candidatos { get; set; } = new();

    // Passo 2: quem está escolhido agora. Fica na tela, em destaque, ANTES do botão de
    // enviar existir.
    public Jogador? Selecionado { get; set; }

    // Passo 3: quem de fato recebeu, com o que aconteceu em cada canal.
    public Jogador? Alvo { get; set; }
    public ResultadoTesteNotificacao? Resultado { get; set; }

    // O resultado da varredura de fantasmas, quando ela acabou de rodar. Mora nesta tela
    // porque é a mesma pergunta do teste ("o aviso chega?") vista do outro lado.
    public RelatorioDaVarredura? Varredura { get; set; }

    // Deu pra entregar em algum canal? É o que decide entre o aviso verde e o amarelo —
    // "mandei" sem dizer se chegou é a informação que menos serve numa tela de teste.
    public bool AlgumCanalDeuCerto =>
        Resultado != null
        && (TextoDoTeste.DeuCerto(Resultado.Push) || TextoDoTeste.DeuCerto(Resultado.WhatsApp));
}

// Como identificar cada candidato na lista sem expor dado desnecessário: o CPF aparece
// mascarado, o suficiente pra desempatar dois homônimos sem escrever o documento inteiro
// numa tela que alguém pode estar mostrando pra outra pessoa.
public static class LinhaDoCandidato
{
    public static string Detalhe(Jogador j)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(j.Apelido)) partes.Add($"\"{j.Apelido}\"");
        if (!string.IsNullOrWhiteSpace(j.Login)) partes.Add(j.Login!);
        if (!string.IsNullOrWhiteSpace(j.Email)) partes.Add(j.Email!);
        if (!string.IsNullOrWhiteSpace(j.Cpf) && j.Cpf!.Length == 11)
            partes.Add($"CPF •••.{j.Cpf.Substring(3, 3)}.•••-••");

        return partes.Count > 0 ? string.Join(" · ", partes) : "sem login nem e-mail no cadastro";
    }

    // O que o teste tem chance de alcançar nessa pessoa, ANTES de mandar. Evita o teste que
    // só serve pra descobrir que não tinha como chegar.
    public static string Alcance(Jogador j)
    {
        if (!AvisoPorWhatsApp.PodeReceber(j.NotificarWhatsApp, j.Celular))
        {
            return string.IsNullOrWhiteSpace(j.Celular)
                ? "sem celular no cadastro — WhatsApp não vai chegar"
                : "desmarcou o WhatsApp nas preferências";
        }

        return $"WhatsApp: {WhatsAppLinkHelper.Formatar(j.Celular)}";
    }
}
