namespace Padelizou.Services;

// DE QUE LADO A PESSOA JOGA — o vocabulário num lugar só.
//
// Já existia espalhado: `Jogador.LadoQuadra` guarda "Esquerda"/"Direita"/"Ambos" como texto
// livre, a panelinha copia isso pra `ConfirmacaoSessao.Lado`, e o cartão compartilhável tem a
// própria tradução (`CartaoDoJogador.RotuloDoLado`, com o mesmo `StartsWith("esq")`). Texto
// livre comparado por prefixo em três lugares é como um deles passa a discordar dos outros —
// e ninguém percebe, porque nada quebra: só some um selo da tela.
//
// Aqui entra o quarto uso (o lado de quem procura parceiro no torneio), e ele nasce com a
// régua compartilhada em vez de uma quarta cópia.
public static class LadoNaQuadra
{
    // Os valores como já são gravados hoje no perfil — mudar isso exigiria migração de dados,
    // e não há motivo: o que faltava era a régua, não outro vocabulário.
    public const string Esquerda = "Esquerda";
    public const string Direita = "Direita";
    public const string Ambos = "Ambos";

    // ⚠️ QUALQUER texto que não comece com "esq" ou "dir" vira `Ambos`, inclusive nulo e
    // vazio. É o lado seguro: "não sei de que lado essa pessoa joga" e "ela joga dos dois" dão
    // no mesmo pra quem procura parceiro — nos dois casos não dá pra descartar ninguém.
    public static string Normalizar(string? lado)
    {
        var t = (lado ?? "").Trim().ToLowerInvariant();
        if (t.StartsWith("esq")) return Esquerda;
        if (t.StartsWith("dir")) return Direita;
        return Ambos;
    }

    // O lado que VALE pra uma inscrição.
    //
    // ⚠️ `ladoDaInscricao` NULO não é "tanto faz": é "essa pessoa nunca escolheu nada aqui" —
    // inscrição feita antes de a escolha existir. Nesse caso vale o perfil, que era o pedido
    // do Felipe ("os que já estão inscritos, coloca o que ele tem no perfil"). Quem escolhe
    // "Ambos" na inscrição grava "Ambos", e aí o perfil não é consultado: a escolha explícita
    // de jogar dos dois lados não pode ser sobrescrita por um perfil que diz "Direita".
    //
    // O efeito colateral é bom: quem nunca escolheu e depois arruma o perfil vê a inscrição
    // acompanhar sozinha, sem UPDATE nenhum no banco.
    public static string Efetivo(string? ladoDaInscricao, string? ladoDoPerfil) =>
        string.IsNullOrWhiteSpace(ladoDaInscricao)
            ? Normalizar(ladoDoPerfil)
            : Normalizar(ladoDaInscricao);

    // O que a lista do torneio mostra embaixo do "Procurando parceiro".
    //
    // Mostra o lado DELE, não o que ele procura: é o fato, e a conta do complemento cada um
    // faz sozinho. Dizer "procura esquerda" seria supor que quem joga na direita só quer o
    // oposto — e dupla de dois flexíveis existe.
    public static string? Rotulo(string lado) => lado switch
    {
        Esquerda => "Joga na esquerda",
        Direita => "Joga na direita",
        // `Ambos` não vira selo: numa lista de 9 inscrições, "joga em qualquer lado" repetido
        // em todas é ruído que empurra pra baixo a informação que separa uma da outra.
        _ => null,
    };

    // A FRASE INTEIRA, pro perfil e pra busca de jogadores.
    //
    // ⚠️ EXISTE PORQUE COLAR O VALOR CRU DAVA ERRO DE CONCORDÂNCIA (Felipe, 21/08/2026): as duas
    // telas escreviam `"Joga do lado " + LadoQuadra.ToLower()` e saía "joga do lado esquerda" e
    // "joga do lado ambos". Os valores gravados são femininos porque descrevem a QUADRA ("a
    // esquerda da quadra"), e "lado" é masculino — o texto não podia sair de uma colagem. E
    // "Ambos" não é lado nenhum: é os dois, e pede outra frase inteira.
    //
    // Mora aqui junto do resto do vocabulário de lado pelo motivo escrito no topo do arquivo:
    // texto livre montado dentro de cada tela é como uma delas passa a discordar das outras.
    public static string Frase(string? lado) => Normalizar(lado) switch
    {
        Esquerda => "Joga do lado esquerdo",
        Direita => "Joga do lado direito",
        _ => "Joga em ambos os lados",
    };

    // As opções do seletor, na ordem em que fazem sentido pra quem está se inscrevendo.
    public static IReadOnlyList<(string Valor, string Texto)> Opcoes { get; } = new[]
    {
        (Direita, "Direita"),
        (Esquerda, "Esquerda"),
        (Ambos, "Tanto faz / jogo dos dois"),
    };
}
