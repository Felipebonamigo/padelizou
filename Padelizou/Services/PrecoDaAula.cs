using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Quanto custa uma aula. Já foi uma linha (`local.PrecoPadrao`) até 03/08/2026, quando o
// primeiro professor de verdade usou o sistema e mostrou que o preço tem duas dimensões:
//
//   TAMANHO  — um aluno, dois ou três dividindo a mesma hora de quadra.
//   QUEM     — o aluno antigo que nunca teve reajuste paga o dele.
//
// As duas se cruzam, e cruzar sem uma regra escrita é como se cobra errado. A regra:
// o preço combinado é um acordo com UMA PESSOA, então vale na aula individual dela; a aula
// em dupla ou trio é da quadra e cobra o valor do tamanho. Em qualquer caso o professor
// ainda enxerga e edita o valor antes de salvar — isto aqui é a sugestão, não a sentença.
public static class PrecoDaAula
{
    public const int MinAlunos = 1;
    public const int MaxAlunos = 3;

    // O formulário manda o que o usuário mandar (0, 9, vazio virando 0). Fora da faixa vira
    // aula individual em vez de erro: o professor quer marcar a aula, não discutir o campo.
    public static int Tamanho(int quantidadeAlunos) =>
        quantidadeAlunos < MinAlunos || quantidadeAlunos > MaxAlunos ? MinAlunos : quantidadeAlunos;

    public static string Rotulo(int quantidadeAlunos) => Tamanho(quantidadeAlunos) switch
    {
        2 => "Em dupla",
        3 => "Em trio",
        _ => "Individual",
    };

    // Preço da tabela do local para um tamanho. Sem preço para o tamanho pedido, cai para o
    // tamanho menor mais próximo que o professor informou — quem preencheu dupla e deixou
    // trio em branco prefere ver o valor da dupla (perto, dá pra ajustar pra cima) do que o
    // do individual, que cobraria três pessoas pelo preço de uma.
    public static decimal DoLocal(LocalAula local, int quantidadeAlunos) => Tamanho(quantidadeAlunos) switch
    {
        3 => local.PrecoTrio ?? local.PrecoDupla ?? local.PrecoPadrao,
        2 => local.PrecoDupla ?? local.PrecoPadrao,
        _ => local.PrecoPadrao,
    };

    // O valor que a tela sugere. `precoCombinado` é o acordo com aquele aluno (null = não
    // tem) e só entra na individual, pelo motivo explicado lá em cima.
    public static decimal Sugerido(LocalAula local, int quantidadeAlunos, decimal? precoCombinado) =>
        Tamanho(quantidadeAlunos) == 1 && precoCombinado is decimal combinado
            ? combinado
            : DoLocal(local, quantidadeAlunos);

    // ---- Identificar o aluno ----
    // A agenda do professor tem dois tipos de aluno: o que tem conta (AlunoId) e o que ele
    // só anotou o nome (aula lançada à mão). Preço combinado precisa achar os dois, então
    // ambos viram uma chave de texto — é ela que casa a linha de PrecoDeAluno com a aula.
    //
    // O nome perde caixa e espaço das pontas porque "joão" e "João " são a mesma pessoa pra
    // quem digitou. Acento fica: dois alunos "Ines" e "Inês" são chute, e chutar aqui
    // aplicaria o desconto de um no outro.
    public static string Chave(int? alunoId, string? nomeAvulso) =>
        alunoId is int id ? $"aluno-{id}" : $"avulso-{(nomeAvulso ?? "").Trim().ToLowerInvariant()}";

    public static string Chave(Aula aula) => Chave(aula.AlunoId, aula.NomeAlunoAvulso);

    public static string Chave(PrecoDeAluno preco) => Chave(preco.AlunoId, preco.NomeAvulso);

    // Os preços combinados de um professor prontos pra consulta por chave. A tela de marcar
    // aula precisa de todos de uma vez (o JS troca o valor quando muda o aluno), e o
    // servidor precisa de um só — os dois saem daqui.
    public static Dictionary<string, decimal> PorAluno(IEnumerable<PrecoDeAluno> precos)
    {
        var mapa = new Dictionary<string, decimal>();
        foreach (var preco in precos)
        {
            // Duplicado não deveria existir (a tela grava um por aluno), mas se existir o
            // último a entrar vence em vez de derrubar a página com exceção de chave repetida.
            mapa[Chave(preco)] = preco.Preco;
        }
        return mapa;
    }
}
