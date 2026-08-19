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

    // Só confere o TAMANHO. Serve pra consulta e pra normalização — não pra deixar entrar.
    // Quem grava gente nova usa CpfEhValido (abaixo).
    public static bool CpfTemFormatoValido(string? valor) => SomenteDigitos(valor).Length == 11;

    // O CPF pra APARECER numa tela: •••.456.789-••, só o miolo.
    //
    // Nasceu com /Admin/Acesso, onde o admin procura por NOME e recebe uma lista de até dez
    // pessoas. Ali o CPF serve pra uma coisa só — conferir "é esse mesmo?" — e o miolo já
    // responde isso. Imprimir os onze dígitos de dez pessoas por busca transformaria uma tela
    // de suporte num extrator de documentos: quem entrasse ali com má intenção sairia com uma
    // lista pronta, e ninguém precisa disso pra devolver o acesso de alguém.
    //
    // O que não tem 11 dígitos volta como veio: é o CPF "EX000000123" de quem excluiu a conta
    // (ExclusaoDeConta.CpfDeQuemSaiu), e mascará-lo esconderia justamente o que ele denuncia.
    public static string CpfMascarado(string? valor)
    {
        var cpf = SomenteDigitos(valor);
        return cpf.Length != 11 ? (valor ?? "") : $"•••.{cpf[3..6]}.{cpf[6..9]}-••";
    }

    // O CPF de verdade, com dígito verificador. Só ter 11 números não prova nada: foi assim
    // que, em 03/08/2026, entrou no torneio real um jogador chamado "." com um CPF inventado.
    // CPF errado é pior do que parece — é por ele que o parceiro sem conta assume o próprio
    // cadastro depois (ver o pré-cadastro em AuthController.Cadastro). Errado, o histórico
    // fica preso num fantasma.
    //
    // Antes isto não era feito "pra não rejeitar os CPFs de teste da homologação". Os seeds
    // e os testes gravam direto pelo EF, sem passar por aqui, então a validação não os
    // alcança — o motivo não se sustentava.
    public static bool CpfEhValido(string? valor)
    {
        var cpf = SomenteDigitos(valor);
        if (cpf.Length != 11) return false;

        // 11111111111, 00000000000 e afins passam na conta dos dígitos por acidente
        // matemático, e são o que se digita quando se quer enrolar.
        if (cpf.All(d => d == cpf[0])) return false;

        return DigitoConfere(cpf, ateOnde: 9) && DigitoConfere(cpf, ateOnde: 10);
    }

    // Regra da Receita: soma ponderada decrescente, resto 11; resto < 2 vira dígito 0.
    private static bool DigitoConfere(string cpf, int ateOnde)
    {
        var soma = 0;
        for (var i = 0; i < ateOnde; i++)
        {
            soma += (cpf[i] - '0') * (ateOnde + 1 - i);
        }

        var resto = soma * 10 % 11;
        if (resto == 10) resto = 0;

        return resto == cpf[ateOnde] - '0';
    }
}
