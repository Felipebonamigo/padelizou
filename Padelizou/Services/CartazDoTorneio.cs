using SkiaSharp;

namespace Padelizou.Services;

// O PNG que o organizador posta pra divulgar o torneio.
//
// É a peça do ANTES — irmã do `CartaoDeCampeao`, que é a do depois. A leitura tem que resolver
// em três segundos, na ordem em que a pessoa decide: primeiro ela reconhece que há um torneio
// (o selo), depois QUAL (o nome), depois SE DÁ (a data), depois onde e quanto. O QR fecha,
// porque só interessa a quem já decidiu — e é ele que faz este cartaz valer mais que um do
// Canva: a arte deixa de ser um aviso e vira uma porta.
//
// ⚠️ Nada aqui vai pro disco: ver a nota do `CartaoCompartilhavel`.
public static class CartazDoTorneio
{
    // ⚠️ AS ALTURAS TÊM NOME, E ISSO NÃO É ENFEITE. O card do ano nasceu com três blocos
    // empilhados por `y +=` e saiu com texto por cima de texto — a causa é que `Pilula` e
    // `FotoRedonda` recebem um CENTRO enquanto `Texto` recebe uma LINHA DE BASE, e misturar as
    // duas contas na mesma variável erra por meia altura de fonte a cada passo.
    //
    // Aqui o miolo é empilhado a partir do que cada bloco DEVOLVE (o nome pode ocupar uma linha
    // ou duas, e as categorias também), e o conjunto ainda é centralizado no espaço que sobra
    // (ver `CentroDoSelo`). O que estes números fixam são as BORDAS: onde a capa acaba, onde o
    // QR mora, e a distância entre a pílula e o nome.
    private const float BaseDaCapa = 320;

    // ⚠️ O ALTO DO CARTAZ MUDA CONFORME O TORNEIO TEM CAPA OU NÃO, e isso não é capricho: sem
    // capa, um selo posicionado para caber embaixo dela deixa um vão de 250px de navy liso
    // entre a marca e o nome do torneio — o cartaz parece cortado pela metade. Com capa, o
    // mesmo selo lá em cima cairia dentro da foto. Duas alturas resolvem os dois casos; uma só
    // estraga um deles, e o que estraga é sempre o que ninguém testou.
    private const float CentroDoSeloComCapa = 385;
    private const float CentroDoSeloSemCapa = 340;

    // ⚠️ DISTÂNCIA DO CENTRO DA PÍLULA À LINHA DE BASE DO NOME, e ela é grande porque soma três
    // coisas que não se veem no número: meia pílula (~41), a ALTURA DAS MAIÚSCULAS do nome
    // (~60 em corpo 78, e a linha de base fica embaixo delas) e o respiro. Na primeira versão
    // isto era 95 e o topo do "NATA PADEL TOUR" entrava dentro da pílula — o erro clássico de
    // somar um CENTRO com uma LINHA DE BASE como se fossem a mesma medida.
    private const float DoSeloAoNome = 125;

    private const float CentroDoQr = 1150;
    private const float CentroXdoQr = 300;
    private const float CentroXdaChamada = 730;

    // A margem lateral do texto. 80 de cada lado é o mesmo respiro do card de campeão.
    private const float LarguraUtil = CartaoCompartilhavel.Largura - 160;

