namespace Padelizou.Services;

// Normaliza CPF e celular vindos de formulário. A máscara do navegador (wwwroot/js/mascaras.js)
// é só visual e pode não rodar — JS desligado, colar texto de fora, POST direto. Como a coluna
// CPF aceita 11 caracteres, "111.444.777-35" chegando cru estoura o INSERT e derruba a página,
// então a limpeza tem que acontecer aqui também.
public static class Documentos
{
    public static string SomenteDigitos(string? valor) =>
        new((valor ?? "").Where(char.IsDigit).ToArray());

    // Vazio vira null pra não gravar string em branco onde a coluna é opcional.
    public static string? SomenteDigitosOuNulo(string? valor)
    {
        var limpo = SomenteDigitos(valor);
        return limpo.Length == 0 ? null : limpo;
    }

    // Só confere o tamanho: validar dígito verificador rejeitaria os CPFs de teste que o
    // próprio time usa em homologação.
    public static bool CpfTemFormatoValido(string? valor) => SomenteDigitos(valor).Length == 11;
}
