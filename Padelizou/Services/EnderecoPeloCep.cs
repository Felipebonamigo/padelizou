using Padelizou.Models;

namespace Padelizou.Services;

// O que fazer com o CEP que veio do formulário. Existe como serviço, e não como código no
// controller, porque DUAS telas gravam endereço (o cadastro e o perfil) — e regra de cadastro
// duplicada é a origem dos bugs graves deste sistema.
public static class EnderecoPeloCep
{
    // Recado pra quem digitou um CEP que não existe. Não recusa a gravação: o resto do
    // formulário é bom e o CEP é opcional — devolver a tela inteira por causa dele seria
    // fazer o campo opcional custar mais caro que os obrigatórios.
    public const string NaoEncontrado =
        "Não encontramos esse CEP, então ele não foi salvo. O resto do seu cadastro foi.";

    // Aplica o resultado da consulta no jogador e devolve o aviso, se houver.
    //
    // ⚠️ Quando o CEP é encontrado, a CIDADE DELE MANDA — não a digitada. É o ponto do
    // trabalho todo: o ViaCEP escreve "Gravataí" sempre igual, enquanto o campo livre produz
    // "GRAVATAI", "Gravatai" e "gravatai". Também fecha a porta de mandar um CEP e uma cidade
    // que não combinam.
    public static string? Aplicar(Jogador jogador, string? cepDigitado, ConsultaDeCepResultado consulta,
        bool enderecoPublico)
    {
        jogador.EnderecoPublico = enderecoPublico;

        var digitos = Documentos.SomenteDigitosOuNulo(cepDigitado);

        // Apagou o CEP: some com o endereço junto. Deixar rua e bairro órfãos guardaria o
        // endereço de quem acabou de pedir pra tirá-lo.
        if (digitos == null)
        {
            jogador.Cep = null;
            jogador.Logradouro = null;
            jogador.Bairro = null;
            return null;
        }

        switch (consulta.Situacao)
        {
            case ResultadoDoCep.Achou:
                var endereco = consulta.Endereco!;
                jogador.Cep = endereco.Cep;
                jogador.Logradouro = endereco.Logradouro;
                jogador.Bairro = endereco.Bairro;
                jogador.Cidade = endereco.Cidade;
                jogador.Estado = endereco.Uf;
                return null;

            case ResultadoDoCep.NaoExiste:
                // Não grava CEP inventado — ele viraria endereço errado que ninguém revisa.
                jogador.Cep = null;
                jogador.Logradouro = null;
                jogador.Bairro = null;
                return NaoEncontrado;

            default:
                // Fora do ar: guarda o CEP como veio e deixa rua/bairro pra próxima gravação.
                // Não avisa nada — não há nada que a pessoa possa fazer a respeito.
                jogador.Cep = digitos;
                return null;
        }
    }
}
