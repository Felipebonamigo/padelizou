using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// QUEM GANHOU, e o card que anuncia isso.
//
// Todo torneio termina e o resultado morre no grupo do WhatsApp. A dupla campeã tira uma foto
// com o troféu e posta; o nome do torneio não aparece, a categoria não aparece, e quem vê não
// tem como saber que aquilo saiu daqui. Cada final é uma peça de divulgação que a gente joga
// fora — e são dezenas por torneio, o ano inteiro.
//
// O card fecha esse buraco: a arte nasce sozinha assim que a final é encerrada, com o nome dos
// dois, a categoria, o torneio e o endereço do site. O campeão posta porque é sobre ELE; a
// marca viaja junto de carona.
//
// ⚠️ O CARIMBO DE CAMPEÃO É UM SÓ NO SISTEMA INTEIRO: `Dupla.UltimaFase == "Campeao"`. Vale
// para os três formatos, e é de propósito — o `RoboDoChaveamento` carimba a dupla vencedora da
// final, e no Americano (onde o campeão é UMA PESSOA, porque o parceiro troca a cada rodada)
// ele cria uma linha SEM parceiro com o mesmo carimbo. Ler qualquer outra coisa aqui seria a
// segunda cópia da regra que decide campeão — e este projeto já tem a cicatriz de ter feito
// isso com "quem venceu".
public static class CampeoesDoTorneio
{
    public const string FaseDeCampeao = "Campeao";

    // Este torneio pode anunciar campeão?
    //
    // ⚠️ Cancelado não pode, mesmo tendo tido final jogada antes de cancelar: é a mesma régua
    // do `PontosDoTorneio.TorneioJaComecou` — o evento não aconteceu, e o sistema inteiro
    // precisa contar a mesma história. Anunciar campeão de torneio cancelado seria a única tela
    // do Padelizou dizendo que ele valeu.
    public static bool PodeAnunciar(string? statusDoTorneio) =>
        !CancelamentoDoTorneio.EstaCancelado(statusDoTorneio);

    // Já tem alguém coroado neste torneio? Pergunta da PÁGINA do torneio, que decide se mostra
    // o botão — e que já carregou categorias e duplas de qualquer jeito, então não vale uma
    // segunda ida ao banco.
    //
    // ⚠️ Existe pra a view não escrever `d.UltimaFase == "Campeao"` na mão. Seria a segunda
    // cópia da regra que decide campeão, no lugar mais fácil de esquecer no dia em que ela
    // mudar: uma tela que some sozinha e ninguém relaciona com a mudança.
    public static bool TemCampeao(Torneio torneio) =>
        PodeAnunciar(torneio.Status)
        && torneio.Categorias.Any(c => c.Duplas.Any(d => d.UltimaFase == FaseDeCampeao));

    // O rótulo do card, que muda com o tamanho da dupla e com o gênero da categoria.
    //
    // ⚠️ Sai do NOME DA CATEGORIA e não do `Jogador.Sexo`, por dois motivos: a régua já existe
    // (`FaixasDePadelimetro.EhFeminina`, a mesma que o Padelímetro usa) e o sexo é dado pessoal
    // que só a inscrição em Mista/Casais precisa ler. Card não é lugar de estrear leitura de
    // dado pessoal — e "3ª Categoria Feminina" já diz tudo o que a frase precisa saber.
    public static string Rotulo(string? nomeCategoria, bool duasPessoas)
    {
        var feminina = FaixasDePadelimetro.EhFeminina(nomeCategoria);

        if (duasPessoas) return feminina ? "CAMPEÃS" : "CAMPEÕES";
        return feminina ? "CAMPEÃ" : "CAMPEÃO";
    }

