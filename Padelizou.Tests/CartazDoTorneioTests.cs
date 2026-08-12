using System.Globalization;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// O CARTAZ DE DIVULGAÇÃO DO TORNEIO.
//
// Os testes que mais valem aqui não são os de regra:
//
// 1) o do QR SEM CINZA, contraprova de um defeito invisível — QR ampliado com filtro suave lê
//    bem na tela grande do desenvolvedor e falha na câmera do celular sob a luz do clube. O
//    mesmo teste pega de quebra qualquer texto do cartaz que passe POR CIMA do código;
// 2) o do CLUBE QUE SUMIU, que é a cicatriz do INNER JOIN: navegação obrigatória num `Include`
//    faz o torneio inteiro desaparecer da consulta, e o método responde "não pode divulgar" em
//    vez de estourar;
// 3) o do NOME DA CATEGORIA QUE NÃO QUEBRA NO MEIO, que depende de um caractere invisível.
public class CartazDoTorneioTests
{
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

    private static FonteDoCartao FontesDeVerdade() => new(PastaDasFontes());
    private static string WebRootDeTeste() => Path.GetDirectoryName(PastaDasFontes())!;

    private const string LinkDeTeste = "https://padelizou.com.br/Torneios/Details/22";

    // O espaço que NÃO quebra linha, escrito por escape: ele é idêntico a um espaço comum na
    // tela, e um teste que dependesse de eu ter digitado o invisível certo não provaria nada.
    private const string Nbsp = " ";

    private static TorneioParaDivulgar Cartaz(
        string nome = "NATA PADEL TOUR",
        string? categorias = null,
        decimal preco = 120m,
        bool temData = true,
        string? capa = null) =>
        new(
            TorneioId: 22,
            Nome: nome,
            Capa: capa,
            Data: "10/10 a 11/10/2026",
            TemData: temData,
            Clube: "Golden Point",
            Cidade: "Gravataí",
            Categorias: categorias ?? DivulgacaoDoTorneio.LinhaDeCategorias(
                new[] { "3ª Masculina", "4ª Masculina", "5ª Masculina", "4ª Feminina" }),
            Preco: preco,
            Selo: "INSCRIÇÕES ABERTAS",
            Acao: "INSCREVA-SE");

    // A linha de categorias do PIOR CASO: oito nomes, que é o teto antes de virar contagem.
    private static string OitoCategorias() =>
        DivulgacaoDoTorneio.LinhaDeCategorias(
            Enumerable.Range(1, 8).Select(i => $"{i}ª Masculina").ToList());

    // ─────────────────────────── QUANDO EXISTE CARTAZ ───────────────────────────

    [Theory]
    [InlineData("Inscrições Abertas", true)]
    [InlineData("Chaves em Sorteio", true)]
    [InlineData("Fase de Grupos", true)]
    [InlineData("Mata-Mata", true)]
    [InlineData("Finalizado", false)]
    [InlineData("Cancelado", false)]
    public void Torneio_cancelado_e_torneio_finalizado_NAO_viram_cartaz(string status, bool divulga)
    {
        Assert.Equal(divulga, DivulgacaoDoTorneio.PodeDivulgar(status));
    }

    [Fact]
    public async Task Cartaz_de_torneio_CANCELADO_nao_e_desenhado_nem_por_link_direto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");

        Assert.NotNull(await DivulgacaoDoTorneio.DoTorneioAsync(ctx, torneio.Id));

        torneio.Status = CancelamentoDoTorneio.Status;
        await ctx.SaveChangesAsync();

