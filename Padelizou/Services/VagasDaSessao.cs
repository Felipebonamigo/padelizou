namespace Padelizou.Services;

// QUANTOS FALTAM PRO JOGO DA SEMANA — a conta e a frase num lugar só.
//
// O número já existia em dois lugares e não era dito em nenhum: a Home carregava
// `JogoDaSemanaVM.Vagas` sem exibir, e a tela da Semana mostrava "N na lista" sem comparar
// com nada. Quem estava sem o quarto às 19h de terça tinha que fazer a subtração de cabeça —
// que é o motivo de o convite sair tarde, quando sai.
//
// ⚠️ FALTA E SOBRA NÃO SÃO O MESMO SINAL COM O SINAL TROCADO, e é isso que a presença
// presumida (21/08/2026) impõe: a panelinha inteira nasce na lista, então uma turma de 10 com
// 4 vagas abre TODA semana com gente a mais. Uma subtração crua responderia "faltam -6" e o
// convite apareceria em destaque justamente na semana em que vai sobrar gente. Por isso as
// duas perguntas têm método próprio, as duas com piso em zero.
//
// ⚠️ SEM VAGAS CONFIGURADAS, NÃO OPINA. `GruposController.Configuracoes` força 4 quando vem
// <= 0, mas linha de banco anterior a essa guarda não passou por lá — e "faltam 4" inventado
// manda chamar gente pra uma quadra que talvez já esteja cheia. Silêncio é a resposta certa.
public static class VagasDaSessao
{
    private static bool Configurada(int vagas) => vagas > 0;

    // Quantos ainda cabem. 0 = fechou, ou já passou do número.
    public static int Faltam(int naLista, int vagas) =>
        Configurada(vagas) ? Math.Max(0, vagas - naLista) : 0;

    // Quantos passaram das vagas. 0 = cabe todo mundo.
    public static int Sobram(int naLista, int vagas) =>
        Configurada(vagas) ? Math.Max(0, naLista - vagas) : 0;

    // A pergunta que decide o DESTAQUE do botão de convidar — e só ela. Um `Faltam() > 0`
    // solto na view seria a segunda cópia da regra, e a que ficaria pra trás.
    public static bool FaltaGente(int naLista, int vagas) => Faltam(naLista, vagas) > 0;

    // A frase da tela. Nulo = não há o que dizer.
    public static string? Frase(int naLista, int vagas)
    {
        if (!Configurada(vagas)) return null;

        int faltam = Faltam(naLista, vagas);
        if (faltam > 0) return faltam == 1 ? "falta 1 pra fechar" : $"faltam {faltam} pra fechar";

        int sobram = Sobram(naLista, vagas);
        // "lista completa" e não "lista fechada": fechada faria par com "inscrições fechadas",
        // que é outra coisa e mora a duas telas daqui.
        return sobram == 0 ? "lista completa" : $"{sobram} a mais que as vagas";
    }
}
