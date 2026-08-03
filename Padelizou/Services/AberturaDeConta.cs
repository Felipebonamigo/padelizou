using Padelizou.Models;

namespace Padelizou.Services;

// O que precisa estar em ordem ANTES de bater no meio de pagamento.
//
// Existe separado porque errar aqui custa caro: o Asaas recusa o cadastro inteiro por um CEP
// com dígito a menos, e a pessoa recebe uma mensagem em linguagem de gateway sem saber qual
// dos nove campos estava errado. Barrar antes é mais barato e explica melhor.
public static class AberturaDeConta
{
    // Faturamento mensal declarado. O Asaas exige o campo; o valor é estimativa do próprio
    // organizador e não muda nada do nosso lado — serve pro cadastro dele lá.
    public const decimal FaturamentoMinimo = 1m;

    // O que já temos no cadastro e não vamos pedir de novo. Null = está tudo lá.
    public static string? FaltaNoPerfil(Jogador jogador)
    {
        if (string.IsNullOrWhiteSpace(jogador.Nome) || !NomeDePessoa.Parece(jogador.Nome))
            return "Seu perfil precisa do seu nome completo.";

        if (string.IsNullOrWhiteSpace(jogador.Email))
            return "Seu perfil precisa de um e-mail — é por ele que a conta é ativada.";

        if (!Documentos.CpfEhValido(jogador.Cpf))
            return "Seu perfil precisa de um CPF válido.";

        // Celular é obrigatório no cadastro do meio de pagamento, e muita conta antiga do
        // Padelizou nasceu sem ele.
        if (Documentos.SomenteDigitos(jogador.Celular).Length < 10)
            return "Seu perfil precisa de um celular com DDD.";

        return null;
    }

    // Idade que o meio de pagamento aceita pra abrir conta. Recusar aqui, com o motivo em
    // português, é melhor do que deixar o gateway devolver a recusa dele.
    public const int IdadeMinima = 18;
    private const int IdadeMaxima = 110;

    // O que é pedido na hora e não fica guardado. Null = pode seguir.
    public static string? ProblemaNoFormulario(
        decimal? faturamentoMensal, DateTime? dataNascimento,
        string? cep, string? endereco, string? numero, string? bairro, DateTime? hoje = null)
    {
        if (faturamentoMensal is not decimal fat || fat < FaturamentoMinimo)
            return "Informe quanto você fatura por mês, mais ou menos — pode ser estimativa.";

        // Não está na lista de obrigatórios da documentação, mas a API recusa sem ela.
        if (dataNascimento is not DateTime nascimento)
            return "Informe sua data de nascimento.";

        var idade = Idade(nascimento, (hoje ?? DateTime.Today).Date);
        if (idade < IdadeMinima)
            return $"É preciso ter {IdadeMinima} anos ou mais para abrir a conta de recebimento.";
        if (idade > IdadeMaxima)
            return "Confira a data de nascimento.";

        if (Documentos.SomenteDigitos(cep).Length != 8)
            return "O CEP precisa ter 8 dígitos.";

        if (string.IsNullOrWhiteSpace(endereco))
            return "Informe o endereço (rua ou avenida).";

        if (string.IsNullOrWhiteSpace(numero))
            return "Informe o número — se não tiver, escreva S/N.";

        if (string.IsNullOrWhiteSpace(bairro))
            return "Informe o bairro.";

        return null;
    }

    // Aniversário que ainda não chegou este ano ainda não contou — sem isso, quem faz 18 em
    // dezembro seria barrado o ano inteiro.
    private static int Idade(DateTime nascimento, DateTime hoje)
    {
        var idade = hoje.Year - nascimento.Year;
        if (nascimento.Date > hoje.AddYears(-idade)) idade--;
        return idade;
    }

    // Monta o que vai pro meio de pagamento juntando cadastro + formulário.
    public static DadosDaSubconta Montar(Jogador jogador, decimal faturamentoMensal,
        DateTime dataNascimento, string cep, string endereco, string numero, string bairro) =>
        new(
            Nome: NomeDePessoa.Arrumar(jogador.Nome),
            Email: (jogador.Email ?? "").Trim(),
            CpfCnpj: jogador.Cpf,
            Celular: jogador.Celular ?? "",
            DataNascimento: dataNascimento.Date,
            FaturamentoMensal: faturamentoMensal,
            Cep: cep.Trim(),
            Endereco: endereco.Trim(),
            Numero: numero.Trim(),
            Bairro: bairro.Trim());

    // O recado depois que deu certo. Prometer "pronto, pode receber" seria mentira: o titular
    // ainda ativa por e-mail e manda documento, e quem não avisa isso gera o suporte de
    // "criei a conta e não caiu nada".
    public const string DepoisDeCriar =
        "Conta criada! Você vai receber um e-mail para ativá-la e enviar seus documentos — "
        + "sem isso o dinheiro não é liberado. Enquanto não terminar, siga combinando por fora.";
}