        // ⚠️ A recusa é aqui dentro, e não só no botão da tela. Cartaz é a peça que mais
        // sobrevive ao próprio contexto — é salva na galeria e reencaminhada dias depois —, e
        // quem já tem o link continuaria baixando a arte de um torneio que não vai acontecer.
        Assert.Null(await DivulgacaoDoTorneio.DoTorneioAsync(ctx, torneio.Id));
    }

    [Fact]
    public async Task O_clube_que_sumiu_do_banco_NAO_derruba_o_cartaz()
    {
        using var ctx = TestInfra.NovoContexto();
        // MontarTorneio não cria clube: o torneio nasce com ClubeId apontando pra ninguém, que
        // é exatamente o estado que fazia o card de campeão sumir inteiro.
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");

        var cartaz = await DivulgacaoDoTorneio.DoTorneioAsync(ctx, torneio.Id);

        // ⚠️ O cartaz EXISTE — só sai sem o nome do clube. Com `Include(t => t.Clube)` a
        // consulta viraria INNER JOIN e devolveria null, e o sintoma seria "o botão do cartaz
        // não faz nada" num torneio perfeitamente válido.
        Assert.NotNull(cartaz);
        Assert.Equal(torneio.Nome, cartaz!.Nome);
        Assert.Null(cartaz.Clube);
        Assert.Equal("", cartaz.Local);
    }

    [Fact]
    public async Task As_categorias_saem_na_MESMA_ordem_das_outras_telas_do_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");

        // Cadastradas fora de ordem de propósito — é assim que acontece de verdade, quando o
        // organizador acrescenta uma categoria depois de já ter criado as outras.
        ctx.Categorias.Add(new Categoria { TorneioId = torneio.Id, Nome = "4ª Categoria Feminina", Codigo = "C4F" });
        ctx.Categorias.Add(new Categoria { TorneioId = torneio.Id, Nome = "Open Masculina", Codigo = "COM" });
        await ctx.SaveChangesAsync();

        var cartaz = await DivulgacaoDoTorneio.DoTorneioAsync(ctx, torneio.Id);

        // Masculinas da mais forte pra mais fraca, depois as femininas. E sem a palavra
        // "Categoria", que não informa nada dentro de um cartaz que já é de um torneio.
        var esperado = string.Join("   ·   ",
            $"Open{Nbsp}Masculina", $"2ª{Nbsp}Masculina", $"4ª{Nbsp}Feminina");

        Assert.Equal(esperado, cartaz!.Categorias);
    }

    [Fact]
    public void O_nome_da_categoria_nao_pode_QUEBRAR_no_meio_entre_duas_linhas()
    {
        var linha = DivulgacaoDoTorneio.LinhaDeCategorias(new[] { "3ª Masculina", "4ª Feminina" });

        // ⚠️ Dentro do nome vai NBSP; entre uma categoria e a seguinte, espaço comum. É isso que
        // faz a quebra cair no ponto separador em vez de deixar "4ª" no fim de uma linha e
        // "Feminina" sozinha no começo da outra — que foi como o cartaz saiu na 1ª conferência.
        Assert.Contains($"4ª{Nbsp}Feminina", linha);
        Assert.DoesNotContain("4ª Feminina", linha);

        // E o efeito, medido: numa largura que só comporta uma categoria por vez, cada linha
        // sai com o nome INTEIRO — nunca com um pedaço dele.
        var fontes = FontesDeVerdade();
        var linhas = CartaoCompartilhavel.QuebrarEmLinhas(linha, fontes.Media, 34, 260);

        Assert.True(linhas.Count > 1, "Nesta largura a linha de categorias tinha que quebrar.");
        Assert.All(linhas, l => Assert.DoesNotContain("Feminina", l.Replace($"4ª{Nbsp}Feminina", "")));
    }

    // ─────────────────────────── O QUE O CARTAZ DIZ ───────────────────────────

    [Theory]
    [InlineData("Inscrições Abertas", "INSCRIÇÕES ABERTAS", "INSCREVA-SE")]
    [InlineData("Chaves em Sorteio", "ACOMPANHE AO VIVO", "VEJA AS CHAVES")]
    [InlineData("Fase de Grupos", "ACOMPANHE AO VIVO", "VEJA AS CHAVES")]
    public void A_chamada_do_cartaz_muda_com_a_porta_da_inscricao(string status, string selo, string acao)
    {
        // ⚠️ Um "INSCRIÇÕES ABERTAS" em torneio de inscrição fechada manda a pessoa apontar a
        // câmera pra procurar um botão que não existe — e a frustração fica com o organizador,
        // com o nosso cartaz na mão dela.
        Assert.Equal(selo, DivulgacaoDoTorneio.Selo(status));
        Assert.Equal(acao, DivulgacaoDoTorneio.Acao(status));
    }

    [Fact]
    public void O_preco_sai_POR_ATLETA_porque_o_padel_anuncia_por_dupla()
    {
        // O valor do sistema é sempre por pessoa (Torneio.PrecoInscricao). Sem a unidade
        // escrita, a dupla chega ao checkout esperando pagar metade.
        Assert.Equal("R$ 120 por atleta", Cartaz(preco: 120m).PrecoComUnidade);
    }

    [Fact]
    public void O_centavo_aparece_quando_existe_e_some_quando_nao_existe()
    {
        // A lição do R$ 79,90: dinheiro deste projeto SEMPRE se testa com centavos.
        Assert.Equal("R$ 79,90", DivulgacaoDoTorneio.PrecoNaTela(79.90m));
        Assert.Equal("R$ 120", DivulgacaoDoTorneio.PrecoNaTela(120m));
        Assert.Equal("R$ 1.250", DivulgacaoDoTorneio.PrecoNaTela(1250m));
    }

    [Fact]
    public void O_preco_sai_em_reais_mesmo_com_a_cultura_do_processo_TROCADA()
    {
        // ⚠️ O `Program.cs` fixa pt-BR na thread, mas quem desenha o cartaz pode ser um teste
        // (que não passa pelo Program) ou um serviço de fundo. No Linux, sem cultura definida, o
        // `ToString("C")` invariante escreve "¤120.00" — moeda-fantasma no cartaz já impresso.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            Assert.StartsWith("R$", DivulgacaoDoTorneio.PrecoNaTela(120m));
            Assert.Equal("R$ 79,90", DivulgacaoDoTorneio.PrecoNaTela(79.90m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Preco_zerado_nao_vira_GRATUITO_no_cartaz()
    {
        // Torneio de graça existe; campo não preenchido existe muito mais. Anunciar de graça o
        // que vai ser cobrado é o mais caro dos dois erros.
        Assert.False(Cartaz(preco: 0m).MostraPreco);
        Assert.True(Cartaz(preco: 1m).MostraPreco);
    }

    [Fact]
    public void Muitas_categorias_viram_o_NUMERO_em_vez_de_uma_lista_ilegivel()
    {
        var oito = Enumerable.Range(1, 8).Select(i => $"{i}ª Masculina").ToList();
        var nove = Enumerable.Range(1, 9).Select(i => $"{i}ª Masculina").ToList();

        Assert.Contains("·", DivulgacaoDoTorneio.LinhaDeCategorias(oito));
        Assert.Equal("9 categorias", DivulgacaoDoTorneio.LinhaDeCategorias(nove));
        Assert.Equal("", DivulgacaoDoTorneio.LinhaDeCategorias(new List<string>()));
    }

    [Fact]
    public void Torneio_sem_data_nao_escreve_data_a_definir_no_cartaz()
    {
        var semData = new Torneio { Nome = "Copa", Codigo = "C1", DataInicio = null };

        // Na listagem "Data a definir" é informação (a pessoa compara torneios). Num cartaz de
        // divulgação é um vazio anunciado, e quem vê conclui que o torneio não vai sair — por
        // isso o desenho pergunta `TemData` antes de escrever a linha.
        Assert.False(DataDoTorneioNaTela.TemData(semData));
        Assert.False(Cartaz(temData: false).TemData);
    }

    // ─────────────────────────── A QUEBRA DE LINHA ───────────────────────────

    [Fact]
    public void Nome_comprido_de_torneio_QUEBRA_em_linhas_e_nao_perde_palavra()
    {
        var fontes = FontesDeVerdade();
        const string nome = "NATA PADEL TOUR — 2ª ETAPA GRAVATAÍ 2026";

        var linhas = CartaoCompartilhavel.QuebrarEmLinhas(nome, fontes.Forte, 78, 920);

        Assert.True(linhas.Count > 1, "Um nome deste tamanho tem que ocupar mais de uma linha.");

        // ⚠️ Nada se perde na quebra. Cortar com reticências levaria embora justamente o fim do
        // nome ("… 2ª Etapa"), que é o que distingue uma etapa da outra.
        Assert.Equal(nome, string.Join(" ", linhas));

        // E cada linha cabe mesmo na largura pedida.
        using var fonte = new SKFont(fontes.Forte, 78);
        Assert.All(linhas, linha => Assert.True(fonte.MeasureText(linha) <= 921));
    }

    [Fact]
    public void Nome_curto_continua_em_UMA_linha()
    {
        var fontes = FontesDeVerdade();
        var linhas = CartaoCompartilhavel.QuebrarEmLinhas("COPA DE VERÃO", fontes.Forte, 78, 920);

        Assert.Single(linhas);
        Assert.Equal("COPA DE VERÃO", linhas[0]);
    }

    // ─────────────────────────── O DESENHO ───────────────────────────

    [Fact]
    public void O_cartaz_desenhado_tem_LETRA_de_verdade_e_nao_so_fundo()
    {
        var png = CartazDoTorneio.Desenhar(Cartaz(), LinkDeTeste, FontesDeVerdade(), WebRootDeTeste());

        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem!.Width);
        Assert.Equal(CartaoCompartilhavel.Altura, imagem.Height);

        // ⚠️ A CONTRAPROVA DO DEFEITO DE PRODUÇÃO: com uma fonte sem glifo — que é o que o VPS
        // entrega sem os .ttf, sem erro nenhum no log — o cartaz sairia inteiro (fundo, faixa,
        // pílula, QR) e este número seria praticamente zero.
        //
        // A varredura para em y=1000 pra não contar o branco do QR, que existiria de qualquer
        // jeito e mascararia justamente a falta de texto.
        var brancos = 0;
        for (var x = 0; x < imagem.Width; x += 2)
        {
            for (var y = 0; y < 1000; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 240 && c.Green > 240 && c.Blue > 240) brancos++;
            }
        }

        Assert.True(brancos > 500,
            $"O cartaz saiu praticamente sem texto branco ({brancos} pixels) — é o sintoma de "
            + "fonte sem glifo, exatamente o que acontece no Linux sem os .ttf.");
    }

    [Fact]
    public void O_QR_sai_em_preto_e_branco_PUROS_e_nada_passa_por_cima_dele()
    {
        // O PIOR CASO de propósito: nome comprido (duas linhas), oito categorias (duas linhas)
        // e preço — tudo o que empurra o miolo pra baixo, na direção do QR.
        var apertado = Cartaz(
            nome: "NATA PADEL TOUR — 2ª ETAPA GRAVATAÍ 2026",
            categorias: OitoCategorias());

        ConferirOQr(apertado, "sem capa");
    }

    [Fact]
    public void Com_CAPA_o_miolo_desce_e_ainda_assim_nao_encosta_no_QR()
    {
        // ⚠️ O caso mais apertado do cartaz inteiro, e o que só existe porque o alto muda: com
        // capa, o selo e todo o miolo começam ~45px mais abaixo. Se algum bloco crescer, é aqui
        // que ele bate no QR primeiro — e num cartaz com capa bonita ninguém repara que o
        // código parou de ler.
        var capa = CriarCapaDeTeste();
        try
        {
            var apertado = Cartaz(
                nome: "NATA PADEL TOUR — 2ª ETAPA GRAVATAÍ 2026",
                categorias: OitoCategorias(),
                capa: capa.RelativoNoBanco);

            ConferirOQr(apertado, "com capa");
        }
        finally
        {
            File.Delete(capa.CaminhoFisico);
        }
    }

    private static void ConferirOQr(TorneioParaDivulgar cartaz, string caso)
    {
        var png = CartazDoTorneio.Desenhar(cartaz, LinkDeTeste, FontesDeVerdade(), WebRootDeTeste());

        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);

        // Uma janela BEM dentro do código (ele fica em volta de x=300, y=1150), longe da borda
        // arredondada da plaquinha branca, que é suavizada de propósito.
        var cinzas = 0;
        var pretos = 0;
        var brancos = 0;
        for (var x = 240; x <= 360; x++)
        {
            for (var y = 1090; y <= 1210; y++)
            {
                var c = imagem!.GetPixel(x, y);
                if (c.Red < 12 && c.Green < 12 && c.Blue < 12) pretos++;
                else if (c.Red > 243 && c.Green > 243 && c.Blue > 243) brancos++;
                else cinzas++;
            }
        }

        // ⚠️ ESTE É O TESTE QUE IMPORTA. Cinza no meio do QR significa uma de duas coisas, e as
        // duas são graves: ou o código foi ampliado com filtro suave (e aí ele lê na tela do
        // desenvolvedor e FALHA na câmera do celular sob a luz do clube), ou algum texto do
        // cartaz está desenhado por cima dele. Nos dois casos a imagem "parece certa".
        Assert.True(cinzas == 0,
            $"[{caso}] Achei {cinzas} pixels que não são preto nem branco dentro do QR — ou o "
            + "código foi suavizado ao ampliar, ou algo do cartaz está por cima dele.");

        Assert.True(pretos > 1000, $"[{caso}] Pouco preto dentro do QR ({pretos}).");
        Assert.True(brancos > 1000, $"[{caso}] Pouco branco dentro do QR ({brancos}).");
    }

    // Uma capa BRANCA, que é o pior caso pra legibilidade: é sobre ela que o véu do gradiente
    // tem que devolver o navy antes de o texto começar.
    private static (string RelativoNoBanco, string CaminhoFisico) CriarCapaDeTeste()
    {
        var nome = $"_teste-{Guid.NewGuid():N}.png";
        var pasta = Path.Combine(WebRootDeTeste(), "uploads", "capas-torneio");
        Directory.CreateDirectory(pasta);

        var fisico = Path.Combine(pasta, nome);

        using (var bitmap = new SKBitmap(1600, 900))
        {
            using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.White);
            using var imagem = SKImage.FromBitmap(bitmap);
            using var dados = imagem.Encode(SKEncodedImageFormat.Png, 90);
            File.WriteAllBytes(fisico, dados.ToArray());
        }

        return ($"/uploads/capas-torneio/{nome}", fisico);
    }

    [Fact]
    public void O_QR_e_ampliado_em_ESCALA_INTEIRA()
    {
        // O lado devolvido nunca é o pedido: ele é o múltiplo do número de módulos que mais se
        // aproxima por baixo, mais a folga da plaquinha. Receber exatamente o pedido
        // significaria escala fracionária — módulos de 7 e de 8 pixels alternados, o defeito
        // que a câmera sente e o olho não.
        using var bitmap = new SKBitmap(CartaoCompartilhavel.Largura, CartaoCompartilhavel.Altura);
        using var canvas = new SKCanvas(bitmap);

        var lado = CartaoCompartilhavel.Qr(canvas, LinkDeTeste, 300, 1150, 240);

        Assert.True(lado > 0, "O QR não foi desenhado.");
        Assert.True(lado <= 240 + 2 * Math.Max(16f, 240 * 0.06f),
            $"O QR desenhado ({lado}) passou do espaço reservado.");
    }

    [Fact]
    public void Sem_a_fonte_no_disco_o_cartaz_nao_e_desenhado()
    {
        var vazia = Path.Combine(Path.GetTempPath(), "pdz-fontes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vazia);

        try
        {
            var fontes = new FonteDoCartao(vazia);

            // O controller recusa antes de desenhar (404). Aqui o que se prende é o motivo:
            // sem os .ttf o recurso se DESLIGA em vez de cair na fonte do sistema, que no Linux
            // não existe e produziria um cartaz com todo texto invisível.
            Assert.False(fontes.Disponivel);
            Assert.Null(fontes.Forte);
        }
        finally
        {
            Directory.Delete(vazia, recursive: true);
        }
    }
}
