using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Quanto custa uma aula. Já foi uma linha (`local.PrecoPadrao`) até 03/08/2026, quando o
// primeiro professor de verdade usou o sistema e mostrou que o preço tem duas dimensões:
//
//   TAMANHO  — quantos alunos dividem a mesma hora de quadra.
//   QUEM     — o aluno antigo que nunca teve reajuste paga o dele.
//
// As duas se cruzam, e cruzar sem uma regra escrita é como se cobra errado. A regra:
// o preço combinado é um acordo com UMA PESSOA, então vale na aula individual dela; a aula
// em turma é da quadra e cobra o valor do tamanho. Em qualquer caso o professor ainda
// enxerga e edita o valor antes de salvar — isto aqui é a sugestão, não a sentença.
public static class PrecoDaAula
{
    public const int MinAlunos = 1;

    // Seis por causa do beach: a turma de sexteto é o formato normal lá, e o teto de três
    // (que vinha das colunas PrecoDupla/PrecoTrio do local) obrigava o professor a lançar a
    // aula como trio e corrigir o preço na mão — em toda aula, e esquecendo em uma.
    // Subir daqui pra frente é só mexer nesta linha: os preços moram em PrecoDeTurma, uma
    // linha por tamanho, e nada mais conta até três.
    public const int MaxAlunos = 6;

    // O formulário manda o que o usuário mandar (0, 9, vazio virando 0). Fora da faixa vira
    // aula individual em vez de erro: o professor quer marcar a aula, não discutir o campo.
    public static int Tamanho(int quantidadeAlunos) =>
        quantidadeAlunos < MinAlunos || quantidadeAlunos > MaxAlunos ? MinAlunos : quantidadeAlunos;

    public static string Rotulo(int quantidadeAlunos) => Tamanho(quantidadeAlunos) switch
    {
        2 => "Em dupla",
        3 => "Em trio",
        4 => "Em quarteto",
        5 => "Em quinteto",
        6 => "Em sexteto",
        _ => "Individual",
    };

    // O mesmo tamanho, dito do lado do ALUNO. Ele não marca "em trio", ele marca "nós
    // três" — e a primeira pessoa da frase é ele. Fica aqui junto do Rotulo do professor
    // porque são a mesma lista vista dos dois lados: separados, subir o teto acertaria um e
    // deixaria o outro parado no número antigo.
    public static string RotuloDoAluno(int quantidadeAlunos) => Tamanho(quantidadeAlunos) switch
    {
        2 => "Nós dois",
        3 => "Nós três",
        4 => "Nós quatro",
        5 => "Nós cinco",
        6 => "Nós seis",
        _ => "Só eu",
    };

    // Os tamanhos de turma que existem pra oferecer numa tela (2, 3, ... até o teto). A
    // individual fica de fora porque ela não é turma: o preço dela é o do local, sempre.
    public static IEnumerable<int> TamanhosDeTurma =>
        Enumerable.Range(2, MaxAlunos - 1);

    // A tabela de preços de um local, pronta pra consulta por tamanho. Tamanho repetido não
    // deveria existir (o índice único no banco impede), mas se existir o último vence em vez
    // de derrubar a página — mesma escolha de PorAluno, logo abaixo.
    public static Dictionary<int, decimal> PorTamanho(IEnumerable<PrecoDeTurma>? precos)
    {
        var mapa = new Dictionary<int, decimal>();
        foreach (var preco in precos ?? Enumerable.Empty<PrecoDeTurma>())
        {
            mapa[preco.QuantidadeAlunos] = preco.Preco;
        }
        return mapa;
    }

    // Preço da tabela do local para um tamanho. Sem preço para o tamanho pedido, cai para o
    // tamanho menor mais próximo que o professor informou — quem preencheu dupla e deixou
    // trio em branco prefere ver o valor da dupla (perto, dá pra ajustar pra cima) do que o
    // do individual, que cobraria três pessoas pelo preço de uma. Com o teto em seis a queda
    // é degrau a degrau pelo mesmo motivo: o sexteto de quem só anunciou quarteto vale mais
    // o valor do quarteto do que o da aula de uma pessoa só.
    //
    // ⚠️ Lê `local.PrecosDeTurma`, então quem carrega o local do banco precisa do Include —
    // sem ele a lista vem vazia e TODA turma sairia pelo preço da individual, calada.
    public static decimal DoLocal(LocalAula local, int quantidadeAlunos)
    {
        var tabela = PorTamanho(local.PrecosDeTurma);

        for (var tamanho = Tamanho(quantidadeAlunos); tamanho >= 2; tamanho--)
        {
            if (tabela.TryGetValue(tamanho, out var preco)) return preco;
        }

        return local.PrecoPadrao;
    }

    // O valor que a tela sugere. `precoCombinado` é o acordo com aquele aluno (null = não
    // tem) e só entra na individual, pelo motivo explicado lá em cima.
    public static decimal Sugerido(LocalAula local, int quantidadeAlunos, decimal? precoCombinado) =>
        Tamanho(quantidadeAlunos) == 1 && precoCombinado is decimal combinado
            ? combinado
            : DoLocal(local, quantidadeAlunos);

    // Racha o valor da turma em N fatias que SOMAM exatamente o total — sem sobrar nem
    // faltar centavo por arredondamento (R$100 em 3 não é 33,33 × 3; sobra 1 centavo perdido
    // se cada linha nascer com Math.Round direto). O centavo que não divide igual vai pros
    // primeiros da lista — a ordem não representa nada, é só uma forma determinística de
    // fechar a conta sem deixar sobra em lugar nenhum.
    public static List<decimal> DivididoIgualmente(decimal total, int quantidadeAlunos)
    {
        var alunos = Math.Max(1, quantidadeAlunos);
        var totalCentavos = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero);
        var baseCentavos = totalCentavos / alunos;
        var resto = totalCentavos % alunos;

        var fatias = new List<decimal>(alunos);
        for (var i = 0; i < alunos; i++)
        {
            fatias.Add((baseCentavos + (i < resto ? 1 : 0)) / 100m);
        }
        return fatias;
    }

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
