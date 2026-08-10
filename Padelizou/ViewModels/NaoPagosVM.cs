using Padelizou.Models;

namespace Padelizou.ViewModels;

// A tela da decisão sobre quem não pagou. Duplas e inscrições individuais vêm separadas porque
// a remoção de cada uma mexe numa tabela diferente — juntá-las numa lista só exigiria carregar
// de qual tabela cada linha veio, e é assim que uma remoção apaga a coisa errada.
public class NaoPagosVM
{
    public Torneio Torneio { get; set; } = null!;
    public List<Linha> Duplas { get; set; } = new();
    public List<Linha> Americanas { get; set; } = new();

    public int Total => Duplas.Count + Americanas.Count;
    public decimal ValorTotal => Duplas.Sum(l => l.Valor) + Americanas.Sum(l => l.Valor);

    public class Linha
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal Valor { get; set; }
    }
}
