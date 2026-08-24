using System.Text;

namespace Padelizou.Services;

// O formato de CSV que o Excel brasileiro abre certo de primeira.
//
// Existe porque são três decisões que parecem detalhe e não são — e que já estavam
// duplicadas entre o extrato do organizador e o relatório do bar:
//
//   1. SEPARADOR PONTO E VÍRGULA. O Excel em português usa a vírgula como separador
//      DECIMAL; com CSV separado por vírgula ele joga a planilha inteira numa coluna só.
//   2. VÍRGULA NO DECIMAL. "1234.50" chega como texto e não soma; "1234,50" vira número.
//   3. BOM NO COMEÇO. Sem ele o Excel lê o arquivo como Latin-1 e todo acento vira lixo —
//      "Comanda nº 7 do André" chega como "Comanda nÂº 7 do AndrÃ©".
//
// Nada disso é padrão CSV; é o que faz o arquivo abrir sem ninguém precisar de instrução.
public static class ArquivoCsv
{
    // Texto que pode conter ponto e vírgula, aspas ou quebra de linha. Aspas dobram.
    public static string Campo(string? valor) =>
        "\"" + (valor ?? "").Replace("\"", "\"\"") + "\"";

    // Dinheiro com dois decimais e vírgula. Sempre F2: "12,5" e "12,50" na mesma coluna é o
    // tipo de coisa que faz o contador desconfiar do arquivo inteiro.
    public static string Dinheiro(decimal valor) => valor.ToString("F2").Replace('.', ',');

    public static string Data(DateTime valor) => valor.ToString("dd/MM/yyyy");

    // Os bytes prontos pro download, com o BOM na frente.
    public static byte[] Bytes(StringBuilder conteudo) =>
        Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(conteudo.ToString()))
            .ToArray();
}
