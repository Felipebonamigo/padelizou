namespace Padelizou.Services;

// O dia da semana em três letras, do jeito que a data de um jogo precisa: "sex 11/09".
//
// 🗣️ Emerson Pisoni, 10/09/2026: *"ali na data daria pra colocar o dia da semana, não quero
// procurar pra saber se é sexta ou sábado, sou vagabundo"*. Num torneio que atravessa o fim de
// semana, "11/09" sozinho não responde a única pergunta de quem lê a lista — dá pra ir? — e
// custava abrir o calendário do celular pra descobrir.
//
// ⚠️ LISTA FIXA, e não `ToString("ddd")`: o abreviado do pt-BR sai do ICU do sistema, vem com
// ponto ("sex.") e já mudou entre versões do ICU. Aqui a linha é apertada e o texto é curto de
// propósito — três letras, sem ponto —, e o que aparece na tela não pode depender de qual
// imagem do Linux o VPS está rodando. É a MESMA lista que o Painel do Clube já escrevia à mão;
// agora existe uma só, e não duas que podem discordar.
public static class DiaDaSemana
{
    private static readonly string[] Curtos = { "dom", "seg", "ter", "qua", "qui", "sex", "sáb" };

    public static string Curto(DateTime quando) => Curtos[(int)quando.DayOfWeek];
}
