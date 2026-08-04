using Padelizou.Models;

namespace Padelizou.Services;

// Quantos estão inscritos no torneio INTEIRO.
//
// A tela já contava por categoria, mas só depois de escolher uma no seletor — pra saber o
// total o organizador tinha que passar por todas e somar de cabeça. E é o total que ele
// responde no WhatsApp o dia inteiro ("quantos já entraram?").
//
// A conta tem três casos que não se somam do mesmo jeito, e é por isso que ela mora aqui e
// não numa linha solta na view:
//   • formato Americano  → cada inscrição é UMA pessoa
//   • categoria de times → cada linha é UM time, e time não é dupla
//   • o resto           → cada linha é UMA dupla, completa ou ainda procurando parceiro
//
// Lista de espera fica de fora do número principal: quem está na fila não está inscrito, e
// juntar os dois faria o organizador anunciar vaga que não tem.
public record ContagemDeInscritos(int Duplas, int Pessoas, int Times, int NaEspera)
{
    public bool Vazio => Duplas == 0 && Pessoas == 0 && Times == 0;

    // O rótulo pronto pra tela, no singular certo. Um torneio pode ter dupla E time ao mesmo
    // tempo (categoria de times convive com as normais), então os dois aparecem quando os
    // dois existem.
    public string Rotulo
    {
        get
        {
            var partes = new List<string>();
            if (Duplas > 0) partes.Add(Duplas == 1 ? "1 dupla" : $"{Duplas} duplas");
            if (Pessoas > 0) partes.Add(Pessoas == 1 ? "1 inscrito" : $"{Pessoas} inscritos");
            if (Times > 0) partes.Add(Times == 1 ? "1 time" : $"{Times} times");

            return partes.Count == 0 ? "ninguém inscrito ainda" : string.Join(" + ", partes);
        }
    }
}

public static class QuantosInscritos
{
    public static ContagemDeInscritos Contar(
        Torneio torneio,
        IEnumerable<Categoria> categorias,
        IEnumerable<InscricaoAmericana> inscricoesAmericanas)
    {
        var todasAsDuplas = categorias.SelectMany(c => c.Duplas ?? new List<Dupla>()).ToList();
        var americanas = inscricoesAmericanas.ToList();

        // Americano cobra e joga por pessoa: contar "duplas" ali seria inventar um número
        // que não existe no torneio.
        bool ehAmericano = torneio.Formato == "Americano";

        var times = todasAsDuplas.Count(d => d.EhTime && !d.EmListaDeEspera);
        var duplas = ehAmericano
            ? 0
            : todasAsDuplas.Count(d => !d.EhTime && !d.EmListaDeEspera);
        var pessoas = ehAmericano
            ? americanas.Count(i => !i.EmListaDeEspera)
            : 0;

        var espera = todasAsDuplas.Count(d => d.EmListaDeEspera)
                   + americanas.Count(i => i.EmListaDeEspera);

        return new ContagemDeInscritos(duplas, pessoas, times, espera);
    }

    // O número de uma categoria só, pro seletor mostrar sem obrigar a entrar em cada uma.
    public static int DaCategoria(Torneio torneio, Categoria categoria,
        IEnumerable<InscricaoAmericana> inscricoesAmericanas) =>
        torneio.Formato == "Americano"
            ? inscricoesAmericanas.Count(i => i.CategoriaId == categoria.Id && !i.EmListaDeEspera)
            : (categoria.Duplas ?? new List<Dupla>()).Count(d => !d.EmListaDeEspera);

    // "1 dupla" e não "1 duplas". A tela por categoria escrevia sempre no plural, e com o
    // total no cabeçalho acertando o singular a diferença ficaria na cara.
    public static string Rotulo(int quantidade, string singular, string plural) =>
        quantidade == 1 ? $"1 {singular}" : $"{quantidade} {plural}";
}
