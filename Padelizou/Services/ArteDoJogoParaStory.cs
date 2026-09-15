using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Padelizou.Services;

// A ARTE DO JOGO PRO STORY DO INSTAGRAM (14/09/2026).
//
// 🗣️ Felipe, com o print de uma arte da semifinal do ER Padel Tour montada à mão: *"Conseguimos
// fazer um Botao no sistema, que ele ja crie essa arte e apenas tiremos a foto na hora para
// postarmos nos stories do instagram, colocando o @ da pessoa ja quando tiver no cadastro?"*.
//
// ⚠️ 1080×1920, e não o 1080×1350 dos outros treze cards. A escolha do 4:5 lá é justificada por
// sobreviver nos TRÊS destinos (feed, story e prévia de link no WhatsApp); esta arte tem UM
// destino só — o story —, e não declara `og:image` em lugar nenhum, porque imprime o @ de
// quatro pessoas. Livre dos outros dois destinos, ela ocupa a tela inteira do celular.
//
// ⚠️ SEM EMOJI, como todo card daqui: a Poppins não tem glifo de emoji e não há fonte de
// fallback (ver FonteDoCartao) — uma raquete no meio da frase sairia como espaço em branco.
//
// ⚠️ SEM A LOGO DO CLUBE, e isso é ausência de CAMPO, não esquecimento de desenho: o print
// traz o selo do ER Padel ao lado do nosso, e o `Clube` não tem coluna de logo. Quando tiver,
// entra aqui ao lado da logo da marca.
//
// ⚠️ NADA VAI PRO DISCO — nem o PNG que sai, nem a foto que entra. Ver a nota do
// `CartaoCompartilhavel`: guardar entraria no `tar` completo diário do backup, que guarda 14
// cópias, por uma imagem que é postada uma vez.
public static class ArteDoJogoParaStory
{
    // O story cheio. A largura é a da marca (1080) — o que muda é a altura.
    public const int Altura = 1920;

    // A moldura da foto. Pública porque é o que o teste olha: "a foto entrou?" só tem resposta
    // se o teste souber ONDE ela deveria estar — sem isso, um teste de "gerou um PNG válido"
    // passaria verde com a foto ignorada, que é justamente o recurso inteiro.
    //
    // Quadrada e de 900px: é a maior que cabe entre o cabeçalho e as marcações com 90px de
    // margem de cada lado — a mesma margem do resto da arte.
    public static readonly SKRect MolduraDaFoto = new(90, 560, 990, 1460);

    private const float MargemLateral = 90;

    // O tamanho cheio da marcação, e o piso de encolhimento.
    public const float TamanhoDaMarcacao = 44;
    public const float TamanhoMinimoDaMarcacao = 22;

    // A largura útil de cada coluna. O 44 de folga é o que separa o texto da divisória do meio:
    // sem ele um @ longo encosta na linha e as duas duplas deixam de parecer dois lados da rede.
    public const float LarguraDaColuna = CartaoCompartilhavel.Largura / 2f - MargemLateral - 44;

    // ── O que chega do celular ───────────────────────────────────────────────────────────

