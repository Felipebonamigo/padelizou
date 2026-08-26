namespace Padelizou.Services;

// Como o nome APARECE na tela. Não mexe no que está gravado.
//
// Na lista de inscritos do "Interno Los Corneteiros" conviviam "ALAN DA SILVEIRA MACHADO" e
// "charls gustavio polese" — cada um digita como quer, e no meio de uma lista isso fica feio.
//
// Só a EXIBIÇÃO muda, de propósito: o nome é dado da pessoa, ela escreveu daquele jeito, e
// reescrever a coluna seria irreversível pra consertar uma questão de aparência. Aqui, se um
// dia a regra estiver errada pra alguém, é só mudar esta classe.
public static class NomeBonito
{
    // Partículas de nome brasileiro ficam minúsculas: "Alan da Silveira Machado", não
    // "Alan Da Silveira Machado". Nunca na primeira posição — ali é o começo do nome.
    private static readonly HashSet<string> Particulas = new(StringComparer.OrdinalIgnoreCase)
    {
        "da", "de", "do", "das", "dos", "e", "di", "du", "del", "della",
        "van", "von", "der", "la", "le", "y",
    };

    // Sufixos de geração: são do sobrenome, não sobrenome. "Otávio Wunsch Junior" encurtado
    // pelo primeiro-e-último viraria "Otávio Junior", que não identifica ninguém — o que
    // identifica é "Otávio Wunsch".
    private static readonly HashSet<string> Sufixos = new(StringComparer.OrdinalIgnoreCase)
    {
        "junior", "júnior", "jr", "jr.", "filho", "neto", "sobrinho", "segundo", "terceiro",
        "ii", "iii", "iv",
    };

    // O nome curto pra listas: PRIMEIRO e ÚLTIMO, sem os do meio.
    //
    // "Anderson Matteus Schwaab" vira "Anderson Schwaab"; "Frederico Siqueira de Paula
    // Vargas" vira "Frederico Vargas". Numa tabela de grupo ou numa vaga de chaveamento o
    // nome inteiro empurra a linha pra três alturas ou é cortado no meio — e o pedaço que
    // sobra costuma ser o do meio, que é justamente o que menos identifica a pessoa.
    //
    // Não toca em quem tem dois nomes: "Marcos Coelho" continua "Marcos Coelho".
    public static string Curto(string? nome)
    {
        var formatado = Formatar(nome);
        if (string.IsNullOrEmpty(formatado)) return "";

        var palavras = formatado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length <= 2) return formatado;

        // O sobrenome é a última palavra que não é partícula ("de", "dos") nem sufixo de
        // geração — as duas coisas viajam coladas nele.
        int ultimo = palavras.Length - 1;
        while (ultimo > 0 && (Particulas.Contains(palavras[ultimo]) || Sufixos.Contains(palavras[ultimo])))
            ultimo--;

