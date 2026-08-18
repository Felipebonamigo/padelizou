using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// OS CARDS COMPARTILHÁVEIS: o card de campeão do torneio e o "seu ano no padel".
//
// ⚠️ O teste mais importante daqui é o da FONTE, e ele existe por causa de um defeito que NÃO
// reproduz nesta máquina: o projeto usa `SkiaSharp.NativeAssets.Linux.NoDependencies`, o Skia
// compilado sem fontconfig. No Windows daqui o Skia acha a fonte no DirectWrite e desenha tudo;
// no VPS o gerenciador de fontes nasce vazio, e um card desenhado com a fonte "do sistema"
// sairia com fundo, moldura, foto — e TODOS os textos invisíveis, sem erro nenhum no log.
//
// Por isso há dois testes de fonte: um provando que faltando o arquivo o recurso se DESLIGA
// (em vez de cair no sistema), e outro provando que o card desenhado tem MESMO letra rasterizada
// — que é o sintoma que o defeito de produção produziria.
public class CartoesCompartilhaveisTests
{
    // A pasta de fontes do projeto, a partir da pasta de saída dos testes.
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

    // A raiz do site, pro desenho procurar foto e logo. Os testes desenham sem foto nenhuma.
    private static string WebRootDeTeste() => Path.GetDirectoryName(PastaDasFontes())!;

    // ─────────────────────────── A FONTE ───────────────────────────

    [Fact]
    public void Fonte_que_nao_esta_no_disco_DESLIGA_o_recurso_em_vez_de_cair_na_do_sistema()
    {
        // Uma pasta que existe e está vazia — é o estado do VPS se alguém apagar os .ttf do
        // publish, e é o estado de qualquer servidor novo antes deste commit.
        var vazia = Path.Combine(Path.GetTempPath(), "pdz-fontes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vazia);

        try
        {
            var fontes = new FonteDoCartao(vazia);

            Assert.False(fontes.Disponivel);

            // ⚠️ NULO, e não `SKTypeface.Default`. Este é o teste inteiro: cair no default faria
            // tudo funcionar aqui no Windows e sair em branco no Linux. O card precisa deixar
            // de existir, não virar um retângulo mudo com a nossa marca embaixo.
            Assert.Null(fontes.Forte);
            Assert.Null(fontes.Media);
            Assert.Null(fontes.Normal);

            // E o motivo tem que dizer QUAL arquivo falta — senão vira "não funciona" no log.
            Assert.Contains(FonteDoCartao.ArquivoForte, fontes.Impedimento);
        }
        finally
        {
            Directory.Delete(vazia, recursive: true);
        }
    }

    [Fact]
    public void As_fontes_do_projeto_estao_no_repositorio_e_carregam()
    {
        var fontes = FontesDeVerdade();

        Assert.True(fontes.Disponivel, fontes.Impedimento ?? "");
        Assert.Null(fontes.Impedimento);
    }

    [Fact]
    public void O_card_desenhado_tem_LETRA_de_verdade_e_nao_so_fundo()
    {
        var campeao = CampeaoDeTeste("Rafael Souza", "Tania Lima", "3ª Categoria Masculina");
        var png = CartaoDeCampeao.Desenhar(campeao, FontesDeVerdade(), WebRootDeTeste());

        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem.Width);
        Assert.Equal(CartaoCompartilhavel.Altura, imagem.Height);

        // ⚠️ A CONTRAPROVA DO DEFEITO DE PRODUÇÃO. O fundo é navy e os enfeites são lime; o
        // BRANCO só aparece onde há texto ("CAMPEÕES", o nome do torneio). Com uma fonte sem
        // glifo — que é o que o VPS entregaria sem os .ttf — o card sairia inteiro e este
        // número seria zero.
        int pixelsBrancos = 0;
        for (int x = 0; x < imagem.Width; x += 2)
        {
            for (int y = 0; y < imagem.Height; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 240 && c.Green > 240 && c.Blue > 240) pixelsBrancos++;
            }
        }