    // Os campeões de cada categoria deste torneio, na ordem em que as categorias aparecem nas
    // outras telas.
    public static async Task<List<CampeaoDeCategoria>> DoTorneioAsync(DbPadelContext contexto, int torneioId)
    {
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            // ⚠️ SÓ COLUNAS DO PRÓPRIO TORNEIO. Aqui havia `Clube = t.Clube.Nome`, e ele fazia o
            // TORNEIO INTEIRO sumir quando não havia clube: `Torneio.ClubeId` é obrigatório, então
            // o EF traduz a navegação como INNER JOIN — sem clube casando, não volta linha
            // nenhuma, e o método devolvia "este torneio não tem campeão" em vez de estourar.
            // Um teste pegou; em produção passaria batido, porque lá a FK sempre tem clube.
            .Select(t => new { t.Id, t.Nome, t.Status, t.ClubeId, t.DataInicio })
            .FirstOrDefaultAsync();

        if (torneio == null || !PodeAnunciar(torneio.Status)) return new List<CampeaoDeCategoria>();

        // O nome do clube numa consulta à parte, justamente pra ele NÃO poder derrubar o card:
        // clube ausente vira uma linha a menos na legenda, nunca um torneio sem campeão.
        var nomeDoClube = await contexto.Clubes
            .AsNoTracking()
            .Where(c => c.Id == torneio.ClubeId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync();

        var duplas = await contexto.Duplas
            .AsNoTracking()
            .Include(d => d.Categoria)
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .Include(d => d.Time)
            .Where(d => d.Categoria.TorneioId == torneioId && d.UltimaFase == FaseDeCampeao)
            .ToListAsync();

        return duplas
            // ⚠️ Agrupa por categoria e fica com UM. Não deveria haver dois carimbos na mesma
            // categoria — o Americano se protege disso antes de coroar, e desfazer uma final
            // remove o carimbo (`PartidasController`). Mas "não deveria" não é "não pode": um
            // acerto na mão no banco basta, e duas duplas campeãs na mesma categoria fariam a
            // página do torneio mostrar dois cards contraditórios sem ninguém entender por quê.
            // O mais recente (maior Id) ganha, que é o que o Americano cria ao coroar de novo.
            .GroupBy(d => d.CategoriaId)
            .Select(g => g.OrderByDescending(d => d.Id).First())
            .Select(d => new CampeaoDeCategoria(
                CategoriaId: d.CategoriaId,
                Categoria: d.Categoria?.Nome ?? "",
                DuplaId: d.Id,
                NomeTime: d.NomeTime,
                LogoDoTime: d.Time?.Logo,
                Jogador1: d.NomeTime == null ? d.Jogador1 : null,
                Jogador2: d.NomeTime == null ? d.Jogador2 : null,
                Torneio: torneio.Nome,
                Clube: nomeDoClube,
                Data: torneio.DataInicio))
            // A MESMA ordem das outras telas do torneio (masculinas da mais forte pra mais
            // fraca, depois femininas, depois mista/casais). A tupla já desempata pelo nome no
            // último item, então não há `ThenBy` aqui.
            .OrderBy(c => CategoriaNaTela.Ordem(c.Categoria))
            .ToList();
    }

    // O campeão de UMA categoria — o que o desenho do card precisa. Null quando não há.
    public static async Task<CampeaoDeCategoria?> DaCategoriaAsync(
        DbPadelContext contexto, int torneioId, int categoriaId)
    {
        var todos = await DoTorneioAsync(contexto, torneioId);
        return todos.FirstOrDefault(c => c.CategoriaId == categoriaId);
    }
}

// Quem levantou a taça numa categoria.
//
// ⚠️ `Jogador1`/`Jogador2` vêm NULOS quando a linha é de TIME: naquele caso o `Jogador1Id`
// aponta pro organizador que cadastrou o time, e estampar o nome dele como campeão seria
// coroar quem não jogou. É a mesma armadilha que já tirou a dupla-TIME de todo somatório de
// ranking do sistema.
public sealed record CampeaoDeCategoria(
    int CategoriaId,
    string Categoria,
    int DuplaId,
    string? NomeTime,
    string? LogoDoTime,
    Jogador? Jogador1,
    Jogador? Jogador2,
    string Torneio,
    string? Clube,
    DateTime? Data)
{
    public bool EhTime => NomeTime != null;

    // Quantas PESSOAS a taça coroa. Zero num time (o time é a entidade), 1 no Americano.
    public int Pessoas => EhTime ? 0 : (Jogador1 != null ? 1 : 0) + (Jogador2 != null ? 1 : 0);

    // "CAMPEÕES" / "CAMPEÃS" / "CAMPEÃO" / "CAMPEÃ".
    public string Rotulo => CampeoesDoTorneio.Rotulo(Categoria, Pessoas != 1);

    // O nome que vai grande no meio do card.
    public string Nomes =>
        NomeTime
        ?? (Jogador1 == null ? ""
            : Jogador2 == null ? Jogador1.ComoChamar
            : $"{Jogador1.ComoChamar}  &  {Jogador2.ComoChamar}");

    // Dá pra desenhar um card com isto? Uma linha de campeão sem nome nenhum (dupla apagada,
    // importação antiga) não vira arte — vira um card em branco com a nossa marca embaixo.
    public bool TemOQueMostrar => !string.IsNullOrWhiteSpace(Nomes);
}
