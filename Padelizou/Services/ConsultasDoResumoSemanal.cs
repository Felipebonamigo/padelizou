using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// AS CONSULTAS do resumo semanal, separadas do varredor pra poderem ser COMPILADAS num teste.
//
// 💥 POR QUE ESTE ARQUIVO EXISTE: a primeira versão pedia os ids assim —
//
//     context.Desafios.SelectMany(d => new[] { d.DesafianteJogador1Id, ... })
//
// — e o Npgsql RECUSA: *"The LINQ expression 'd => new int[]{ ... }' could not be translated"*.
// Projetar colunas dentro de um array literal não vira SQL. E o defeito era invisível: o banco
// InMemory de toda a suíte não traduz nada, então 5.000 testes verdes conviviam com uma rotina
// que estourava na primeira quinta-feira em produção.
//
// ⚠️ A saída não é `AsEnumerable()` antes do `SelectMany` — isso puxaria a tabela inteira pro
// processo. O que se projeta é um tipo anônimo com as colunas (SQL entende), e o achatamento
// acontece na memória, sobre um resultado que já veio pequeno.
public static class ConsultasDoResumoSemanal
{
    // Os anúncios que estão no mural agora. `ValeAte == null` é o "até alguém aceitar", e sem
    // ele o anúncio sem prazo sumiria da conta — a mesma armadilha da coluna nulável que já
    // mordeu o mural e o "meu anúncio".
    public static IQueryable<AnuncioDeDesafio> NoMural(DbPadelContext context, DateTime agora) =>
        context.AnunciosDeDesafio
            .AsNoTracking()
            .Include(a => a.Cidades).ThenInclude(c => c.Cidade)
            .Where(a => a.Status == AnuncioDeDesafio.Publicado
                && a.Jogador2Id != null
                && (a.ValeAte == null || a.ValeAte >= agora));

    // Os dois lados de cada anúncio, como colunas — não como array.
    public static IQueryable<ParDeIds> DuplasDosAnuncios(DbPadelContext context) =>
        context.AnunciosDeDesafio
            .AsNoTracking()
            .Select(a => new ParDeIds(a.Jogador1Id, a.Jogador2Id));

    // Os quatro de cada desafio, como colunas.
    //
    // ⚠️ NÃO reusar `Desafio.Envolvidos` aqui: ela é `[NotMapped]` e devolve exatamente o
    // `new[] { ... }` que o provedor recusa. A propriedade serve pra código em memória.
    public static IQueryable<QuatroIds> EnvolvidosDosDesafios(DbPadelContext context) =>
        context.Desafios
            .AsNoTracking()
            .Select(d => new QuatroIds(
                d.DesafianteJogador1Id, d.DesafianteJogador2Id,
                d.DesafiadoJogador1Id, d.DesafiadoJogador2Id));

    // Todo mundo que já usou o mural: publicou, foi parceiro, ou esteve num desafio.
    public static async Task<List<int>> QuemJaUsouAsync(DbPadelContext context,
        CancellationToken cancelationToken = default)
    {
        var duplas = await DuplasDosAnuncios(context).ToListAsync(cancelationToken);
        var envolvidos = await EnvolvidosDosDesafios(context).ToListAsync(cancelationToken);

        // O achatamento é aqui, na memória, sobre linhas que já vieram do banco.
        return duplas.SelectMany(d => new[] { d.Um, d.Dois ?? 0 })
            .Concat(envolvidos.SelectMany(e => new[] { e.Um, e.Dois, e.Tres, e.Quatro }))
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    public record ParDeIds(int Um, int? Dois);

    public record QuatroIds(int Um, int Dois, int Tres, int Quatro);
}
