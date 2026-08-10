namespace Padelizou.Services;

// A conta do card "Na prática", da tela do plano do professor — a que responde "então quanto
// isso me custa por mês?".
//
// ⚠️ ELA MUDOU DE DONO CONFORME O `ModoComissao`, E A VERSÃO ANTERIOR NÃO SABIA DISSO. O card
// dizia "o Avulso paga R$ 200,00 de taxa; o Assinante paga R$ 109,90" sem dizer **quem** paga —
// e no modo padrão ("Somada") quem paga não é o professor, é o ALUNO: o preço da aula sobe e o
// professor recebe o valor cheio. Os R$ 200 nunca saíram do bolso dele. Quem lia aquilo achava
// que a mensalidade economizava R$ 90 do próprio dinheiro; economizava do dinheiro do aluno.
//
// A conta só fica igual à antiga no "Descontada", onde a taxa sai mesmo da fatia dele.
//
// ⚠️ Fica aqui, e não na Razor, porque é uma AFIRMAÇÃO SOBRE DINHEIRO com dois caminhos — o
// tipo de coisa que na tela ninguém testa e que passa anos dizendo o número errado pra metade
// das pessoas.
public static class ExemploDoPlanoNaTela
{
    // Números redondos de propósito: a comparação tem que ser conta de padaria, não planilha.
    public const int Aulas = 20;
    public const decimal PrecoDaAula = 100m;

    // `TaxaSaiDoAluno` é o que decide a FRASE; os quatro números são o que ela mostra.
    // Os "custo do professor" já incluem a mensalidade, porque é isso que ele quer comparar.
    public record Conta(
        int Aulas,
        decimal PrecoDaAula,
        bool TaxaSaiDoAluno,
        decimal AlunoPagaAvulso,
        decimal AlunoPagaAssinante,
        decimal CustoMensalAvulso,
        decimal CustoMensalAssinante);

    // `modoDoProfessor` é o `Jogador.ModoComissao` (pode ser nulo) e `modoPadrao` vem do
    // AsaasSettings — a mesma dupla que a tela de Configurar usa. Nulo cai no padrão, que
    // hoje é "Somada"; é essa combinação que faz o caso mais comum ser o do aluno pagando.
    public static Conta Montar(string? modoDoProfessor, string? modoPadrao, PlanoProfessorSettings cfg)
    {
        var modo = string.IsNullOrWhiteSpace(modoDoProfessor) ? modoPadrao : modoDoProfessor;
        var descontada = string.Equals(modo, "Descontada", StringComparison.OrdinalIgnoreCase);

        var taxaAvulso = PrecoDaAula * cfg.PercentualAvulso / 100m;
        var taxaAssinante = PrecoDaAula * cfg.PercentualAssinantePix / 100m;

        if (descontada)
        {
            // A taxa sai da fatia dele: o aluno paga o anunciado e o professor recebe menos.
            return new Conta(Aulas, PrecoDaAula,
                TaxaSaiDoAluno: false,
                AlunoPagaAvulso: PrecoDaAula,
                AlunoPagaAssinante: PrecoDaAula,
                CustoMensalAvulso: Aulas * taxaAvulso,
                CustoMensalAssinante: cfg.MensalidadeAssinante + Aulas * taxaAssinante);
        }

        // "Somada": o aluno paga preço + taxa e o professor recebe o valor cheio. Do bolso do
        // professor sai SÓ a mensalidade — e no Avulso não sai nada.
        return new Conta(Aulas, PrecoDaAula,
            TaxaSaiDoAluno: true,
            AlunoPagaAvulso: PrecoDaAula + taxaAvulso,
            AlunoPagaAssinante: PrecoDaAula + taxaAssinante,
            CustoMensalAvulso: 0m,
            CustoMensalAssinante: cfg.MensalidadeAssinante);
    }
}
