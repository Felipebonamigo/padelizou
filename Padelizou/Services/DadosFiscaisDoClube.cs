using Padelizou.Models;

namespace Padelizou.Services;

// A identidade do clube perante a SEFAZ e a prefeitura — e a resposta honesta pra pergunta
// "por que a nota não sai?" ANTES de tentar emitir e levar rejeição.
//
// Quem emite é o clube, sempre. O Padelizou é o sistema que monta o documento e manda pro
// provedor; o CNPJ, o certificado e a responsabilidade pela tributação são dele. Isso não é
// detalhe jurídico solto: é o que define de quem é a culpa quando um NCM está errado, e é o
// que precisa estar escrito no contrato do plano fiscal (ver FISCAL.md).
//
// A ordem dos avisos aqui importa mais do que parece: uma tela que diz "falta CNPJ, falta
// inscrição estadual, falta endereço, falta certificado" de uma vez faz o dono do clube
// fechar a aba. O checklist é curto e por documento — quem só dá aula não precisa saber que
// falta inscrição estadual, porque NFS-e não usa.
public static class DadosFiscaisDoClube
{
    public const string Nfce = "NFC-e";
    public const string Nfse = "NFS-e";

    // CRT do emitente. MEI é Simples Nacional (1) — e é justamente por isso que o clube MEI
    // aparece aqui: ele até emite NFS-e, mas na prática não emite NFC-e, porque a atividade
    // de bar raramente cabe no MEI. Quem vende cerveja de verdade é ME, e é esse o público do
    // plano fiscal.
    public const int SimplesNacional = 1;
    public const int SimplesComExcesso = 2;
    public const int RegimeNormal = 3;

    public static readonly (int Codigo, string Rotulo)[] Regimes =
    {
        (SimplesNacional, "Simples Nacional (inclui MEI)"),
        (SimplesComExcesso, "Simples Nacional — excesso de sublimite"),
        (RegimeNormal, "Regime Normal (Lucro Presumido ou Real)")
    };

    public static bool RegimeValido(int? regime) => regime != null && Regimes.Any(r => r.Codigo == regime);

    public static string RotuloDoRegime(int? regime) =>
        Regimes.FirstOrDefault(r => r.Codigo == regime).Rotulo ?? "Não informado";

    // ---- Validação do formulário ----
    public static string? ProblemaComOsDados(string? cnpj, string? razaoSocial, int? regime,
        string? cep, string? municipioIbge)
    {
        // CNPJ é o único campo que a tela recusa mesmo em branco... não: em branco ela aceita
        // (o clube pode estar preenchendo aos poucos). O que ela não aceita é CNPJ INVENTADO,
        // porque um número que não existe só é descoberto na primeira emissão.
        if (!string.IsNullOrWhiteSpace(cnpj) && !Documentos.CnpjEhValido(cnpj))
            return "Esse CNPJ não confere — confira os números.";

        if (razaoSocial != null && razaoSocial.Trim().Length > 120)
            return "A razão social é muito longa (máximo 120 caracteres).";

        if (regime != null && !RegimeValido(regime))
            return "Escolha um regime tributário da lista.";

        if (!string.IsNullOrWhiteSpace(cep) && Documentos.SomenteDigitos(cep).Length != 8)
            return "O CEP tem 8 dígitos.";

        if (!string.IsNullOrWhiteSpace(municipioIbge) && Documentos.SomenteDigitos(municipioIbge).Length != 7)
            return "O código do município (IBGE) tem 7 dígitos.";

        return null;
    }

    // O que falta pra este clube emitir o documento pedido. Lista vazia = pronto pra ligar.
    public static IReadOnlyList<string> FaltaParaEmitir(Clube clube, string documento)
    {
        var falta = new List<string>();

        if (!Documentos.CnpjEhValido(clube.Cnpj)) falta.Add("o CNPJ do clube");
        if (string.IsNullOrWhiteSpace(clube.RazaoSocial)) falta.Add("a razão social");
        if (!RegimeValido(clube.RegimeTributario)) falta.Add("o regime tributário");

        // Endereço: a nota não aceita "ao lado do posto". Os quatro campos abaixo são os que
        // o layout exige — o número entra na lista porque "S/N" é resposta válida e mesmo
        // assim precisa estar escrito.
        if (string.IsNullOrWhiteSpace(clube.Logradouro)) falta.Add("o logradouro");
        if (string.IsNullOrWhiteSpace(clube.NumeroEndereco)) falta.Add("o número do endereço");
        if (Documentos.SomenteDigitos(clube.Cep).Length != 8) falta.Add("o CEP");
        if (Documentos.SomenteDigitos(clube.MunicipioIbge).Length != 7) falta.Add("o município (vem do CEP)");

        // Daqui pra baixo é o que muda por documento — e é o motivo de este método receber
        // qual deles se quer. Cobrar inscrição estadual de quem só emite nota de aula seria
        // mandar o clube atrás de um documento que ele não precisa ter.
        if (documento == Nfce)
        {
            if (string.IsNullOrWhiteSpace(clube.InscricaoEstadual)) falta.Add("a inscrição estadual");
            if (clube.CertificadoConfiguradoEm == null) falta.Add("o certificado digital A1");
        }
        else if (documento == Nfse)
        {
            if (string.IsNullOrWhiteSpace(clube.InscricaoMunicipal)) falta.Add("a inscrição municipal");
            if (clube.CertificadoConfiguradoEm == null) falta.Add("o certificado digital A1");
        }

        return falta;
    }

    public static bool PodeEmitir(Clube clube, string documento) => FaltaParaEmitir(clube, documento).Count == 0;

    // A frase pronta pra tela. Uma lista de sete itens em bullet vira parede; escrita em
    // linha, com o "e" antes do último, é um recado que se lê em dois segundos.
    public static string ResumoDoQueFalta(IReadOnlyList<string> falta)
    {
        if (falta.Count == 0) return "Tudo pronto.";
        if (falta.Count == 1) return $"Falta {falta[0]}.";

        return $"Faltam {string.Join(", ", falta.Take(falta.Count - 1))} e {falta[^1]}.";
    }
}
