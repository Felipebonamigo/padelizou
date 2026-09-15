using Padelizou.Services;
using SkiaSharp;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — O PÓDIO CORTAVA O ÚLTIMO SEMIFINALISTA. 🗣️ Felipe, com o print da 4ª Masculina
// do 2ª Etapa ER Padel Tour: *"Aqui tambem"* — a linha SEMIFINALISTAS encostava nas duas bordas
// da arte e morria em "Marcio Rafae", sem o resto do nome.
//
// ⚠️ ESTE NÃO É O DEFEITO DE CSS DO MESMO DIA (`ArteNaoCortadaNoCelularTests`), e a diferença é
// a que importa: lá quem cortava era a TELA, e a arte baixada saía inteira; aqui o corte está
// DENTRO DO PNG e vai junto pro story de quem compartilha. Medido, os dois no mesmo dia: o
// nome do card de CAMPEÕES cabia (892px numa caixa de 920) — o corte do primeiro print era só
// o CSS. A linha de semifinalistas do PÓDIO media 1205px numa caixa de 940.
//
// 🔑 A CAUSA É O CHÃO DO `TamanhoQueCabe`. Ele encolhe a fonte por regra de três até o texto
// caber — mas termina em `Math.Max(tamanhoMinimo, proporcional)`: quando nem o mínimo cabe, ele
// devolve o mínimo assim mesmo e o `DrawText` desenha um texto MAIS LARGO que a caixa. O Skia
// não reclama e não corta com reticências: pinta até a borda do canvas e o resto não existe.
// Encolher salva nome de dupla; não salva QUATRO nomes numa linha só.
//
// 🔍 POR QUE O TESTE OLHA PIXEL, e não a conta: pela própria definição dele o `TamanhoQueCabe`
// está certo (devolve o mínimo, como está escrito). O que está errado é o que aparece na arte —
// e a arte é o produto. Medir tinta na margem trava o defeito de que Felipe reclamou sem
// depender de como ele venha a ser resolvido (encolher, quebrar em linhas, cortar).
public class ArteNaoCortaNomeDentroDoPngTests
{
    // A margem que os próprios cards declaram (`MargemH`, 140 no pódio e 160 no de campeões):
    // 70 e 80 de cada lado. O teste cobra 60 pra deixar folga pro antisserrilhado das letras —
    // o que se quer pegar é o texto que chega em x=0, não a curva de um "g" um pixel adiantada.
    private const int FaixaProibida = 60;

    // Acima disto é TINTA. O fundo é o gradiente navy com o brilho lime do canto (o mais claro
    // que ele chega é ~(53,72,61), luminância ~66); o texto é branco (255) ou lime claro. 140
    // separa os dois com sobra dos dois lados.
    private const int LimiarDeTinta = 140;

    // A faixa lime do topo é de bordo a bordo DE PROPÓSITO (`CartaoCompartilhavel.FaixaDoTopo`,
    // 14px) — é a assinatura da marca, não texto vazando.
    private const int AlturaDaFaixa = 14;

    // Os nomes são os do print, e isso é metade do valor do teste: nome real é mais comprido
    // que o nome que a gente digita ao testar, e foi por isso que o defeito passou.
    private static PodioDeCategoria PodioDoPrint() => new(
        CategoriaId: 7,
        Categoria: "4ª Masculina",
        Campeao: "Alexandre Longhi (Xandy)  &  Felipe Zago",
        Vice: "Felipe Bonamigo  &  Guilherme Bagesteiro",
        Semifinalistas: new List<string>
        {
            "Lucas Almeida (Foka)  &  Alexandre Costa (Camomila)",
            "Marcos Coelho  &  Marcio Rafael Machado",
        },
        Torneio: "2ª Etapa ER Padel Tour (EPT)",
        Clube: "Er Padel",
        Data: new DateTime(2026, 9, 11));

    [Fact]
    public void O_podio_nao_encosta_nome_nenhum_na_borda_da_arte()
    {
        var png = CartaoDoPodio.Desenhar(PodioDoPrint(), FontesDeVerdade(), RaizWeb());

        var invasoes = TintaNaMargem(png);

        Assert.True(invasoes.Count == 0, Recado(invasoes));
    }

    [Fact]
    public void O_podio_com_nomes_curtos_continua_passando()
    {
        // A trava do outro lado: o teste acima tem que estar medindo o card, e não recusando
        // qualquer pódio. Com nome curto ele já era verde ANTES da correção — foi assim que a
        // linha única, que continua sendo a preferida quando cabe, ficou provada de pé.
        var curto = PodioDoPrint() with
        {
            Campeao = "Ana  &  Bia",
            Vice = "Cris  &  Dani",
            Semifinalistas = new List<string> { "Eva  &  Fabi", "Gi  &  Helo" },
        };

        Assert.Empty(TintaNaMargem(CartaoDoPodio.Desenhar(curto, FontesDeVerdade(), RaizWeb())));
    }

