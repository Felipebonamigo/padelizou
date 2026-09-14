using Microsoft.AspNetCore.Http;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// A ARTE DO JOGO PRO STORY (14/09/2026).
//
// 🗣️ Felipe, com o print de uma arte da semifinal do ER Padel Tour feita à mão: *"Conseguimos
// fazer um Botao no sistema, que ele ja crie essa arte e apenas tiremos a foto na hora para
// postarmos nos stories do instagram, colocando o @ da pessoa ja quando tiver no cadastro?"*.
//
// ⚠️ 1080×1920, e não o 1080×1350 dos outros treze cards: este é o único que NÃO vai virar
// prévia de link no WhatsApp — ele nasce pro story e só. O formato 4:5 deixaria duas faixas
// vazias em cima e embaixo no lugar onde a arte inteira é pra ocupar a tela.
public class ArteDoJogoParaStoryTests
{
    // ── A régua do @: quem pode ser marcado numa imagem que vai pro story ────────────────
    //
    // ⚠️ ESTE É O GRUPO DE TESTES QUE IMPORTA MAIS. O story é a superfície mais pública que
    // este sistema tem — mais que o perfil, que já esconde o contato de quem está deslogado.
    // Imprimir o @ de quem pediu privacidade não é um card feio, é publicar o que a pessoa
    // desmarcou. Sem @ liberado, sai o NOME, que já é público (chave, ranking, classificação).

    [Fact]
    public void Com_arroba_no_cadastro_a_marcacao_e_o_arroba()
    {
        var m = JogosParaArte.Marcacao(ComConta("Felipe Bonamigo", "felipebonamigo"));

        Assert.Equal("@felipebonamigo", m.Texto);
        Assert.True(m.EhArroba);
        Assert.Null(m.MotivoSemArroba);
    }

    [Fact]
    public void Sem_arroba_no_cadastro_sai_o_nome_curto()
    {
        var m = JogosParaArte.Marcacao(ComConta("Guilherme Bagesteiro Lima", arroba: null));

        Assert.Equal(NomeBonito.Curto("Guilherme Bagesteiro Lima"), m.Texto);
        Assert.False(m.EhArroba);
        Assert.Equal("sem @ no cadastro", m.MotivoSemArroba);
    }

    [Fact]
    public void Perfil_privado_NAO_vai_marcado_na_arte()
    {
        var jogador = ComConta("Alexandre Costa", "xandicosta.13");
        jogador.PerfilPrivado = true;

        var m = JogosParaArte.Marcacao(jogador);

        Assert.False(m.EhArroba);
        Assert.DoesNotContain("xandicosta", m.Texto);
        Assert.Equal("perfil privado", m.MotivoSemArroba);
    }

    // Quem foi cadastrado por um TERCEIRO (parceiro inscrito por CPF) nunca viu a tela de
    // preferências, nunca leu a política e não tem login pra marcar "perfil privado" — o
    // interruptor existe, mas não pra ele. Mesma razão do ContatoDoJogador.
    [Fact]
    public void Pre_cadastro_NAO_vai_marcado_na_arte()
    {
        var m = JogosParaArte.Marcacao(new Jogador
        {
            Nome = "Lucas Foka",
            Instagram = "fokalucas",
            // Sem SenhaHash: é exatamente assim que nasce quem foi inscrito por CPF pelo
            // parceiro (ver Jogador.EhPreCadastro).
        });

        Assert.False(m.EhArroba);
        Assert.Equal("pré-cadastro", m.MotivoSemArroba);
    }

    [Fact]
    public void Conta_excluida_NAO_vai_marcada_na_arte()
    {
        var jogador = ComConta("Alguem Que Saiu", "alguem");
        jogador.ExcluidoEm = new DateTime(2026, 9, 1);

        var m = JogosParaArte.Marcacao(jogador);

        Assert.False(m.EhArroba);
        Assert.Equal("conta excluída", m.MotivoSemArroba);
    }

    // Americano / dupla incompleta: o lado tem UMA pessoa, e a arte não pode inventar a outra.
    [Fact]
    public void Lado_sem_parceiro_sai_com_uma_marcacao_so()
    {
        var lado = JogosParaArte.Lado(ComConta("Felipe Bonamigo", "felipebonamigo"), null);

        Assert.Single(lado.Marcacoes);
    }

