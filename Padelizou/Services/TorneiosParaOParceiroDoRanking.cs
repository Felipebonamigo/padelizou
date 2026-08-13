using Padelizou.Models;

namespace Padelizou.Services;

// A lista de torneios que o Ranking Brasil (mundodoatleta.com.br) puxa da gente.
//
// Até aqui a parceria era de mão única: nós perguntamos "esta pessoa pode disputar esta
// categoria?" (Services/RankingRsService) e eles não tinham como saber onde as perguntas iam
// nascer. O pedido veio deles, em 12/08/2026, com o escopo escrito por eles mesmos: *"uma API
// que libera buscarmos os torneios pontuados no ranking — apenas informações dos torneios, como
// nome, clube, data, foto. Não precisa nenhum dado dos atletas."*
//
// ⚠️ NENHUM DADO DE ATLETA SAI DAQUI, e isso é REGRA, não economia de campo. Não há nome, CPF,
// contato, nem sequer a CONTAGEM de inscritos — o número de gente num torneio é informação
// comercial nossa e do organizador, e "só um totalzinho" é exatamente o jeito de um escopo
// combinado virar outro sem ninguém decidir nada. Há teste guardando isso: o corpo inteiro é
// serializado e conferido contra o nome de um jogador inscrito.
//
// ⚠️ A régua de QUEM APARECE é a mesma da vitrine do site (PermissaoDeOrganizador), e não uma
// cópia. Copiar a condição faria o torneio oculto continuar saindo pra fora no dia em que ela
// mudasse — e aqui o estrago é maior que na listagem: quem lê é um site de terceiro, que
// publica o que a gente mandar e não tem como saber que aquilo não devia ter saído.
public static class TorneiosParaOParceiroDoRanking
{
    // ── QUEM ENTRA ────────────────────────────────────────────────────────────────────────
    //
    // Três condições, e cada uma responde uma pergunta diferente:
    //
    //  1. ADERIU À PARCERIA — `ValidarPeloRankingRs` é a caixa que o organizador marca na
    //     criação ("Conferir as inscrições no Ranking Brasil"). É a MESMA coluna que decide se
    //     há o que acertar com eles (Services/AcertoComORankingRs.ProblemaParaAcertar), então
    //     "torneio pontuado no ranking" e "torneio que a gente cobra por" são o mesmo conjunto
    //     — que é como uma parceria deve se comportar.
    //
    //  2. A INSCRIÇÃO ESTÁ ABERTA — foi o pedido: "os torneios atuais abertos". Torneio que
    //     ainda vai abrir, que já sorteou ou que acabou não serve pra quem vai divulgar
    //     inscrição. Quem responde é PortaDaInscricao, e não um `Status ==` escrito aqui.
    //
    //  3. O PÚBLICO JÁ PODE VER — aprovado, não oculto e não cancelado. Sem isto, um torneio
    //     esperando aprovação (ou escondido pelo organizador) estrearia no site DELES antes de
    //     existir no nosso.
    public static bool EntraNaLista(Torneio torneio) =>
        torneio.ValidarPeloRankingRs
        && PortaDaInscricao.EstaAberta(torneio)
        && PermissaoDeOrganizador.ApareceParaOPublico(torneio);

    // ── O QUE SAI ─────────────────────────────────────────────────────────────────────────

    // O clube é o "onde" que eles pediram. Endereço e cidade entram porque quem lê a lista está
    // montando uma agenda pra atleta decidir se dá pra ir — e um nome de clube sozinho não
    // responde isso pra quem não é da cidade.
    public record ClubePublicado(string Nome, string? Cidade, string? Estado, string? Endereco);

    // ⚠️ A categoria vai com o DE-PARA, e é o campo mais útil da resposta pra eles: `RankingId`
    // é o id do catálogo DELES (Services/CategoriaDoRankingRs), então eles casam a categoria
    // sem ter que adivinhar pelo nome que o organizador digitou. Nulo = esta categoria não é
    // conferida contra o ranking (7ª, Iniciantes, Mista D e o que não deu pra casar).
    public record CategoriaPublicada(string Nome, int? RankingId, string? RankingNome);

    public record TorneioPublicado(
        int Id,
        string Nome,
        string Link,
        string? Foto,
        ClubePublicado? Clube,
        string? Local,
        string? DataInicio,
        string? DataFim,
        string Formato,
        decimal PrecoInscricao,
        string? InscricoesAte,
        bool InscricaoRestrita,
        IReadOnlyList<CategoriaPublicada> Categorias);

    // `GeradoEm` existe pra eles saberem que a resposta é de agora — sem isso, um cache no
    // meio do caminho entrega uma lista de ontem e não há como perceber.
    public record Resposta(string GeradoEm, int Quantidade, IReadOnlyList<TorneioPublicado> Torneios);