    // A FOTO QUE O ORGANIZADOR ACABOU DE TIRAR, pronta pra entrar na arte.
    //
    // ⚠️ PASSA PELO `ImagemEnviada.Recodificar`, e não por um `SKBitmap.Decode` direto. Não é
    // cerimônia: é ele que ENDIREITA a foto pela orientação do EXIF (foto de celular em pé vem
    // deitada no arquivo — a arte sairia com o jogo de lado) e é ele que APAGA os metadados,
    // entre eles a COORDENADA DE GPS que a câmera embute. Publicar num story a foto com o GPS
    // de dentro seria vazar o endereço do clube junto com a arte. E ele já traz as travas de
    // tamanho: 25 MB de arquivo e 12.000px de lado (o "decompression bomb").
    //
    // ⚠️ NUNCA GRAVA: `Recodificar` é o único pedaço do `ImagemEnviada` que não toca o disco —
    // foi separado do `SalvarAsync` justamente por isso.
    //
    // Nulo = não veio foto, ou o que veio não é imagem de verdade. Os dois casos dão a MESMA
    // arte (a da moldura vazia), porque nenhum dos dois é motivo pra recusar a arte inteira —
    // mas quem chama distingue os dois pra poder avisar (ver a nota no controller).
    public static async Task<SKBitmap?> FotoDoEnvioAsync(
        Microsoft.AspNetCore.Http.IFormFile? arquivo, ILogger? logger = null)
    {
        if (arquivo == null || arquivo.Length == 0) return null;
        if (arquivo.Length > ImagemEnviada.BytesMaximos) return null;
        if (!ImagemEnviada.ExtensaoAceita(arquivo.FileName)) return null;

        try
        {
            byte[] bytes;
            await using (var entrada = arquivo.OpenReadStream())
            using (var buffer = new MemoryStream())
            {
                await entrada.CopyToAsync(buffer);
                bytes = buffer.ToArray();
            }

            var arrumada = ImagemEnviada.Recodificar(bytes, FormatoDeImagem.FotoDaArte, logger);
            return arrumada == null ? null : SKBitmap.Decode(arrumada);
        }
        catch (Exception ex)
        {
            // Foto que não abre não pode derrubar a arte: a moldura vazia ainda é uma arte
            // postável. Mas o erro é REGISTRADO — "não deu certo" calado foi o que escondeu a
            // pasta de logos sem permissão por um dia inteiro (ver ImagemEnviada).
            logger?.LogWarning(ex, "A foto enviada pra arte do jogo não pôde ser lida.");
            return null;
        }
    }

    // ── O desenho ────────────────────────────────────────────────────────────────────────

