using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O QUE ACONTECEU quando perguntamos ao Ranking sobre uma pessoa numa categoria.
//
// Por que esta tabela existe, se `BloqueioDoRanking` já guarda as recusas: porque ela guarda
// SÓ as recusas. Quem foi consultado e passou não deixava rastro nenhum — e "quantas pessoas
// passaram pelo filtro" é justamente metade do que a parceria entrega.
//
// ⚠️ E não dá pra responder isso por SUBTRAÇÃO ("inscritos menos barrados"). Três situações
// diferentes produzem exatamente o mesmo silêncio de hoje, e só uma delas é "passou":
//   • a categoria não tem correspondência no ranking, então ninguém foi perguntado;
//   • o organizador ligou a validação DEPOIS de as inscrições já terem entrado;
//   • o servidor deles não respondeu — e por regra da casa isso aprova todo mundo, calado
//     (ver Services/ValidacaoPeloRankingRs, as três coisas que aquele arquivo nunca faz).
// Contar essas pessoas como "aprovadas pelo ranking" seria transformar o silêncio em selo de
// qualidade, numa página que o parceiro lê. Aqui cada uma tem nome próprio.
//
// ⚠️ A identidade é o CPF, e não o JogadorId, pelo mesmo motivo do BloqueioDoRanking: na hora
// da consulta a pessoa pode ainda não ter linha em `Jogador` — a inscrição só cria o jogador
// depois de passar por todas as regras.
[Table("ConsultaAoRankingRs")]
public class ConsultaAoRankingRs
{
    public int Id { get; set; }

    public int TorneioId { get; set; }
    public virtual Torneio Torneio { get; set; } = null!;

    public int CategoriaId { get; set; }
    public virtual Categoria Categoria { get; set; } = null!;

    public string Cpf { get; set; } = null!;

    // O nome exatamente como foi mandado. O casamento deles é POR NOME (provado contra a API
    // em 07/08/2026), então ver o texto consultado é o que explica um resultado estranho.
    public string NomeConsultado { get; set; } = null!;

    // Contra qual categoria do ranking a pergunta foi feita. Nulo quando não houve pergunta.
    public int? CategoriaRsId { get; set; }
    public string? CategoriaRsNome { get; set; }

    public string Resultado { get; set; } = ResultadoDaConsulta.SemResposta;

    // Se o nome foi encontrado no ranking. Vem junto de `Aprovado` e é informação DIFERENTE:
    // quem não pontuou em lugar nenhum passa por não ter o que provar, e quem pontua só em
    // categorias mais fracas passa tendo sido conferido de verdade.
    public bool EncontradoNoRanking { get; set; }

    // Quando a última tentativa aconteceu. Tentar de novo ATUALIZA a linha, não empilha outra
    // — mesma disciplina do BloqueioDoRanking, e pelo mesmo motivo: a pessoa que insiste no
    // formulário não pode virar cinco linhas no relatório do parceiro.
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}

public static class ResultadoDaConsulta
{
    // Perguntamos e o ranking liberou. É o único valor que significa "passou pelo filtro".
    public const string Aprovado = "Aprovado";

    // Perguntamos e o ranking barrou. Esta pessoa também tem linha em `BloqueioDoRanking`,
    // que é onde mora a decisão do organizador sobre a recusa.
    public const string Barrado = "Barrado";

    // Não perguntamos: a categoria daqui não tem correspondência no ranking. Não é falha, é
    // decisão — ver Services/CategoriaDoRankingRs: adivinhar o sexo errado faria a API deles
    // responder "aprovado" numa boa, e errado. Mas o resultado prático é que a pessoa se
    // inscreveu sem conferência nenhuma, e o relatório precisa dizer isso.
    public const string SemDePara = "SemDePara";

    // Perguntamos e não veio resposta útil: servidor fora do ar, tempo esgotado, ou a
    // integração sem chave configurada. A inscrição passou assim mesmo, de propósito — o
    // contrário transformaria uma queda na casa deles em "ninguém se inscreve hoje".
    public const string SemResposta = "SemResposta";

    public static bool EhValido(string? resultado) =>
        resultado is Aprovado or Barrado or SemDePara or SemResposta;
}
