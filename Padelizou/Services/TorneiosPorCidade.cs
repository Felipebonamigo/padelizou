using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// "Torneios de padel em Porto Alegre" — uma página por cidade.
//
// O motivo é o que as pessoas realmente digitam: ninguém procura "padelizou" sem já conhecer
// o site. Quem ainda não conhece procura pelo que quer — o esporte mais o lugar. A listagem
// geral em /Torneios não responde a isso: ela não fala de cidade nenhuma, então não é dela que
// o buscador se lembra quando alguém pergunta por uma.
//
// Só nascem páginas de cidade que TÊM torneio. Página vazia prometendo torneios é pior que
// página nenhuma: o visitante volta e o buscador aprende que o site responde com nada.
public static class TorneiosPorCidade
{
    // Um lugar reúne TODAS as grafias da mesma cidade ("Gravataí" e "GRAVATAI" são uma linha
    // só), por isso guarda uma lista de ids — ver CidadesSemRepetir.
    public record Lugar(string Nome, string? Uf, string Slug, IReadOnlyList<int> CidadeIds, int Torneios)
    {
        // "Porto Alegre (RS)" — a UF desempata as homônimas na hora de ler a lista.
        public string NomeNaTela => Uf == null ? Nome : $"{Nome} ({Uf})";
    }

    // A UF entra sempre que existe, e isso resolve as homônimas de verdade: "São Francisco" é
    // cidade em meia dúzia de estados, e dois lugares diferentes disputando o mesmo endereço
    // fariam um deles simplesmente não abrir.
    //
    // A base é NomeDeCidade.Chave — a mesma normalização que decide se duas grafias são a
    // mesma cidade. Uma segunda régua de "como escrever a cidade numa URL" acabaria
    // discordando dela, e o link gerado numa tela não abriria na outra.
    public static string SlugDe(string nome, string? uf)
    {
        var baseSlug = NomeDeCidade.Chave(nome).Replace(' ', '-');
        var estado = NomeDeCidade.ArrumarEstado(uf);
        return estado == null ? baseSlug : $"{baseSlug}-{estado.ToLowerInvariant()}";
    }

    // As cidades que têm pelo menos um torneio visível ao público, da que tem mais pra que tem
    // menos. É o que alimenta o sitemap, o bloco de links em /Torneios e as próprias páginas.
    public static async Task<List<Lugar>> ListarAsync(DbPadelContext db)
    {
        var cidades = await db.Cidades.AsNoTracking().ToListAsync();
        if (cidades.Count == 0) return new List<Lugar>();

        // Clube -> cidade. Projetado (e não Include) porque só o par de ids interessa, e
        // porque navegação obrigatória carregada por Include vira INNER JOIN.
        var clubes = await db.Clubes.AsNoTracking()
            .Select(c => new { c.Id, c.CidadeId })
            .ToListAsync();
        var cidadeDoClube = clubes
            .Where(c => c.CidadeId != null)
            .ToDictionary(c => c.Id, c => c.CidadeId!.Value);

        var torneios = await db.Torneios.AsNoTracking().ToListAsync();

        // Quantos torneios por linha de cidade — ainda por grafia, porque é o que a FK aponta.
        var porCidadeId = new Dictionary<int, int>();
        foreach (var torneio in torneios.Where(PermissaoDeOrganizador.ApareceParaOPublico))
        {
            if (!cidadeDoClube.TryGetValue(torneio.ClubeId, out var cidadeId)) continue;
            porCidadeId[cidadeId] = porCidadeId.GetValueOrDefault(cidadeId) + 1;
        }

        // ⚠️ A soma acontece DEPOIS do agrupamento das grafias. Contar por linha e só então
        // agrupar mostraria "Gravataí (1)" e "GRAVATAI (2)" como dois lugares — e nenhum dos
        // dois com o número certo.
        return CidadesSemRepetir.Agrupar(cidades)
            .Select(grupo => new Lugar(
                // O catálogo real tem "porto alegre" em minúsculo, e MelhorGrafia só escolhe
                // entre o que existe: sem esta passada o título da página sairia assim no
                // Google. O slug não muda por isso — ele normaliza de qualquer jeito.
                NomeDeCidade.ComoTitulo(grupo.Nome),
                grupo.Estado,
                SlugDe(grupo.Nome, grupo.Estado),
                grupo.Ids,
                grupo.Ids.Sum(id => porCidadeId.GetValueOrDefault(id))))
            .Where(lugar => lugar.Torneios > 0)
            .OrderByDescending(lugar => lugar.Torneios)
            .ThenBy(lugar => NomeDeCidade.Chave(lugar.Nome), StringComparer.Ordinal)
            .ToList();
    }

    // A página de uma cidade: o lugar e os torneios dele, do mais próximo pro mais distante.
    // Devolve null quando o endereço não corresponde a cidade nenhuma COM torneio — e aí quem
    // chama responde 404, que é o certo: 200 com uma tela vazia ensina o buscador a indexar
    // endereço que não leva a nada.
    public static async Task<(Lugar Lugar, List<Torneio> Torneios)?> AbrirAsync(DbPadelContext db, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var lugar = (await ListarAsync(db))
            .FirstOrDefault(l => string.Equals(l.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase));
        if (lugar == null) return null;

        var idsDaCidade = lugar.CidadeIds.ToHashSet();
        var clubesDaCidade = await db.Clubes.AsNoTracking()
            .Where(c => c.CidadeId != null && idsDaCidade.Contains(c.CidadeId.Value))
            .Select(c => c.Id)
            .ToListAsync();
        var clubes = clubesDaCidade.ToHashSet();

        var torneios = (await db.Torneios.AsNoTracking().ToListAsync())
            .Where(t => clubes.Contains(t.ClubeId) && PermissaoDeOrganizador.ApareceParaOPublico(t))
            // Sem data vai pro fim: torneio que ainda não marcou dia não pode encabeçar uma
            // lista cuja pergunta é "o que vem por aí".
            .OrderBy(t => t.DataInicio == null)
            .ThenBy(t => t.DataInicio)
            .ToList();

        return (lugar, torneios);
    }
}
