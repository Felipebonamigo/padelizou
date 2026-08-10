namespace Padelizou.Services;

// O que o Ranking RS respondeu sobre um atleta numa categoria.
public enum RespostaDoRanking
{
    // Não deu pra perguntar: integração desligada, categoria que não casa com o ranking, ou
    // o servidor deles fora do ar. NUNCA vira recusa — ver o comentário do RankingRsService.
    NaoConsultado,
    Aprovado,
    ForaDeCategoria,
}

// Uma categoria mais forte em que o atleta já pontuou, e que por isso barra a inscrição.
public record CategoriaBloqueante(string Categoria, int Pontos, int Posicao);

public record ResultadoDoRanking(
    RespostaDoRanking Resposta,
    string Nome,
    string? CategoriaConsultada,
    // false = o nome não existe no ranking. Vem junto de Aprovado, porque quem não pontuou
    // não tem o que provar — mas é informação diferente de "consultei e ele está liberado".
    bool EncontradoNoRanking,
    IReadOnlyList<CategoriaBloqueante> Bloqueantes,
    // Eco do identificador que mandamos, pra casar a resposta do lote com a linha certa.
    string? Referencia = null)
{
    public bool Reprovado => Resposta == RespostaDoRanking.ForaDeCategoria;

    // A frase que o jogador lê. Cita as categorias e os pontos porque "você não pode" sem
    // motivo é o tipo de recusa que vira mensagem no WhatsApp do organizador.
    public string Motivo(string categoriaNoPadelizou)
    {
        var onde = Bloqueantes.Count == 0
            ? "numa categoria mais forte"
            : string.Join(" e ", Bloqueantes.Select(b => $"{b.Categoria} ({b.Pontos} pts)"));

        return $"{Nome} não pode jogar {categoriaNoPadelizou}: o {MarcaDoRanking.Nome} aponta pontos em {onde}.";
    }
}

public record AtletaParaValidar(string Nome, int CategoriaRsId, string? Referencia = null);

// ⚠️ EXISTIA AQUI UM `PosicoesAsync` — "em que categorias esta pessoa pontua e em que posição"
// —, que alimentava um selo no perfil de cada jogador. Saiu em 08/08/2026 A PEDIDO DELES: o
// dado é deles, e fora de um torneio que contratou a conferência ele não presta serviço nenhum,
// só publica o ranking do parceiro de graça em cada perfil do nosso site.
//
// A interface ficou com UMA pergunta só, e é a que a parceria existe pra responder: **esta
// pessoa pode jogar esta categoria?**. Se um dia a vitrine voltar, ela está no commit desta
// mudança — inteira, com os testes contra servidor de mentira.
public interface IRankingRsService
{
    bool Configurado { get; }

    Task<ResultadoDoRanking> ValidarAsync(string nome, int categoriaRsId, CancellationToken ct = default);

    // Um POST só pra várias pessoas. É o que serve pro organizador conferir um torneio inteiro
    // sem disparar uma requisição por inscrito.
    Task<IReadOnlyList<ResultadoDoRanking>> ValidarLoteAsync(
        IReadOnlyList<AtletaParaValidar> atletas, CancellationToken ct = default);
}