    [Fact]
    public void O_podio_com_tres_semifinalistas_ainda_cabe_acima_da_divisoria()
    {
        // Chave torta, W.O. na semifinal: três duplas carimbadas "Semifinal". Três linhas
        // empilhadas no tamanho cheio bateriam na divisória — quem segura é a conta de altura,
        // não um número escolhido no olho.
        var tres = PodioDoPrint() with
        {
            Semifinalistas = new List<string>
            {
                "Lucas Almeida (Foka)  &  Alexandre Costa (Camomila)",
                "Marcos Coelho  &  Marcio Rafael Machado",
                "Anderson Matteus Schwaab  &  Charls Gustavio Polese",
            },
        };

        Assert.Empty(TintaNaMargem(CartaoDoPodio.Desenhar(tres, FontesDeVerdade(), RaizWeb())));
    }

    [Fact]
    public void O_card_de_campeoes_tambem_segura_o_nome_mais_longo_que_existe()
    {
        // ⚠️ ESTE JÁ PASSAVA — entra como trava, não como correção: o nome do print #1 media
        // 892px numa caixa de 920, e quem o cortava era o CSS da tela. Fica aqui porque o card
        // de campeões desenha do mesmo jeito que o pódio e tem o mesmo chão silencioso; sem
        // trava, o dia em que ele passar do limite é um nome de campeão pela metade no story.
        //
        // 🔎 E o caso limite não é o que o comentário do `NomeDaDupla` faz supor: `ComoChamar`
        // encurta "Anderson Matteus Schwaab" pra "Anderson Schwaab" (primeiro + último). O que
        // alonga de verdade é o APELIDO, que vai entre parênteses e não é encurtado por nada —
        // é ele que faz "Alexandre Costa (Camomila)". Então o pior caso real é dupla com nome
        // comprido E apelido nos dois, que é o que está montado aqui.
        var campeao = new CampeaoDeCategoria(
            CategoriaId: 3,
            Categoria: "Open Masculina",
            DuplaId: 11,
            NomeTime: null,
            LogoDoTime: null,
            Jogador1: new Padelizou.Models.Jogador
            {
                Id = 1, Nome = "Anderson Matteus Schwaab", Apelido = "Andersinho",
            },
            Jogador2: new Padelizou.Models.Jogador
            {
                Id = 2, Nome = "Charls Gustavio Polese", Apelido = "Charlinho",
            },
            Torneio: "2ª Etapa ER Padel Tour (EPT)",
            Clube: "Er Padel",
            Data: new DateTime(2026, 9, 11));

        // O nome é montado pelo `NomeDaDupla` (mesma régua das telas), não escrito aqui — um
        // literal esconderia justamente uma mudança de separador, que é o que mexe na largura.
        // E precisa ser mais longo que o do print, senão a trava não está travando nada.
        Assert.Contains("(Andersinho)", campeao.Nomes);
        Assert.True(campeao.Nomes.Length > "Jean Fernandes  &  Lucas Pedroso (Pedrosin)".Length,
            $"O pior caso ficou mais CURTO que o do print ({campeao.Nomes}) — a trava não vale nada assim.");

        Assert.Empty(TintaNaMargem(CartaoDeCampeao.Desenhar(campeao, FontesDeVerdade(), RaizWeb())));
    }

    private static string Recado(List<(int Y, int Colunas)> invasoes) =>
        $"Texto passando da margem de {FaixaProibida}px da arte — no PNG, não na tela: o " +
        "`TamanhoQueCabe` devolve o `tamanhoMinimo` quando nem ele cabe, e o Skia pinta até a " +
        "borda do canvas e some com o resto (foi assim que 'Marcio Rafael Machado' virou " +
        "'Marcio Rafae'). Linhas com tinta na margem: " +
        string.Join(", ", invasoes.Take(12).Select(i => $"y={i.Y} ({i.Colunas}px)")) +
        (invasoes.Count > 12 ? $" … e mais {invasoes.Count - 12}" : "");

    // Varre o PNG e devolve as linhas em que há tinta dentro da margem — de qualquer um dos
    // dois lados, porque o texto é CENTRALIZADO e vaza pelos dois ao mesmo tempo.
    private static List<(int Y, int Colunas)> TintaNaMargem(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        var achados = new List<(int, int)>();

        for (var y = AlturaDaFaixa; y < bitmap.Height; y++)
        {
            var colunas = 0;

            for (var x = 0; x < bitmap.Width; x++)
            {
                if (x >= FaixaProibida && x < bitmap.Width - FaixaProibida) continue;

                var p = bitmap.GetPixel(x, y);
                var luminancia = (0.299 * p.Red) + (0.587 * p.Green) + (0.114 * p.Blue);
                if (luminancia > LimiarDeTinta) colunas++;
            }

            if (colunas > 0) achados.Add((y, colunas));
        }

        return achados;
    }

    private static FonteDoCartao FontesDeVerdade() => new(PastaDasFontes());

    private static string RaizWeb() => Path.GetDirectoryName(PastaDasFontes())!;

    private static string PastaDasFontes()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "wwwroot", "fonts");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }

        throw new DirectoryNotFoundException("Não achei Padelizou/wwwroot/fonts a partir de " + AppContext.BaseDirectory);
    }
}