        Assert.True(pixelsBrancos > 500,
            $"O card saiu praticamente sem texto branco ({pixelsBrancos} pixels) — é o sintoma "
            + "de fonte sem glifo, exatamente o que acontece no Linux sem os .ttf.");
    }

    [Fact]
    public void Nome_de_dupla_comprido_ENCOLHE_pra_caber_em_vez_de_vazar()
    {
        var fontes = FontesDeVerdade();
        const float larguraMaxima = CartaoCompartilhavel.Largura - 160;

        var curto = CartaoCompartilhavel.TamanhoQueCabe("Ana & Bia", fontes.Forte, 62, larguraMaxima);
        var comprido = CartaoCompartilhavel.TamanhoQueCabe(
            "Anderson Schwaab (Deco)  &  Charls Gustavio Polese", fontes.Forte, 62, larguraMaxima);

        Assert.Equal(62, curto);          // coube: não mexe
        Assert.True(comprido < 62);       // não coube: encolheu

        // E depois de encolher, cabe mesmo — é a garantia que interessa.
        using var fonte = new SKFont(fontes.Forte, comprido);
        Assert.True(fonte.MeasureText("Anderson Schwaab (Deco)  &  Charls Gustavio Polese") <= larguraMaxima + 1);
    }

    // ─────────────────────────── O CAMPEÃO ───────────────────────────

    private static CampeaoDeCategoria CampeaoDeTeste(string um, string? dois, string categoria) =>
        new(
            CategoriaId: 1,
            Categoria: categoria,
            DuplaId: 1,
            NomeTime: null,
            LogoDoTime: null,
            Jogador1: new Jogador { Id = 1, Nome = um },
            Jogador2: dois == null ? null : new Jogador { Id = 2, Nome = dois },
            Torneio: "Copa de Teste",
            Clube: "Er Padel",
            Data: new DateTime(2026, 8, 9));

    [Theory]
    [InlineData("3ª Categoria Masculina", true, "CAMPEÕES")]
    [InlineData("3ª Categoria Feminina", true, "CAMPEÃS")]
    [InlineData("3ª Categoria Masculina", false, "CAMPEÃO")]
    [InlineData("3ª Categoria Feminina", false, "CAMPEÃ")]
    public void O_rotulo_acompanha_o_genero_da_categoria_e_o_tamanho_da_dupla(
        string categoria, bool duasPessoas, string esperado)
    {
        Assert.Equal(esperado, CampeoesDoTorneio.Rotulo(categoria, duasPessoas));
    }

    [Fact]
    public async Task Torneio_CANCELADO_nao_anuncia_campeao_mesmo_com_final_jogada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        var campea = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        campea.UltimaFase = "Campeao";
        await ctx.SaveChangesAsync();

        // Antes de cancelar existe campeão...
        Assert.Single(await CampeoesDoTorneio.DoTorneioAsync(ctx, torneio.Id));

        torneio.Status = "Cancelado";
        await ctx.SaveChangesAsync();

        // ...e depois de cancelar, não. É a MESMA régua do ponto: o evento não aconteceu, e
        // esta seria a única tela do Padelizou dizendo que ele valeu.
        Assert.Empty(await CampeoesDoTorneio.DoTorneioAsync(ctx, torneio.Id));
    }

    [Fact]
    public async Task Categoria_de_TIME_coroa_o_time_e_nunca_o_organizador_que_cadastrou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        // A linha de TIME tem o organizador no Jogador1Id — é assim que o sistema guarda.
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = organizador.Id,
            NomeTime = "Batata Padel",
            UltimaFase = "Campeao",
        });
        await ctx.SaveChangesAsync();

        var campeao = Assert.Single(await CampeoesDoTorneio.DoTorneioAsync(ctx, torneio.Id));

        Assert.True(campeao.EhTime);
        Assert.Equal("Batata Padel", campeao.Nomes);
        Assert.DoesNotContain("Organizador", campeao.Nomes);
        // Nenhum jogador sai no card: quem levantou a taça foi o time.
        Assert.Null(campeao.Jogador1);
        Assert.Equal(0, campeao.Pessoas);
    }

    [Fact]
    public async Task Campeao_do_Americano_e_UMA_pessoa_e_o_card_diz_CAMPEAO_no_singular()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        var vencedora = new Jogador { Id = 0, Nome = "Caroline Tedesco", Cpf = "12312312399" };
        ctx.Jogadores.Add(vencedora);
        await ctx.SaveChangesAsync();

        // O carimbo do Americano: linha SEM parceiro (ver RoboDoChaveamento.CoroarNoAmericano).
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = vencedora.Id,
            Jogador2Id = null,
            UltimaFase = "Campeao",
        });
        await ctx.SaveChangesAsync();

        var campeao = Assert.Single(await CampeoesDoTorneio.DoTorneioAsync(ctx, torneio.Id));

        Assert.Equal(1, campeao.Pessoas);
        Assert.Contains("Caroline", campeao.Nomes);
        Assert.Equal("CAMPEÃO", campeao.Rotulo);   // "2ª Categoria Masculina" no cenário padrão
    }

    [Fact]
    public async Task Dois_carimbos_na_MESMA_categoria_viram_UM_card_so()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        // Não deveria acontecer — mas um acerto na mão no banco basta, e dois cards
        // contraditórios na página do torneio seriam impossíveis de explicar.
        foreach (var d in ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList())
        {
            d.UltimaFase = "Campeao";
        }
        await ctx.SaveChangesAsync();

        Assert.Single(await CampeoesDoTorneio.DoTorneioAsync(ctx, torneio.Id));
    }

    [Theory]
    [InlineData("6ª Categoria Masculina", "6-categoria-masculina")]
    [InlineData("Categoria Open Masculino", "categoria-open-masculino")]
    [InlineData("Mista A — Iniciantes", "mista-a-iniciantes")]
    [InlineData("3ª Feminina", "3-feminina")]
    public void O_nome_do_arquivo_e_ASCII_puro_porque_cabecalho_HTTP_nao_aceita_outra_coisa(
        string categoria, string esperado)
    {
        var nome = Padelizou.Controllers.CartoesController.Arquivo(categoria);

        Assert.Equal(esperado, nome);

        // ⚠️ A GARANTIA QUE IMPORTA. Este teste nasceu de um 500 real: o "ª" de "6ª Categoria"
        // NÃO é acento — é a letra U+00AA por conta própria, então passa por `FormD` e por
        // `char.IsLetterOrDigit` inteiro. Ele chegava no `Content-Disposition` e o Kestrel
        // derrubava a resposta com `Invalid non-ASCII or control character in header`. O card
        // de toda categoria numerada dava erro; só as "Open" funcionavam — e foi uma "Open" a
        // primeira que eu abri pra conferir.
        Assert.All(nome, c => Assert.InRange(c, (char)32, (char)126));
    }

    [Fact]
    public void Categoria_so_com_caractere_estranho_ainda_gera_um_nome()
    {
        // `filename=""` é cabeçalho inútil, e o navegador salva como "download".
        Assert.Equal("padelizou", Padelizou.Controllers.CartoesController.Arquivo("♣♣♣"));
    }

    // ─────────────────────────── O ANO ───────────────────────────

    private static JogoDoAno Jogo(int mes, bool? venceu, int? parceiro, string? nome, string? clube,
        bool torneio = false) =>
        new(new DateTime(2026, mes, 10), venceu, parceiro, nome, clube, torneio);

    private static DadosDaRetrospectiva Dados(params JogoDoAno[] jogos) =>
        new(2026, new Jogador { Id = 1, Nome = "Felipe Bonamigo" }, jogos,
            Torneios: 0, Titulos: 0, Finais: 0, Pontos: 0, MelhorPosicao: null, CategoriaDaPosicao: null);

    [Fact]
    public void O_parceiro_do_ano_e_com_quem_mais_jogou()
    {
        var ano = RetrospectivaDoAno.Montar(Dados(
            Jogo(1, true, 7, "Lucas Foka", "Er Padel"),
            Jogo(2, true, 7, "Lucas Foka", "Er Padel"),
            Jogo(3, false, 9, "Tania Lima", "Er Padel")));

        Assert.Equal("Lucas Foka", ano.Parceiro!.Nome);
        Assert.Equal(2, ano.Parceiro.Jogos);
        Assert.Equal(2, ano.Parceiro.Vitorias);
        Assert.Equal("Er Padel", ano.Clube!.Nome);
        Assert.Equal(3, ano.Clube.Jogos);
    }

    [Fact]
    public void Empate_em_games_nao_conta_como_derrota_no_aproveitamento()
    {
        // ⚠️ Só o jogo de GRUPO empata (games iguais); torneio sempre tem vencedor. Contar o
        // empate como derrota daria um aproveitamento que não bate com vitórias + derrotas.
        var ano = RetrospectivaDoAno.Montar(Dados(
            Jogo(1, true, null, null, null),
            Jogo(2, false, null, null, null),
            Jogo(3, null, null, null, null)));   // empatou

        Assert.Equal(3, ano.Jogos);
        Assert.Equal(1, ano.Vitorias);
        Assert.Equal(2, ano.JogosDecididos);
        Assert.Equal(50, ano.AproveitamentoPct);
    }

    [Fact]
    public void Ano_com_menos_de_tres_jogos_NAO_vira_card()
    {
        var poucos = RetrospectivaDoAno.Montar(Dados(
            Jogo(1, true, null, null, null),
            Jogo(2, true, null, null, null)));
        Assert.False(poucos.TemCard);

        var suficientes = RetrospectivaDoAno.Montar(Dados(
            Jogo(1, true, null, null, null),
            Jogo(2, true, null, null, null),
            Jogo(3, true, null, null, null)));
        Assert.True(suficientes.TemCard);
    }

    [Fact]
    public void O_ano_que_ainda_nao_chegou_nao_existe()
    {
        var agora = new DateTime(2026, 8, 11);

        Assert.True(RetrospectivaDoAno.AnoValido(2026, agora));
        Assert.True(RetrospectivaDoAno.AnoValido(2025, agora));
        Assert.False(RetrospectivaDoAno.AnoValido(2027, agora));   // futuro
        Assert.False(RetrospectivaDoAno.AnoValido(2024, agora));   // antes do Padelizou
    }

    [Fact]
    public void O_mes_sai_em_portugues_sem_depender_da_cultura_do_servidor()
    {
        // ⚠️ O VPS roda em UTC e a cultura dele NÃO é pt-BR. `ToString("MMMM")` devolveria
        // "August" no meio de um card em português, e aqui na máquina de desenvolvimento
        // ninguém veria isso acontecer.
        Assert.Equal("agosto", RetrospectivaDoAno.NomeDoMes(8));
        Assert.Equal("março", RetrospectivaDoAno.NomeDoMes(3));
    }

    [Fact]
    public async Task A_retrospectiva_conta_jogo_de_GRUPO_alem_do_torneio()
    {
        using var ctx = TestInfra.NovoContexto();

        var eu = new Jogador { Nome = "Felipe Bonamigo", Cpf = "11111111111" };
        var parceiro = new Jogador { Nome = "Lucas Foka", Cpf = "22222222222" };
        var rival1 = new Jogador { Nome = "Rival Um", Cpf = "33333333333" };
        var rival2 = new Jogador { Nome = "Rival Dois", Cpf = "44444444444" };
        ctx.Jogadores.AddRange(eu, parceiro, rival1, rival2);

        var clube = new Clube { Nome = "Er Padel" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var grupo = new padelizou.Models.GrupoPrivado { Nome = "Panelinha", CodigoConvite = "PANELA", AdministradorId = eu.Id };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        for (int i = 0; i < 3; i++)
        {
            ctx.JogosSemanais.Add(new JogoSemanal
            {
                GrupoId = grupo.Id,
                ClubeId = clube.Id,
                DataJogo = new DateTime(2026, 5, 10 + i),
                Dupla1Jogador1Id = eu.Id,
                Dupla1Jogador2Id = parceiro.Id,
                Dupla2Jogador1Id = rival1.Id,
                Dupla2Jogador2Id = rival2.Id,
                GamesDupla1 = 6,
                GamesDupla2 = 3,
                VencedorLado = Padelizou.Services.ResultadoDoJogoSemanal.Dupla1,
                RegistradoPorId = eu.Id,
            });
        }
        await ctx.SaveChangesAsync();

        var ano = await new EstatisticasService(ctx).ObterRetrospectivaAsync(eu.Id, 2026);

        // ⚠️ É o coração da decisão: o ranking oficial ignoraria estes três jogos (não são
        // torneio), e a retrospectiva conta — senão a maioria das pessoas veria um card vazio.
        Assert.Equal(3, ano.Jogos);
        Assert.Equal(3, ano.Vitorias);
        Assert.Equal(0, ano.JogosDeTorneio);
        Assert.True(ano.TemCard);
        Assert.Equal("Lucas Foka", ano.Parceiro!.Nome);
        Assert.Equal("Er Padel", ano.Clube!.Nome);
    }

    [Fact]
    public async Task Jogo_de_OUTRO_ano_fica_de_fora()
    {
        using var ctx = TestInfra.NovoContexto();

        var eu = new Jogador { Nome = "Felipe", Cpf = "11111111111" };
        var parceiro = new Jogador { Nome = "Lucas", Cpf = "22222222222" };
        var rival1 = new Jogador { Nome = "Rival Um", Cpf = "33333333333" };
        var rival2 = new Jogador { Nome = "Rival Dois", Cpf = "44444444444" };
        ctx.Jogadores.AddRange(eu, parceiro, rival1, rival2);
        await ctx.SaveChangesAsync();

        var grupo = new padelizou.Models.GrupoPrivado { Nome = "Panelinha", CodigoConvite = "PANELA", AdministradorId = eu.Id };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        foreach (var data in new[] { new DateTime(2025, 12, 31), new DateTime(2026, 1, 1) })
        {
            ctx.JogosSemanais.Add(new JogoSemanal
            {
                GrupoId = grupo.Id,
                DataJogo = data,
                Dupla1Jogador1Id = eu.Id,
                Dupla1Jogador2Id = parceiro.Id,
                Dupla2Jogador1Id = rival1.Id,
                Dupla2Jogador2Id = rival2.Id,
                GamesDupla1 = 6,
                GamesDupla2 = 2,
                VencedorLado = Padelizou.Services.ResultadoDoJogoSemanal.Dupla1,
                RegistradoPorId = eu.Id,
            });
        }
        await ctx.SaveChangesAsync();

        var servico = new EstatisticasService(ctx);

        // A virada do ano é o lugar clássico de errar por um: 31/12 é do ano velho, 01/01 do novo.
        Assert.Equal(1, (await servico.ObterRetrospectivaAsync(eu.Id, 2025)).Jogos);
        Assert.Equal(1, (await servico.ObterRetrospectivaAsync(eu.Id, 2026)).Jogos);
    }
}