    public static byte[] Desenhar(
        JogoParaArte jogo, SKBitmap? foto, FonteDoCartao fontes, string webRootPath)
    {
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, CartaoDeCampeao.LogoDaMarca);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas, Altura);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                Cabecalho(canvas, fontes, jogo, logo);
                CartaoCompartilhavel.FotoEmMoldura(
                    canvas, foto, MolduraDaFoto, fontes, "A FOTO DO JOGO ENTRA AQUI");
                Marcacoes(canvas, fontes, jogo);
                Assinatura(canvas, fontes, jogo);

                CartaoCompartilhavel.Rodape(canvas, fontes, altura: Altura);
            }, Altura);
        }
        finally
        {
            logo?.Dispose();
        }
    }

    // A FASE é o que se lê de longe — é ela que diz por que esta foto está sendo postada. O
    // nome do torneio vem abaixo e menor, pela mesma razão do card de campeão: quem vê já sabe
    // de que torneio se trata.
    private static void Cabecalho(
        SKCanvas canvas, FonteDoCartao fontes, JogoParaArte jogo, SKBitmap? logo)
    {
        CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 128, 96);

        CartaoCompartilhavel.TextoCentralizado(
            canvas, jogo.Fase.ToUpperInvariant(), 340, fontes.Forte, 116,
            CartaoCompartilhavel.Lime, CartaoCompartilhavel.Largura - MargemLateral * 2,
            tamanhoMinimo: 44);

        CartaoCompartilhavel.TextoCentralizado(
            canvas, jogo.Torneio.ToUpperInvariant(), 410, fontes.Media, 46,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemLateral * 2,
            tamanhoMinimo: 26);

        if (!string.IsNullOrWhiteSpace(jogo.Categoria))
        {
            CartaoCompartilhavel.Pilula(canvas, jogo.Categoria, 478, fontes, 38);
        }
    }

    // ⚠️ UM TAMANHO PRA TODAS AS QUATRO MARCAÇÕES: o menor que serve pra qualquer uma delas.
    //
    // Achado OLHANDO a arte gerada (14/09/2026). Deixar cada linha encolher sozinha até caber é
    // o certo pra um TÍTULO — é o que o `TextoCentralizado` faz em todos os cards daqui — e é o
    // ERRADO pra uma COLUNA: "@anderson.matteus.schwaab" saía em 26px ao lado de "@fokalucas"
    // em 44px, e as quatro marcações pareciam quatro tamanhos de fonte escolhidos a esmo. Uma
    // lista se lê como lista quando a tipografia é a mesma; o que varia aqui é COR e PESO
    // (marcado x não marcado), que é informação, não acidente de comprimento.
    public static float TamanhoDasMarcacoes(JogoParaArte jogo, FonteDoCartao fontes)
    {
        var menor = TamanhoDaMarcacao;

        foreach (var marcacao in jogo.Dupla1.Marcacoes.Concat(jogo.Dupla2.Marcacoes))
        {
            if (string.IsNullOrWhiteSpace(marcacao.Texto)) continue;

            menor = Math.Min(menor, CartaoCompartilhavel.TamanhoQueCabe(
                marcacao.Texto,
                marcacao.EhArroba ? fontes.Forte : fontes.Media,
                TamanhoDaMarcacao, LarguraDaColuna, TamanhoMinimoDaMarcacao));
        }

        return menor;
    }

    // OS @ EM DUAS COLUNAS, uma dupla de cada lado — a forma do print, e ela carrega
    // informação: quem lê sabe quem jogou COM quem sem precisar de rótulo.
    private static void Marcacoes(SKCanvas canvas, FonteDoCartao fontes, JogoParaArte jogo)
    {
        const float primeiraLinhaY = 1560;
        const float entrelinha = 92;

        var meio = CartaoCompartilhavel.Largura / 2f;
        var centroEsquerda = (MargemLateral + meio) / 2f;
        var centroDireita = CartaoCompartilhavel.Largura - centroEsquerda;

        // A divisória vertical entre as duas duplas: é ela que faz as colunas serem lados da
        // rede em vez de uma lista de quatro nomes em duas colunas por falta de espaço.
        var linhas = Math.Max(jogo.Dupla1.Marcacoes.Count, jogo.Dupla2.Marcacoes.Count);
        if (linhas > 0)
        {
            var alturaDaDivisoria = (linhas - 1) * entrelinha + 96;
            var topo = primeiraLinhaY - 66;
            using var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(70), IsAntialias = true };
            canvas.DrawRect(new SKRect(meio - 1, topo, meio + 1, topo + alturaDaDivisoria), tinta);
        }

        var tamanho = TamanhoDasMarcacoes(jogo, fontes);
        Coluna(canvas, fontes, jogo.Dupla1, centroEsquerda, primeiraLinhaY, entrelinha, tamanho);
        Coluna(canvas, fontes, jogo.Dupla2, centroDireita, primeiraLinhaY, entrelinha, tamanho);
    }

    private static void Coluna(
        SKCanvas canvas, FonteDoCartao fontes, LadoDaArte lado,
        float centroX, float primeiraLinhaY, float entrelinha, float tamanho)
    {
        var y = primeiraLinhaY;

        foreach (var marcacao in lado.Marcacoes)
        {
            if (string.IsNullOrWhiteSpace(marcacao.Texto)) continue;

            // O @ sai em LIME e no peso forte; o nome de quem não pôde ser marcado sai em
            // branco e no peso médio. É a mesma diferença de COR E PESO que o card do placar
            // faz com quem está na frente — sem fonte de fallback, um ícone de Instagram ao
            // lado do @ sairia como espaço em branco.
            // `tamanho` chega JÁ DECIDIDO pelas quatro marcações (ver TamanhoDasMarcacoes), e
            // o mínimo é igual a ele — é isso que impede esta linha de encolher sozinha e
            // desigualar a coluna.
            CartaoCompartilhavel.Texto(
                canvas, marcacao.Texto, centroX, y,
                marcacao.EhArroba ? fontes.Forte : fontes.Media, tamanho,
                marcacao.EhArroba ? CartaoCompartilhavel.LimeClaro : CartaoCompartilhavel.Branco,
                LarguraDaColuna, tamanhoMinimo: tamanho);

            y += entrelinha;
        }
    }

    // O @ de quem organiza, quando o torneio tem um cadastrado — a linha que no print é o
    // @er.padel. Sem ele, a arte simplesmente não tem esta linha (e não um "@" solto).
    private static void Assinatura(SKCanvas canvas, FonteDoCartao fontes, JogoParaArte jogo)
    {
        if (string.IsNullOrWhiteSpace(jogo.ArrobaDoOrganizador)) return;

        var meio = CartaoCompartilhavel.Largura / 2f;
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
            canvas.DrawRect(new SKRect(meio - 90, 1748, meio + 90, 1750), tinta);

        CartaoCompartilhavel.TextoCentralizado(
            canvas, jogo.ArrobaDoOrganizador, 1808, fontes.Media, 44,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemLateral * 2,
            tamanhoMinimo: 24);
    }
}
