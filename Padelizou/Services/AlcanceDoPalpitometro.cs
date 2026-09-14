namespace Padelizou.Services;

// ONDE O PALPITÔMETRO VALE — escolha do ORGANIZADOR, torneio a torneio (Felipe, 14/09/2026).
//
// 🗣️ *"na parte de criar torneio, coloque la para o organizador decidir se vai habilitar o
// palpitometro ou nao, se vai ser apenas da masculina/feminina ou em ambos, deixe nascendo como
// permitido e nas tanto feminino como masculino"*.
//
// ⚠️ UMA COLUNA SÓ (`Torneio.PalpitometroEm`), E NÃO UM `bool` MAIS UM ALCANCE. As duas
// perguntas do pedido ("habilita?" e "quais categorias?") cabem num valor: `Nenhuma` é a
// primeira respondida com não. Separadas em dois campos, elas poderiam DISCORDAR — desligado
// com "Feminina" gravado ao lado —, e aí existem duas verdades sobre a mesma coisa e o resto do
// código precisa escolher em qual acreditar. É a mesma decisão que o `Torneio.GamesSoDaFinal`
// tomou ao recusar um `bool FinalSeparada`, e é o que deixa a tela ser um rádio de quatro
// opções (igual ao `quemMarcaPlacar`): rádio sempre manda o marcado, sem a pegadinha do campo
// escondido que o par caixa+valor obriga.
//
// ⚠️ A RÉGUA DO SEXO É A DO PADELÍMETRO, e não uma nova: `FaixasDePadelimetro.EhFeminina` já
// responde "esta categoria é feminina?" pelo NOME (o único lugar onde esse dado existe — a
// `Categoria` não tem coluna de sexo, o nome é texto livre). Uma segunda definição aqui
// divergiria da primeira no dia em que uma das duas mudasse, e a tela mostraria o palpitômetro
// onde o Padelímetro diz que é feminina e o alcance diz que não.
//
// ⚠️ MISTA, CASAL E LENDAS ENTRAM NA MASCULINA — decisão do Felipe, perguntado em 14/09/2026. A
// régua é binária (tem "Fem" no nome, ou não), o que é o oposto de exigir "Masc" escrito: com a
// exigência, essas três ficariam sem palpitômetro nas DUAS escolhas restritas, e o organizador
// que marcou "só masculinas" não teria como descobrir por quê.
public static class AlcanceDoPalpitometro
{
    // Os nomes gravados em Torneio.PalpitometroEm — mesmo desenho de QuemMarcaOPlacar e
    // FormaDePagamentoDoTorneio: constantes num lugar só, nunca texto solto.
    public const string Nenhuma = "Nenhuma";
    public const string Todas = "Todas";
    public const string Masculina = "Masculina";
    public const string Feminina = "Feminina";

    public static bool Existe(string? alcance) =>
        alcance is Nenhuma or Todas or Masculina or Feminina;

    // ⚠️ DESCONHECIDO VIRA `Todas`, NUNCA `Nenhuma`. Dois caminhos reais chegam aqui com lixo: o
    // POST montado à mão e a linha antiga do banco. Cair em "desligado" faria o palpitômetro
    // sumir calado do torneio de quem nunca abriu esta tela — um recurso que some sem erro
    // nenhum é justamente o que ninguém vai reportar.
    public static string Normalizar(string? alcance) =>
        Existe(alcance) ? alcance! : Todas;

    // A pergunta que a tela e o servidor fazem: esta categoria tem palpitômetro?
    public static bool Libera(string? alcance, string? nomeDaCategoria) =>
        Normalizar(alcance) switch
        {
            Nenhuma => false,
            Masculina => !FaixasDePadelimetro.EhFeminina(nomeDaCategoria),
            Feminina => FaixasDePadelimetro.EhFeminina(nomeDaCategoria),
            _ => true,
        };
}