    public static byte[] Desenhar(
        TorneioParaDivulgar torneio, string linkDoTorneio, FonteDoCartao fontes, string webRootPath)
    {
        // Carregadas fora do desenho pra serem descartadas de um jeito só, e porque uma capa
        // que não abre não pode interromper o cartaz no meio.
        var capa = CartaoCompartilhavel.Ler(webRootPath, torneio.Capa);
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, CartaoDeCampeao.LogoDaMarca);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas);
                Capa(canvas, capa);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                Cabecalho(canvas, fontes, logo);

                var selo = CentroDoSelo(fontes, torneio, capa != null);
                CartaoCompartilhavel.Pilula(canvas, torneio.Selo, selo, fontes, TamanhoDoSelo);

                Miolo(canvas, fontes, torneio, selo + DoSeloAoNome);
                Chamada(canvas, fontes, torneio, linkDoTorneio);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            capa?.Dispose();
            logo?.Dispose();
        }
    }

    // Onde o bloco "selo + informação" começa, DEPOIS de centralizado no espaço que sobra.
    //
    // ⚠️ ISTO EXISTE POR CAUSA DO INTERNO DE CLUBE. O cartaz mais completo (nome de duas
    // linhas, oito categorias, data, local e preço) preenche a arte sozinho; o mais enxuto —
    // "Interno de Sábado", sem data marcada e sem preço — deixava **550px de navy liso** entre
    // o nome e o QR. Os dois são o mesmo código, e um layout de posições fixas só pode servir
    // bem a um deles. O primeiro é o que se testa; o segundo é o que mais existe.
    //
    // ⚠️ E A MEDIDA É FEITA DESENHANDO, num bitmap de 1×1 que vai pro lixo. Parece desperdício
    // e é o contrário: uma função separada que "calcula a altura do miolo" seria uma SEGUNDA
    // CÓPIA das regras de espaçamento — e no dia em que alguém mexesse num `y +=` sem mexer na
    // outra, o cartaz sairia torto sem nenhum teste ficar vermelho. Aqui a medida não pode
    // divergir do desenho porque ela É o desenho. Custa uns milissegundos, num PNG que fica
    // uma hora em cache.
    private static float CentroDoSelo(FonteDoCartao fontes, TorneioParaDivulgar torneio, bool temCapa)
    {
        var padrao = temCapa ? CentroDoSeloComCapa : CentroDoSeloSemCapa;

        using var descartavel = new SKBitmap(1, 1);
        using var canvasDeMedida = new SKCanvas(descartavel);
        var fimDoMiolo = Miolo(canvasDeMedida, fontes, torneio, padrao + DoSeloAoNome);

        // A pílula é desenhada a partir do CENTRO; o miolo devolve uma LINHA DE BASE. As duas
        // pontas do bloco viram bordas aqui, e é essa conversão que o card do ano errou.
        var topoDoBloco = padrao - AlturaDaPilula / 2f;
        var alturaDoBloco = fimDoMiolo + DescidaDaUltimaLinha - topoDoBloco;

        var zonaInicio = temCapa ? BaseDaCapa + 45f : 265f;
        var zonaFim = CentroDoQr - 130f;

        var folga = (zonaFim - zonaInicio) - alturaDoBloco;
        if (folga <= 0) return padrao;  // não sobra espaço: fica onde estava

        return padrao + (zonaInicio + folga / 2f - topoDoBloco);
    }

    private const float TamanhoDoSelo = 38;

    // A pílula mede o corpo do texto mais a folga que o `CartaoCompartilhavel.Pilula` acrescenta.
    private const float AlturaDaPilula = TamanhoDoSelo + 44;

    // O quanto a última linha do miolo desce abaixo da própria linha de base.
    private const float DescidaDaUltimaLinha = 18;

    // A capa do torneio ocupando o alto, com o navy voltando por cima na base.
    //
    // ⚠️ O GRADIENTE NÃO É ESTÉTICA, É LEGIBILIDADE. A capa é foto escolhida pelo organizador:
    // pode ser um céu claro, uma quadra iluminada, um banner branco com letra colorida. Sem o
    // véu que devolve o navy sólido antes do texto começar, o nome do torneio cairia em cima de
    // uma região clara qualquer e sumiria — e isso só apareceria no dia em que alguém subisse
    // uma capa clara, muito depois de o recurso ter sido conferido com a capa escura de teste.
    private static void Capa(SKCanvas canvas, SKBitmap? capa)
    {
        if (capa == null || capa.Width == 0 || capa.Height == 0) return;

        var faixa = new SKRect(0, 0, CartaoCompartilhavel.Largura, BaseDaCapa);

        // Recorta a FATIA CENTRAL da capa na proporção da faixa, em vez de espremer a imagem
        // inteira: capa é quase sempre paisagem 16:9, e achatá-la numa faixa mais alta deforma
        // rosto e logotipo — o mesmo cuidado que a foto redonda tem.
        var proporcao = faixa.Width / faixa.Height;
        var larguraOrigem = Math.Min(capa.Width, capa.Height * proporcao);
        var alturaOrigem = larguraOrigem / proporcao;
        var origem = new SKRect(
            (capa.Width - larguraOrigem) / 2f, (capa.Height - alturaOrigem) / 2f,
            (capa.Width + larguraOrigem) / 2f, (capa.Height + alturaOrigem) / 2f);

        canvas.DrawBitmap(capa, origem, faixa, new SKSamplingOptions(SKCubicResampler.Mitchell));

        // Escurece o topo (pra marca aparecer sobre qualquer foto) e devolve o navy sólido na
        // base (pra o texto começar em fundo limpo).
        using (var tinta = new SKPaint { IsAntialias = true })
        {
            tinta.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, BaseDaCapa),
                new[]
                {
                    CartaoCompartilhavel.Navy.WithAlpha(215),
                    CartaoCompartilhavel.Navy.WithAlpha(60),
                    CartaoCompartilhavel.Navy.WithAlpha(235),
                    CartaoCompartilhavel.Navy,
                },
                new[] { 0f, 0.42f, 0.86f, 1f },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(faixa, tinta);
        }
    }

    private static void Cabecalho(SKCanvas canvas, FonteDoCartao fontes, SKBitmap? logo)
    {
        CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 122, 84);

        // Letras espaçadas à mão: `SKFont` não tem tracking. Mesma solução do card de campeão.
        CartaoCompartilhavel.TextoCentralizado(
            canvas, "P A D E L I Z O U", 205, fontes.Media, 30,
            CartaoCompartilhavel.Branco, LarguraUtil);
    }

    // Nome, data, local, categorias e preço — empilhados a partir do que o bloco anterior
    // devolveu, porque nome e categorias podem ocupar uma linha ou duas.
    private static float Miolo(
        SKCanvas canvas, FonteDoCartao fontes, TorneioParaDivulgar torneio, float primeiraLinha)
    {
        var y = CartaoCompartilhavel.TextoEmVariasLinhas(
            canvas, torneio.Nome.ToUpperInvariant(), primeiraLinha, fontes.Forte, 78,
            CartaoCompartilhavel.Branco, LarguraUtil, maximoDeLinhas: 2, tamanhoMinimo: 44);

        // A data é a segunda coisa que a pessoa procura ("eu tô livre nesse fim de semana?"),
        // então ela vem em lime — a única linha do miolo que puxa a cor da marca.
        //
        // ⚠️ Torneio sem data marcada NÃO escreve "Data a definir" no cartaz. Na listagem aquilo
        // é informação (a pessoa está comparando torneios); num cartaz de divulgação é um vazio
        // anunciado, e quem vê conclui que o torneio não vai sair.
        if (torneio.TemData)
        {
            y += 88;
            CartaoCompartilhavel.TextoCentralizado(
                canvas, torneio.Data, y, fontes.Forte, 56,
                CartaoCompartilhavel.LimeClaro, LarguraUtil, tamanhoMinimo: 34);
        }

        if (torneio.Local.Length > 0)
        {
            y += 62;
            CartaoCompartilhavel.TextoCentralizado(
                canvas, torneio.Local, y, fontes.Normal, 38,
                CartaoCompartilhavel.Apagado, LarguraUtil, tamanhoMinimo: 24);
        }

        if (torneio.Categorias.Length > 0)
        {
            y += 48;
            Regua(canvas, y);

            y += 62;
            y = CartaoCompartilhavel.TextoEmVariasLinhas(
                canvas, torneio.Categorias, y, fontes.Media, 34,
                CartaoCompartilhavel.Branco, LarguraUtil, maximoDeLinhas: 2, tamanhoMinimo: 24);
        }

        if (torneio.MostraPreco)
        {
            y += 74;
            CartaoCompartilhavel.TextoCentralizado(
                canvas, torneio.PrecoComUnidade, y, fontes.Media, 44,
                CartaoCompartilhavel.Branco, LarguraUtil, tamanhoMinimo: 26);
        }

        return y;
    }

    // A linha fina que separa "que torneio é" de "como ele é" — a mesma do card de campeão.
    private static void Regua(SKCanvas canvas, float y)
    {
        using var tinta = new SKPaint
        {
            Color = CartaoCompartilhavel.Apagado.WithAlpha(90),
            IsAntialias = true,
        };
        var meio = CartaoCompartilhavel.Largura / 2f;
        canvas.DrawRect(new SKRect(meio - 90, y, meio + 90, y + 2), tinta);
    }

    // O QR e o convite, lado a lado.
    //
    // ⚠️ DUAS COLUNAS, e não o QR centralizado com a frase embaixo: empilhado, ou o código fica
    // pequeno demais pra câmera de longe, ou a frase é espremida contra o rodapé. Lado a lado,
    // o QR fica grande e a frase ganha o espaço que precisa.
    private static void Chamada(
        SKCanvas canvas, FonteDoCartao fontes, TorneioParaDivulgar torneio, string linkDoTorneio)
    {
        // 240 é o ALVO, não o resultado: o desenho arredonda pra baixo até o múltiplo exato do
        // número de módulos (ver `Qr`), e o código de um endereço mais comprido tem mais
        // módulos, logo sai menor. Pedir com folga é o que mantém o QR grande o bastante pra
        // ser lido do outro lado da quadra mesmo no torneio de nome quilométrico.
        CartaoCompartilhavel.Qr(canvas, linkDoTorneio, CentroXdoQr, CentroDoQr, 240);

        const float larguraDaColuna = 440;

        // O gesto explicado em letra pequena. Parece óbvio, mas o cartaz vai parar em grupo de
        // WhatsApp de gente que nunca escaneou nada — e um QR sem instrução vira desenho.
        CartaoCompartilhavel.Texto(
            canvas, "APONTE A CÂMERA", CentroXdaChamada, CentroDoQr - 42, fontes.Normal, 28,
            CartaoCompartilhavel.Apagado, larguraDaColuna, tamanhoMinimo: 20);

        CartaoCompartilhavel.Texto(
            canvas, torneio.Acao, CentroXdaChamada, CentroDoQr + 26, fontes.Forte, 52,
            CartaoCompartilhavel.Lime, larguraDaColuna, tamanhoMinimo: 30);
    }
}
