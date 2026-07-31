namespace Padelizou.Services;

// Os campos de texto que o banco corta, e o aviso amigável pra cada um.
//
// POR QUE ISTO EXISTE: algumas colunas são `character varying(n)`, não `text` — e o Postgres
// não trunca, ele **recusa**: `value too long for type character varying(30)`. Sem checagem,
// um login de 31 letras no cadastro derrubava a página com erro 500 e a pessoa perdia tudo o
// que tinha digitado, sem entender por quê. Nome comprido colado do contato do celular fazia
// o mesmo na inscrição do torneio.
//
// A tela também ganha `maxlength`, que resolve 99% dos casos (o navegador nem deixa digitar,
// e corta o que for colado). Isto aqui é a segunda tranca: formulário em cache, campo criado
// por script, POST feito à mão.
//
// Recusa com aviso em vez de cortar em silêncio: login e nome são como a pessoa vai ser
// chamada e como vai entrar. Cortar "joaodasilvajuniorneto" no meio e não avisar seria pior
// que o erro — ela só descobriria no dia em que não conseguisse entrar.
public static class LimitesDeTexto
{
    // Os números vêm do banco (ver DbPadelContext / information_schema). Mexer num deles sem
    // migração faz a coluna recusar de novo, então eles moram juntos, num lugar só.
    public const int NomeDeJogador = 100;
    public const int LoginDeJogador = 30;
    public const int NomeDeTorneio = 150;
    public const int NomeDeAlunoAvulso = 100;
    public const int TelefoneDeAlunoAvulso = 20;

    // Devolve a mensagem de erro quando passa do limite, ou null quando está tudo bem.
    // Campo vazio não é problema daqui: quem exige preenchimento é o `required` da tela e a
    // checagem de cada ação.
    public static string? Problema(string? valor, int limite, string comoSeChama)
    {
        var texto = valor?.Trim() ?? "";
        if (texto.Length <= limite) return null;

        return $"{comoSeChama} está muito {(comoSeChama.EndsWith('a') ? "longa" : "longo")}: " +
               $"são {texto.Length} caracteres e cabem {limite}. Encurte e tente de novo.";
    }
}
