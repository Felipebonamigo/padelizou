namespace Padelizou.Services;

// Aviso de "ainda estamos testando" no topo do site. Ligado por padrão: enquanto o
// Padelizou não abrir de verdade, quem entra precisa saber que o que está vendo pode
// mudar. Cada ambiente sobrescreve o texto no systemd (prod e dev não são a mesma coisa).
public class BetaSettings
{
    public bool Habilitado { get; set; } = true;
    public string Rotulo { get; set; } = "Beta";
    // ⚠️ CURTO POR OBRIGAÇÃO, não por estilo: a faixa aparece em TODA tela do site, e o texto
    // de 67 caracteres virava duas linhas no celular, mais uma do link — ~170px antes do nome
    // do torneio (Deivid, 10/09/2026: "diminui um pouco a aba dos bugs", "tá muito longo").
    // Cada ambiente sobrescreve isto no systemd; quem escrever o de lá tem o mesmo teto.
    public string Texto { get; set; } = "Em testes — as coisas ainda mudam por aqui.";

    // Este é o ambiente de TESTE (dev.padelizou.com.br), não o Padelizou de verdade.
    //
    // Nasce FALSO: quem tem que se declarar é a cópia, não o original. Se fosse ao
    // contrário, um ambiente novo criado sem a chave se passaria por produção — e o erro
    // caro aqui é alguém inscrever gente de verdade, cobrar de verdade e marcar torneio de
    // verdade num banco que é apagado sem aviso.
    //
    // Ligado, o portão de entrada abre com um aviso grande dizendo o que é aquilo. É na
    // porta de propósito: dentro do site as duas telas são idênticas, e a hora em que a
    // pessoa ainda pode perceber que errou de endereço é antes de entrar.
    public bool AmbienteDeTeste { get; set; }
}
