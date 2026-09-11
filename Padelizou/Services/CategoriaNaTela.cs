namespace Padelizou.Services;

// Como a categoria do torneio APARECE: em que ordem, e com que nome.
//
// As duas coisas juntas porque as duas são só de tela — o nome gravado não muda, e é ele que
// as regras leem (SexoDoJogador.ExigeUmDeCada, FaixasDePadelimetro, CategoriaDoRankingRs).
//
// Existe por uma queixa concreta do Felipe (08/08/2026), olhando o NATA PADEL TOUR no celular:
// a "4ª Categoria Feminina" aparecia no FIM da lista, depois da 7ª, porque a lista saía na
// ordem em que as categorias foram criadas — e ela foi acrescentada depois. E cada opção
// ocupava duas linhas na tela, então a lista não cabia.
public static class CategoriaNaTela
{
    // O nome curto: sai a palavra "Categoria", que não informa nada dentro de um campo que já
    // se chama Categoria — e é justamente ela que empurra o texto pra segunda linha no
    // celular. "4ª Categoria Masculina" vira "4ª Masculina"; "Categoria Mista A" vira
    // "Mista A".
    //
    // ⚠️ SÓ EXIBIÇÃO. O nome gravado continua inteiro: é por ele que as regras reconhecem
    // Mista e Casais, e é ele que vai pro de-para do Ranking RS.
    public static string Curto(string? nome)
    {
        var texto = (nome ?? "").Trim();

        // No meio ("4ª Categoria Masculina") e no começo ("Categoria Mista A") — os dois
        // formatos existem no catálogo.
        texto = texto.Replace(" Categoria ", " ", StringComparison.OrdinalIgnoreCase);
        if (texto.StartsWith("Categoria ", StringComparison.OrdinalIgnoreCase))
            texto = texto["Categoria ".Length..];

        return texto.Trim();
    }

    // A ordem em que as categorias se leem: pelo NÍVEL, da mais forte pra mais fraca, com a
    // masculina na frente da feminina do mesmo degrau — e mista e casais fechando a lista.
    //
    // ⚠️ ATÉ 10/09/2026 ERA AO CONTRÁRIO: agrupava por sexo primeiro (todas as masculinas, e só
    // então as femininas). 🗣️ Felipe, olhando o seletor de "Chaves e Grupos" do 2ª Etapa ER
    // Padel Tour: *"esta fora de ordem"*. Num torneio com as duas escadas, agrupar por sexo
    // empurra a 3ª Feminina pra DEPOIS da 6ª Masculina — quem procura a chave da 3ª acha duas
    // escadas de níveis em vez de uma.
    //
    // ⚠️ A COMPARAÇÃO É A ORDEM DOS CAMPOS DA TUPLA — é `Nivel` primeiro por isso, e não por
    // acaso. Trocar os dois de lugar muda a ordem de todas as listas de categoria do site.
    //
    // ⚠️ Deduzida do NOME, e não de um campo, porque a `Categoria` DO TORNEIO não guarda tipo
    // nem nível — ela copia só nome e código do catálogo. É a mesma limitação que faz
    // SexoDoJogador casar pelo nome.
    public static (int Nivel, int Grupo, string Nome) Ordem(string? nome)
    {
        var texto = (nome ?? "").Trim();

        int grupo =
            Contem(texto, "Masculin") ? 0 :
            Contem(texto, "Feminin") ? 1 :
            FaixasDePadelimetro.EhMista(texto) ? 2 :
            FaixasDePadelimetro.EhCasal(texto) ? 3 : 4;

        return (Nivel(texto), grupo, texto);
    }

    // De que escada a categoria é: "Masculino", "Feminino" — ou null pra Mista e Casais, que
    // não são de um sexo só. Lê o mesmo `Grupo` da ordem, pra não existir uma SEGUNDA regra
    // dizendo o que é masculina e o que é feminina.
    public static string? SexoDaEscada(string? nome) => Ordem(nome).Grupo switch
    {
        0 => SexoDoJogador.Masculino,
        1 => SexoDoJogador.Feminino,
        _ => null,
    };

    // Open é o topo (0); depois 2ª..7ª pelo próprio número; Iniciantes fecha a escada. O que
    // não tem nível — Mista A, Casais — cai num degrau só e se desempata pelo nome, que é o
    // que põe "Mista A" antes de "Mista B".
    private static int Nivel(string texto)
    {
        if (Contem(texto, "Open")) return 0;
        if (Contem(texto, "Iniciantes")) return 90;

        // O número vem sempre antes do "ª" ("4ª Categoria Masculina").
        int posicao = texto.IndexOf('ª');
        if (posicao > 0 && char.IsDigit(texto[posicao - 1]))
            return texto[posicao - 1] - '0';

        return 50;
    }

    private static bool Contem(string texto, string parte) =>
        texto.Contains(parte, StringComparison.OrdinalIgnoreCase);
}