    // Monta a resposta inteira.
    //
    // ⚠️ O FILTRO ACONTECE AQUI DENTRO, e não é responsabilidade de quem chama. É a mesma lição
    // do `PontosDoTorneio.Pontos` (ver RANKING.md): régua que vive num `.Where()` do chamador
    // depende de todo mundo lembrar dela, e o segundo chamador — escrito daqui a três meses —
    // não lembra. Aqui o esquecimento publicaria torneio escondido no site de terceiro.
    //
    // Clube e cidade chegam por DICIONÁRIO, não por Include: `Torneio.ClubeId` é obrigatório no
    // modelo mas nem todo torneio tem clube de verdade, e navegação obrigatória carregada por
    // Include vira INNER JOIN — o torneio sem clube sumiria da lista, calado. Mesma armadilha
    // documentada em Services/TorneiosPorCidade.
    public static Resposta Montar(
        IEnumerable<Torneio> torneios,
        IReadOnlyDictionary<int, Clube> clubes,
        IReadOnlyDictionary<int, Cidade> cidades,
        ILookup<int, Categoria> categoriasPorTorneio,
        string enderecoBase,
        DateTime agora)
    {
        var lista = torneios
            .Where(EntraNaLista)
            // Do mais próximo pro mais distante, igual à vitrine: quem publica a lista publica
            // nessa ordem, e torneio sem data marcada vai pro fim em vez de encabeçar tudo.
            .OrderBy(t => t.DataInicio == null)
            .ThenBy(t => t.DataInicio)
            .Select(t => new TorneioPublicado(
                Id: t.Id,
                Nome: t.Nome,
                Link: Absoluto(enderecoBase, $"/Torneios/Details/{t.Id}")!,
                Foto: Absoluto(enderecoBase, t.ImagemCapa),
                Clube: ClubeDe(t, clubes, cidades),
                Local: Vazio(t.LocalTorneio),
                DataInicio: Data(t.DataInicio),
                DataFim: Data(t.DataFim),
                Formato: t.Formato,
                // Por PESSOA, que é como o torneio anuncia — ver Torneio.PrecoInscricao. Uma
                // dupla paga o dobro disto, e publicar o valor da dupla faria o torneio parecer
                // duas vezes mais caro do que ele é.
                PrecoInscricao: t.PrecoInscricao,
                InscricoesAte: Data(t.PrevisaoEncerramentoInscricoes),
                // Vai marcado em vez de ficar de fora: o torneio restrito APARECE na nossa
                // vitrine (o que Restrito tranca é a inscrição, não a página), então escondê-lo
                // aqui criaria duas listas diferentes de "torneios abertos". Mas publicá-lo
                // calado faria eles anunciarem como aberto a todos um evento que pede chave.
                InscricaoRestrita: t.Restrito,
                Categorias: categoriasPorTorneio[t.Id]
                    .OrderBy(c => c.Nome, StringComparer.CurrentCulture)
                    .Select(c => new CategoriaPublicada(
                        c.Nome,
                        c.RankingRsCategoriaId,
                        c.RankingRsCategoriaId is int id ? CategoriaDoRankingRs.NomeDoId(id) : null))
                    .ToList()))
            .ToList();

        return new Resposta(agora.ToString("yyyy-MM-dd'T'HH:mm:sszzz"), lista.Count, lista);
    }

    private static ClubePublicado? ClubeDe(Torneio torneio,
        IReadOnlyDictionary<int, Clube> clubes, IReadOnlyDictionary<int, Cidade> cidades)
    {
        if (!clubes.TryGetValue(torneio.ClubeId, out var clube)) return null;

        Cidade? cidade = clube.CidadeId is int id && cidades.TryGetValue(id, out var achada)
            ? achada : null;

        return new ClubePublicado(
            clube.Nome,
            // A cidade sai escrita como gente: o catálogo de produção tem "porto alegre" em
            // minúsculo, e o que a gente manda pra fora vira título na tela deles.
            cidade == null ? null : NomeDeCidade.ComoTitulo(cidade.Nome),
            cidade?.Estado,
            Vazio(clube.Endereco));
    }

    // Data sem hora e sem fuso: `2026-08-22`. O torneio guarda só a data (a hora de abertura da
    // grade mora em campo separado), e mandar um instante com fuso convidaria o outro lado a
    // converter — e a mostrar o torneio um dia antes pra quem abrisse o site de outro país.
    public static string? Data(DateTime? data) => data?.ToString("yyyy-MM-dd");

    // Endereço completo, porque quem lê está em outro domínio: "/uploads/capas/x.webp" não abre
    // nada no site deles. Capa antiga pode ter sido gravada como URL inteira — aí ela já é o
    // endereço, e concatenar produziria um link quebrado.
    public static string? Absoluto(string enderecoBase, string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return null;

        return caminho.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? caminho
            : $"{enderecoBase.TrimEnd('/')}/{caminho.TrimStart('/')}";
    }

    // Campo em branco no banco vira ausente no JSON. `""` obrigaria o outro lado a testar as
    // duas coisas — e é o tipo de detalhe que só aparece como um endereço vazio na tela deles.
    private static string? Vazio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;
}
