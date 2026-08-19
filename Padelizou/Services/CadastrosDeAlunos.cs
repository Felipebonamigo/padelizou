using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// As regras da ficha do aluno (ver Models/CadastroDoAluno): como achar a ficha certa, o que o
// "cadastro rápido" grava, e quem é o pagador quando não é o próprio aluno.
//
// Fica aqui, e não no controller, porque as três respostas são consumidas em quatro telas —
// marcar aula, painel do professor, fechamento do mês e a cobrança — e as quatro TÊM que
// concordar sobre quem é a pessoa e quem paga por ela.
public static class CadastrosDeAlunos
{
    // A ficha daquela pessoa, pela mesma chave que casa aulas e acordos de preço.
    public static CadastroDoAluno? Achar(IEnumerable<CadastroDoAluno> fichas, int? alunoId, string? nomeAvulso)
    {
        var chave = PrecoDaAula.Chave(alunoId, nomeAvulso);
        return fichas.FirstOrDefault(f => PrecoDaAula.Chave(f.AlunoId, f.NomeAvulso) == chave);
    }

    public static Dictionary<string, CadastroDoAluno> PorChave(IEnumerable<CadastroDoAluno> fichas)
    {
        var mapa = new Dictionary<string, CadastroDoAluno>();
        foreach (var ficha in fichas)
        {
            // Duplicada não deveria existir (o upsert grava uma por chave), mas se existir a
            // última vence em vez de derrubar a página — mesma escolha de PrecoDaAula.PorAluno.
            mapa[PrecoDaAula.Chave(ficha.AlunoId, ficha.NomeAvulso)] = ficha;
        }
        return mapa;
    }

    // Só dígitos, e vazio vira nulo. O professor digita "(51) 99999-0000", "51999990000" e
    // "51 9 9999 0000" pra mesma pessoa em três dias diferentes — guardar do jeito que veio
    // faria o mesmo aluno virar três, que é exatamente o problema que a ficha veio resolver.
    public static string? CelularNormalizado(string? celular)
    {
        var digitos = Documentos.SomenteDigitos(celular);
        return digitos.Length == 0 ? null : digitos;
    }

    // O celular serve pra achar a pessoa: com 8 dígitos ou menos não serve pra nada — nem
    // discar nem casar com conta nenhuma — e guardá-lo daria a impressão de cadastro feito.
    public const int MinimoDeDigitosDoCelular = 10;

    public static bool CelularServeParaAchar(string? celular) =>
        (CelularNormalizado(celular)?.Length ?? 0) >= MinimoDeDigitosDoCelular;

    // ---- Quem paga ----

    public static bool TemResponsavel(CadastroDoAluno? ficha) =>
        !string.IsNullOrWhiteSpace(ficha?.ResponsavelNome);

    // O nome que vai na cobrança do mês. Sem responsável é o próprio aluno — que é o caso
    // normal; o responsável existe pra criança e pra quem tem a conta paga por outro.
    public static string QuemPaga(CadastroDoAluno? ficha, string nomeDoAluno) =>
        TemResponsavel(ficha) ? ficha!.ResponsavelNome!.Trim() : nomeDoAluno;

    // O celular pra onde a cobrança é mandada: o do responsável quando existe, senão o do
    // aluno. Mandar pro aluno uma cobrança que o pai vai pagar é como o WhatsApp do professor
    // vira o meio de campo de novo.
    public static string? CelularDeCobranca(CadastroDoAluno? ficha, string? celularDoAluno) =>
        TemResponsavel(ficha) && CelularServeParaAchar(ficha!.ResponsavelCelular)
            ? CelularNormalizado(ficha.ResponsavelCelular)
            : CelularNormalizado(ficha?.Celular ?? celularDoAluno);

    // O CPF que o gateway vai exigir do pagador. Nulo = ainda não dá pra emitir a cobrança
    // pelo app, e a tela precisa dizer isso ANTES do professor tentar fechar o mês.
    public static string? CpfDoPagador(CadastroDoAluno? ficha, Jogador? contaDoAluno)
    {
        if (TemResponsavel(ficha))
        {
            var doResponsavel = Documentos.SomenteDigitosOuNulo(ficha!.ResponsavelCpf);
            return Documentos.CpfEhValido(doResponsavel) ? doResponsavel : null;
        }

        // Sem responsável, quem paga é o aluno — e só quem tem conta tem CPF no sistema.
        return Documentos.CpfEhValido(contaDoAluno?.Cpf) ? contaDoAluno!.Cpf : null;
    }

    // A mensagem de recusa do formulário, ou null quando está tudo certo. O CPF é opcional
    // (o professor pode cadastrar o responsável hoje e o documento depois), mas CPF ERRADO
    // não passa: ele só seria descoberto no dia da cobrança, com o mês já fechado.
    public static string? ProblemaComOResponsavel(string? nome, string? cpf)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return string.IsNullOrWhiteSpace(cpf)
                ? null
                : "Informe o nome do responsável junto com o CPF.";
        }

        var digitos = Documentos.SomenteDigitosOuNulo(cpf);
        if (digitos != null && !Documentos.CpfEhValido(digitos))
        {
            return "O CPF do responsável não confere — revise os números.";
        }

        return null;
    }
}
