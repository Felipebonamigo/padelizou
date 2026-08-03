namespace Padelizou.Services;

// Apagar um local de aula de vez. Até 03/08/2026 só existia "Desativar", e isso não cobria
// o caso mais comum de todos: errar o nome no primeiro cadastro. O local errado ficava lá
// pra sempre, desativado, atrapalhando a lista.
//
// A trava é o histórico. Aula aponta pro local com Restrict — o banco recusa e o professor
// levaria um erro 500 no lugar de uma explicação. Então a regra é decidida aqui, antes:
// local sem nenhuma aula some de verdade; local com aula não some, porque some junto o
// registro de quanto ele ganhou lá.
public static class RemocaoDeLocal
{
    // Devolve o motivo para NÃO apagar, ou null quando pode. Recebe a contagem (e não o
    // local) pra ser testável sem banco: quem chama é que consulta.
    public static string? MotivoParaNaoApagar(int quantidadeDeAulas)
    {
        if (quantidadeDeAulas <= 0) return null;

        var aulas = quantidadeDeAulas == 1 ? "1 aula" : $"{quantidadeDeAulas} aulas";
        return $"Esse local já tem {aulas} na sua agenda, e apagar levaria junto o histórico "
             + "do que você ganhou lá. Use Desativar: ele some da marcação de aula e das "
             + "telas novas, e o que já passou continua registrado.";
    }

    // O que apagar leva junto. Aparece na confirmação — a pessoa tem que saber antes de
    // clicar que os horários da semana e as ofertas de pacote vão embora com o local.
    public static string Consequencia(int quantidadeDeHorarios, int quantidadeDePacotes)
    {
        var partes = new List<string>();
        if (quantidadeDeHorarios > 0)
            partes.Add(quantidadeDeHorarios == 1 ? "1 horário" : $"{quantidadeDeHorarios} horários");
        if (quantidadeDePacotes > 0)
            partes.Add(quantidadeDePacotes == 1 ? "1 pacote" : $"{quantidadeDePacotes} pacotes");

        return partes.Count == 0
            ? "Apagar este local?"
            : $"Apagar este local? Vai junto: {string.Join(" e ", partes)}.";
    }
}
