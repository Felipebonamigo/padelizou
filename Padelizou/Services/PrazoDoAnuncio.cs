namespace Padelizou.Services;

// As três respostas de "quando vocês querem jogar".
//
// Existe como constante, e não como texto solto no formulário e no `switch`, porque as duas
// pontas precisam concordar: a tela manda a string e o controller a traduz em data. Um valor
// escrito de dois jeitos vira um anúncio com o prazo errado, sem erro nenhum na tela.
public static class PrazoDoAnuncio
{
    public const string EstaSemana = "esta-semana";
    public const string ProximaSemana = "proxima-semana";

    // Sem data pra vencer. Quem tira o anúncio do mural é o primeiro desafio ACEITO, que o vira
    // `AnuncioDeDesafio.Fechado` — ver SemanaDoDesafio.
    //
    // 💡 Existe porque a semana é um prazo bom pra quem tem agenda e péssimo pra quem só quer
    // jogar quando aparecer alguém: essa pessoa republicaria o mesmo anúncio todo domingo, ou
    // (mais provável) desistiria depois da segunda vez.
    public const string AteAlguemAceitar = "ate-alguem-aceitar";
}