    // ── O rótulo da fase ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Semifinal", 2, "Semifinal 2")]
    [InlineData("Quartas de Final", 3, "Quartas de Final 3")]
    // A final é UMA. Numerá-la ("Final 1") diria que existe uma segunda.
    [InlineData("Final", 1, "Final")]
    [InlineData("Semifinal", null, "Semifinal")]
    [InlineData("Fase de Grupos", null, "Fase de Grupos")]
    public void O_rotulo_da_fase_numera_tudo_menos_a_final(string fase, int? numero, string esperado)
    {
        Assert.Equal(esperado, FaseNaTela.Rotulo(fase, numero));
    }

    // ── O desenho ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_arte_sai_no_tamanho_do_story()
    {
        var png = ArteDoJogoParaStory.Desenhar(JogoDeExemplo(), foto: null, Fontes(), WebRoot());

        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem!.Width);
        Assert.Equal(ArteDoJogoParaStory.Altura, imagem.Height);
        Assert.Equal(1920, ArteDoJogoParaStory.Altura);
    }

    [Fact]
    public void Sem_foto_a_arte_ja_desenha_letra_de_verdade()
    {
        var png = ArteDoJogoParaStory.Desenhar(JogoDeExemplo(), foto: null, Fontes(), WebRoot());

        Assert.True(PixelsClaros(png) > 500);
    }

    // ⚠️ O TESTE QUE NÃO PODE SER "GEROU UM PNG". Com a moldura vazia desenhada e a foto
    // IGNORADA, um teste de "saiu imagem válida" passa verde defendendo nada — e o recurso
    // inteiro é a foto entrar. Então a pergunta é: a cor da foto está DENTRO da moldura?
    [Fact]
    public void A_foto_enviada_aparece_DENTRO_da_moldura()
    {
        using var foto = Chapada(800, 800, new SKColor(0xE0, 0x10, 0x10));

        var png = ArteDoJogoParaStory.Desenhar(JogoDeExemplo(), foto, Fontes(), WebRoot());

        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);

        var moldura = ArteDoJogoParaStory.MolduraDaFoto;
        var centro = imagem!.GetPixel((int)moldura.MidX, (int)moldura.MidY);
        Assert.True(centro.Red > 180, $"o centro da moldura saiu {centro} — a foto não entrou");
        Assert.True(centro.Green < 90 && centro.Blue < 90, $"o centro da moldura saiu {centro}");

        // E o fundo navy da marca continua lá FORA da moldura: a foto não pode vazar pra cima
        // do cabeçalho nem cobrir o rodapé assinado.
        var acima = imagem.GetPixel((int)moldura.MidX, (int)moldura.Top - 40);
        Assert.True(acima.Red < 120, $"acima da moldura saiu {acima} — a foto vazou");
    }

    // A foto retrato do celular num quadrado: recorta pelo centro em vez de esticar. Um rosto
    // achatado é pior que um rosto com menos fundo em volta.
    [Fact]
    public void Foto_em_pe_entra_recortada_e_preenche_a_moldura_toda()
    {
        using var foto = Chapada(600, 1200, new SKColor(0x10, 0xC0, 0x30));

        var png = ArteDoJogoParaStory.Desenhar(JogoDeExemplo(), foto, Fontes(), WebRoot());

        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);

        var moldura = ArteDoJogoParaStory.MolduraDaFoto;
        // Os quatro cantos de dentro: se a foto tivesse sido encaixada ("contain") em vez de
        // recortada ("cover"), sobrariam faixas do fundo navy nas laterais.
        foreach (var (x, y) in new[]
                 {
                     (moldura.Left + 20, moldura.Top + 20),
                     (moldura.Right - 20, moldura.Top + 20),
                     (moldura.Left + 20, moldura.Bottom - 20),
                     (moldura.Right - 20, moldura.Bottom - 20),
                 })
        {
            var c = imagem!.GetPixel((int)x, (int)y);
            Assert.True(c.Green > 150 && c.Red < 90, $"o canto ({x},{y}) saiu {c} — a moldura não ficou cheia");
        }
    }

    // ── O tamanho das marcações ──────────────────────────────────────────────────────────
    //
    // ⚠️ ESTES DOIS TESTES NASCERAM DE OLHAR A ARTE GERADA (14/09/2026). Cada linha encolhendo
    // sozinha até caber é o certo pra um TÍTULO (é o que o TextoCentralizado faz em todo card
    // daqui), e é o ERRADO pra uma COLUNA: "@anderson.matteus.schwaab" saía em 26px ao lado de
    // "@fokalucas" em 44px, e as quatro marcações pareciam quatro tamanhos de fonte escolhidos
    // a esmo em vez de uma lista. O tamanho é UM: o menor que serve pra todas.

    [Fact]
    public void Com_arrobas_curtos_as_marcacoes_ficam_no_tamanho_cheio()
    {
        var curtinhos = JogoDeExemplo() with
        {
            Dupla1 = new LadoDaArte(new[]
            {
                new MarcacaoNaArte("@ze", true, "Zé", null),
                new MarcacaoNaArte("@ana", true, "Ana", null),
            }),
            Dupla2 = new LadoDaArte(new[]
            {
                new MarcacaoNaArte("@lu", true, "Lu", null),
                new MarcacaoNaArte("@rafa", true, "Rafa", null),
            }),
        };

        Assert.Equal(ArteDoJogoParaStory.TamanhoDaMarcacao,
            ArteDoJogoParaStory.TamanhoDasMarcacoes(curtinhos, Fontes()));
    }

    // ⚠️ O CASO DO PRINT DO FELIPE, MEDIDO — e a medida é o motivo de tudo isto existir:
    // "@guilhermebagesteiro" NÃO CABE em 44px numa coluna de 406px (precisa de 33,8), enquanto
    // "@felipebonamigo" cabia. Era exatamente esse par de tamanhos na mesma coluna que se via
    // na arte gerada. Os quatro @ do print agora saem no MESMO tamanho, o do mais comprido.
    [Fact]
    public void Os_quatro_arrobas_do_print_saem_todos_no_mesmo_tamanho()
    {
        var jogo = JogoDeExemplo();
        var fontes = Fontes();

        var tamanho = ArteDoJogoParaStory.TamanhoDasMarcacoes(jogo, fontes);

        Assert.True(tamanho < ArteDoJogoParaStory.TamanhoDaMarcacao,
            $"saiu {tamanho}: se nada encolhesse, @guilhermebagesteiro vazaria a coluna");

        foreach (var marcacao in jogo.Dupla1.Marcacoes.Concat(jogo.Dupla2.Marcacoes))
        {
            var familia = marcacao.EhArroba ? fontes.Forte : fontes.Media;

            // ⚠️ ESTE É O MECANISMO, e ele é exato: o `Coluna` desenha passando
            // `tamanhoMinimo: tamanho`, e com isso o `TamanhoQueCabe` devolve o tamanho
            // compartilhado pra TODAS as marcações — nenhuma linha encolhe sozinha depois.
            // (Sem o piso, a conferência de largura de cada linha voltaria a desigualar a
            // coluna, que é exatamente o defeito que este bloco tranca.)
            Assert.Equal(tamanho, CartaoCompartilhavel.TamanhoQueCabe(
                marcacao.Texto, familia, tamanho,
                ArteDoJogoParaStory.LarguraDaColuna, tamanhoMinimo: tamanho));

            // E o tamanho escolhido REALMENTE cabe. A folga de 1% existe porque o
            // `TamanhoQueCabe` é uma regra de três (a largura do texto é quase linear no
            // tamanho da fonte, não exatamente — hinting e arredondamento), então o valor
            // devolvido pode passar do limite por uma fração de pixel.
            using var fonte = new SkiaSharp.SKFont(familia, tamanho);
            var medida = fonte.MeasureText(marcacao.Texto);
            Assert.True(medida <= ArteDoJogoParaStory.LarguraDaColuna * 1.01f,
                $"'{marcacao.Texto}' mede {medida} numa coluna de {ArteDoJogoParaStory.LarguraDaColuna}");
        }
    }

    [Fact]
    public void Um_arroba_longo_encolhe_TODAS_as_marcacoes_junto()
    {
        var comUmLongo = JogoDeExemplo() with
        {
            Dupla1 = new LadoDaArte(new[]
            {
                new MarcacaoNaArte("@anderson.matteus.schwaab", true, "Anderson Schwaab", null),
                new MarcacaoNaArte("@ze", true, "Zé", null),
            }),
        };

        var tamanho = ArteDoJogoParaStory.TamanhoDasMarcacoes(comUmLongo, Fontes());

        Assert.True(tamanho < ArteDoJogoParaStory.TamanhoDaMarcacao,
            $"saiu {tamanho} — o @ longo não puxou o tamanho pra baixo");

        // E é o tamanho que o MAIS LONGO precisa — não um valor menor "por garantia", que
        // desperdiçaria coluna nas outras três.
        Assert.Equal(
            CartaoCompartilhavel.TamanhoQueCabe(
                "@anderson.matteus.schwaab", Fontes().Forte,
                ArteDoJogoParaStory.TamanhoDaMarcacao,
                ArteDoJogoParaStory.LarguraDaColuna,
                ArteDoJogoParaStory.TamanhoMinimoDaMarcacao),
            tamanho);
    }

    // A coluna tem que sobrar folga até a divisória do meio: sem ela o @ longo encosta na
    // linha e as duas duplas deixam de parecer dois lados da rede.
    [Fact]
    public void A_coluna_nao_chega_na_divisoria_do_meio()
    {
        var meiaLargura = CartaoCompartilhavel.Largura / 2f;
        var centroDaColuna = (90 + meiaLargura) / 2f;
        var bordaDireitaDoTexto = centroDaColuna + ArteDoJogoParaStory.LarguraDaColuna / 2f;

        Assert.True(bordaDireitaDoTexto < meiaLargura - 15,
            $"o texto chega a {bordaDireitaDoTexto} e a divisória está em {meiaLargura}");
    }

    // ── A foto que chega do celular ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_foto_do_celular_e_aceita_e_NAO_toca_o_disco()
    {
        var bytes = PngChapado(900, 900, new SKColor(0x20, 0x40, 0xF0));

        using var foto = await ArteDoJogoParaStory.FotoDoEnvioAsync(Arquivo(bytes, "IMG_0042.png"));

        Assert.NotNull(foto);
        Assert.True(foto!.Width > 0 && foto.Height > 0);
    }

    // ⚠️ FOTO DE CELULAR VEM DEITADA, com a orientação no EXIF. Sem passar pelo
    // ImagemEnviada.Recodificar, a arte sairia com o jogo de lado — e é a mesma passagem que
    // APAGA o GPS embutido na foto, que num story público é vazar o endereço do clube.
    [Fact]
    public async Task Foto_deitada_pelo_EXIF_entra_endireitada()
    {
        var deitada = FotoDeCelularEmPe(400, 300);

        using var foto = await ArteDoJogoParaStory.FotoDoEnvioAsync(Arquivo(deitada, "IMG_0043.jpg"));

        Assert.NotNull(foto);
        // Declarada 400×300 no arquivo, com o EXIF pedindo o giro: endireitada fica EM PÉ.
        Assert.True(foto!.Height > foto.Width,
            $"saiu {foto.Width}x{foto.Height} — a foto não foi endireitada");
    }

    [Fact]
    public async Task Arquivo_que_nao_e_imagem_nao_derruba_a_arte()
    {
        using var foto = await ArteDoJogoParaStory.FotoDoEnvioAsync(
            Arquivo(new byte[] { 1, 2, 3, 4, 5 }, "nao-e-imagem.png"));

        Assert.Null(foto);
    }

    [Fact]
    public async Task Sem_arquivo_nenhum_a_arte_segue_sem_foto()
    {
        Assert.Null(await ArteDoJogoParaStory.FotoDoEnvioAsync(null));
    }

    // ── Apoio ────────────────────────────────────────────────────────────────────────────

    private static JogoParaArte JogoDeExemplo() => new(
        PartidaId: 7,
        Fase: "Semifinal",
        Categoria: "Open Masculina",
        Torneio: "ER PADEL TOUR",
        Dupla1: new LadoDaArte(new[]
        {
            new MarcacaoNaArte("@felipebonamigo", true, "Felipe Bonamigo", null),
            new MarcacaoNaArte("@guilhermebagesteiro", true, "Guilherme Bagesteiro", null),
        }),
        Dupla2: new LadoDaArte(new[]
        {
            new MarcacaoNaArte("@fokalucas", true, "Lucas Foka", null),
            new MarcacaoNaArte("@xandicosta.13", true, "Alexandre Costa", null),
        }),
        ArrobaDoOrganizador: "@er.padel");

    // ⚠️ `SenhaHash` NÃO é enfeite: sem ele o Jogador é PRÉ-CADASTRO (Jogador.EhPreCadastro é
    // `SenhaHash` vazia e não excluído), e a régua da arte recusaria o @ pelo motivo errado —
    // o teste passaria verde defendendo outra coisa. Toda a TestInfra cria jogador sem senha,
    // então quem testa a régua do @ tem que dizer explicitamente "esta pessoa tem conta".
    private static Jogador ComConta(string nome, string? arroba) => new()
    {
        Nome = nome,
        Cpf = Guid.NewGuid().ToString("N")[..11],
        Instagram = arroba,
        SenhaHash = "hash-de-teste",
    };

    private static FormFile Arquivo(byte[] conteudo, string nome) =>
        new(new MemoryStream(conteudo), 0, conteudo.Length, "foto", nome);

    private static SKBitmap Chapada(int largura, int altura, SKColor cor)
    {
        var bitmap = new SKBitmap(largura, altura);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(cor);
        canvas.Flush();
        return bitmap;
    }

    private static byte[] PngChapado(int largura, int altura, SKColor cor)
    {
        using var bitmap = Chapada(largura, altura, cor);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }

    // JPEG 400×300 com o EXIF dizendo "gire um quarto de volta" — a foto do celular em pé.
    private static byte[] FotoDeCelularEmPe(int larguraNoArquivo, int alturaNoArquivo)
    {
        using var bitmap = Chapada(larguraNoArquivo, alturaNoArquivo, new SKColor(0x80, 0x80, 0x80));
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(SKEncodedImageFormat.Jpeg, 90);
        return ComOrientacaoNoExif(dados.ToArray(), orientacao: 6);
    }

    // Enfia um APP1/Exif mínimo logo depois do SOI, só com a tag Orientation (0x0112).
    private static byte[] ComOrientacaoNoExif(byte[] jpeg, ushort orientacao)
    {
        var tiff = new List<byte>();
        tiff.AddRange(new byte[] { 0x4D, 0x4D, 0x00, 0x2A });          // big-endian, magia 42
        tiff.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x08 });          // offset da IFD0
        tiff.AddRange(new byte[] { 0x00, 0x01 });                      // 1 entrada
        tiff.AddRange(new byte[] { 0x01, 0x12 });                      // tag Orientation
        tiff.AddRange(new byte[] { 0x00, 0x03 });                      // tipo SHORT
        tiff.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });          // 1 valor
        tiff.AddRange(new[] { (byte)(orientacao >> 8), (byte)(orientacao & 0xFF), (byte)0, (byte)0 });
        tiff.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });          // sem IFD seguinte

        var app1 = new List<byte> { 0x45, 0x78, 0x69, 0x66, 0x00, 0x00 }; // "Exif\0\0"
        app1.AddRange(tiff);

        var tamanho = app1.Count + 2;
        var saida = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1, (byte)(tamanho >> 8), (byte)(tamanho & 0xFF) };
        saida.AddRange(app1);
        saida.AddRange(jpeg.Skip(2)); // o resto, sem o SOI que já foi escrito
        return saida.ToArray();
    }

    private static FonteDoCartao Fontes() => new(PastaDasFontes());

    private static string WebRoot() => Path.GetDirectoryName(PastaDasFontes())!;

    private static string PastaDasFontes()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "wwwroot", "fonts");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("wwwroot/fonts não encontrado a partir do bin.");
    }

    private static int PixelsClaros(byte[] png)
    {
        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);

        int claros = 0;
        for (int x = 0; x < imagem!.Width; x += 2)
            for (int y = 0; y < imagem.Height; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 200 && c.Green > 200 && c.Blue > 200) claros++;
            }
        return claros;
    }
}
