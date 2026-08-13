namespace Padelizou.ViewModels;

// O que a TELA DE CRIAR TORNEIO precisa saber além do próprio Torneio — hoje, o atalho
// "copiar configurações de um torneio meu".
//
// ⚠️ CLASSE DE VERDADE, e não tipo anônimo no ViewBag. Tipo anônimo é `internal` e as views
// compilam em outro assembly: o `as` devolve nulo, o bloco inteiro some da tela e **não há
// erro nenhum** — nem exceção, nem log, nem teste vermelho. Foi exatamente assim que este
// atalho nasceu invisível, e só apareceu abrindo a tela.
public class TorneioParaCopiarVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public DateTime? DataInicio { get; set; }
}

// A categoria de TIMES do torneio copiado. Ela não vem do catálogo (o código dela é
// sorteado), então não entra pelos checkboxes de categoria — reaparece no bloco próprio.
public class CategoriaDeTimesCopiadaVM
{
    public string Nome { get; set; } = "";
    public int? Quantidade { get; set; }
    public int? Grupos { get; set; }
    public int? Classificados { get; set; }
}