        return ultimo == 0 ? palavras[0] : $"{palavras[0]} {palavras[ultimo]}";
    }

    // O nome como as telas mostram: **curto + apelido entre parênteses**.
    //
    // "José Carlos da Silva" com apelido "Zeca" → "José Silva (Zeca)".
    //
    // Até 06/08/2026 quem tinha apelido aparecia SÓ pelo apelido. Mudou porque apelido não
    // identifica ninguém fora da turma: "Zeca" pode ser três pessoas no mesmo torneio, e quem
    // lê a chave de fora não sabe de quem se trata. O apelido continua ali porque no padel
    // muita gente é conhecida só por ele — mas agora acompanhando o nome, não no lugar dele.
    public static string ComApelido(string? nome, string? apelido)
    {
        var curto = Curto(nome);
        var alcunha = Formatar(apelido);

        if (alcunha.Length == 0) return curto;
        if (curto.Length == 0) return alcunha;

        // Apelido igual ao nome curto não vira "José Silva (José Silva)". Acontece com quem
        // preenche o apelido repetindo o nome pra "garantir".
        if (string.Equals(alcunha, curto, StringComparison.CurrentCultureIgnoreCase)) return curto;

        return $"{curto} ({alcunha})";
    }

    // SÓ O APELIDO — e o nome curto quando não há apelido.
    //
    // ⚠️ ISTO NÃO REVOGA A DECISÃO DE 06/08/2026 explicada em ComApelido logo acima. Lá o
    // argumento é que apelido não identifica ninguém DE FORA da turma ("Zeca" pode ser três
    // pessoas no mesmo torneio), e ele continua valendo em torneio, chave e ranking — que é
    // onde ComApelido é usado.
    //
    // Este método existe pro caso oposto, e é o único lugar onde ele se aplica hoje: a lista de
    // jogos DE UMA PANELINHA, lida de dentro do grupo, por gente que se conhece pelo apelido,
    // numa linha que já disputa espaço com data, placar, quatro nomes e dois botões (pedido do
    // Felipe, 26/08/2026: "pode ser só o apelido"). Quem chama é ConvidadoNoJogo.ApelidoNaTela.
    //
    // Sem apelido cai no nome curto de propósito: sumir da linha não é opção.
    public static string ApelidoOuCurto(string? nome, string? apelido)
    {
        var alcunha = Formatar(apelido);
        return alcunha.Length > 0 ? alcunha : Curto(nome);
    }

    public static string Formatar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";

        var palavras = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var saida = new List<string>(palavras.Length);

        for (var i = 0; i < palavras.Length; i++)
        {
            var palavra = palavras[i];

            // "e" e "da" no meio do nome ficam minúsculos; no começo, não — quem se chama
            // "Del Rey" perderia a maiúscula do próprio primeiro nome.
            if (i > 0 && Particulas.Contains(palavra))
            {
                saida.Add(palavra.ToLowerInvariant());
                continue;
            }

            saida.Add(PalavraFormatada(palavra));
        }

        return string.Join(' ', saida);
    }

    // Nome composto por hífen ou apóstrofo é uma palavra só pro espaço, mas duas pra caixa:
    // "ana-maria" tem que virar "Ana-Maria", e "d'avila" vira "D'Avila".
    private static string PalavraFormatada(string palavra)
    {
        if (!Ajustavel(palavra)) return palavra;

        var resultado = new System.Text.StringBuilder(palavra.Length);
        bool comecoDePedaco = true;

        foreach (var c in palavra)
        {
            if (comecoDePedaco)
            {
                resultado.Append(char.ToUpperInvariant(c));
                comecoDePedaco = false;
            }
            else
            {
                resultado.Append(char.ToLowerInvariant(c));
            }

            if (c is '-' or '\'' or '’' or '.') comecoDePedaco = true;
        }

        return resultado.ToString();
    }

    // O ponto mais importante desta classe: só mexe em palavra que está TODA maiúscula ou
    // TODA minúscula. Quem tem maiúscula no meio escreveu assim de propósito — "McDonald",
    // "DiCaprio", "D'Ávila" — e passar o rolo compressor viraria "Mcdonald", estragando
    // justamente o nome de quem se deu ao trabalho de digitar certo.
    private static bool Ajustavel(string palavra)
    {
        var letras = palavra.Where(char.IsLetter).ToList();
        if (letras.Count == 0) return false;

        bool todaMaiuscula = letras.All(char.IsUpper);
        bool todaMinuscula = letras.All(char.IsLower);

        // "Mateus" (só a inicial maiúscula) já está no formato de destino — passar por aqui
        // devolve o mesmo texto, então tratar como ajustável não muda nada e simplifica.
        bool soAInicialMaiuscula = char.IsUpper(letras[0]) && letras.Skip(1).All(char.IsLower);

        return todaMaiuscula || todaMinuscula || soAInicialMaiuscula;
    }
}
