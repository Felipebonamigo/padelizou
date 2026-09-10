using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using SkiaSharp;
using Xunit;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;
using Lado = Padelizou.Services.ProximasFasesDaChave.Lado;

namespace Padelizou.Tests;

// 10/09/2026 — "COMPARTILHAR ESTA LISTA": a aba Jogos, do jeito que está filtrada, vira texto
// pro grupo do WhatsApp e arte 1080×1350 pro story.
//
// 🗣️ Felipe, num print da aba Jogos do 2ª Etapa ER PADEL TOUR filtrada por "Los Corneteiros":
// *"no final da lista, criar um botão 'compartilhar lista' para o usuario poder mandar no grupo
// d whats dele, a lista selecionada, ou até uma imagem para compartilhar na rede social"* —
// *"Algo que fique bom para compartilhar no insta tambem"* — e a pergunta que a tela tem que
// fazer: *"se vale apenas os jogos ja marcados ou se os possiveis tambem (por que o mata mata
// nao ta definido)"*.
//
// ⚠️ A LISTA COMPARTILHADA É A LISTA DA TELA, letra por letra: mesma fila (OrdemNoHorario),
// mesmos filtros, mesmas etiquetas (CategoriaNaTela.Curto, LugarDoJogo.Etiqueta). Uma segunda
// conta de "quais jogos" faria o grupo do WhatsApp receber uma grade diferente da que a pessoa
// estava olhando quando apertou o botão.
public class CompartilharListaDeJogosTests
{
    private static readonly DateTime Sexta = new(2026, 9, 11, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 9, 12, 9, 0, 0);

    private static Jogador J(string nome, string? apelido = null) => new() { Nome = nome, Apelido = apelido, Cpf = Guid.NewGuid().ToString("N")[..11] };

    private static Dupla D(Jogador j1, Jogador? j2) => new() { Jogador1 = j1, Jogador2 = j2, Jogador1Id = 1, Jogador2Id = j2 == null ? null : 2 };

    private static Partida P(int id, DateTime? quando, string fase, Dupla d1, Dupla d2, string categoria = "6ª Categoria Masculina") => new()
    {
        Id = id,
        HorarioPrevisto = quando,
        Fase = fase,
        Dupla1 = d1,
        Dupla2 = d2,
        Status = "Agendada",
        Categoria = new Categoria { Nome = categoria },
    };

    private static JogoQueVem Previa(DateTime? quando, string fase = "Quartas de Final", int numero = 1) =>
        new("5ª Categoria Masculina", fase, numero, quando,
            new Lado("1º Grupo A"), new Lado("2º Grupo B"), CategoriaId: 7);

    // ── A lista: o que a tela mostra, no formato que texto e arte leem ────────────────────

    [Fact]
    public void A_lista_espelha_a_fila_da_tela_com_as_mesmas_etiquetas()
    {
        var jogo = P(1, Sexta, "Grupo D",
            D(J("Pedro Kirchner"), J("Carlos Morais")),
            D(J("Evandro Cunha"), J("Marcos Silva")));
        var fila = OrdemNoHorario.Ordenar(new[] { jogo }, new[] { Previa(Sabado) });

        var lista = ListaDeJogos.Montar(fila, sedes: null, comPrevias: true);

        Assert.Equal(2, lista.Count);

        var real = lista[0];
        Assert.Equal(Sexta, real.Horario);
        Assert.Equal("6ª Masculina", real.Categoria);          // CategoriaNaTela.Curto, como a linha
        Assert.Equal("Grupo D", real.Fase);
        Assert.Equal("Pedro Kirchner / Carlos Morais", real.Lado1);   // como a tela escreve
        Assert.Equal("Evandro Cunha / Marcos Silva", real.Lado2);
        Assert.Equal("Pedro / Carlos", real.Lado1Curto);              // como os cards escrevem
        Assert.Equal("Evandro / Marcos", real.Lado2Curto);
        Assert.False(real.Previa);

        var previa = lista[1];
        Assert.True(previa.Previa);
        Assert.Equal("5ª Masculina", previa.Categoria);
        Assert.Equal("Quartas de Final 1", previa.Fase);       // numerada, como a linha da prévia
        Assert.Equal("1º Grupo A", previa.Lado1);
        Assert.Equal("2º Grupo B", previa.Lado2Curto);
    }

    // 🗣️ "se vale apenas os jogos ja marcados ou se os possiveis tambem" — a resposta é da
    // pessoa, e o padrão é SÓ OS MARCADOS: prévia de todas as categorias no grupo de um time
    // é ruído; quem quer a grade inteira liga.
    [Fact]
    public void Sem_previas_a_lista_so_tem_jogo_marcado()
    {
        var jogo = P(1, Sexta, "Grupo D", D(J("Ana Souza"), J("Bia Lima")), D(J("Carla Reis"), J("Dani Alves")));
        var fila = OrdemNoHorario.Ordenar(new[] { jogo }, new[] { Previa(Sabado) });

        var lista = ListaDeJogos.Montar(fila, sedes: null, comPrevias: false);

        Assert.Single(lista);
        Assert.False(lista[0].Previa);
    }

    // A vaga em aberto (inscrição sozinha) diz "parceiro", igual à linha da tela — "Pedro / ?"
    // parecia dado corrompido, e "Pedro" sozinho parece jogo de simples.
    [Fact]
    public void Dupla_sem_parceiro_diz_parceiro_nas_duas_formas()
    {
        var jogo = P(1, Sexta, "Grupo A", D(J("Pedro Kirchner"), null), D(J("Ana Souza"), J("Bia Lima")));

        var lista = ListaDeJogos.Montar(OrdemNoHorario.Ordenar(new[] { jogo }, Array.Empty<JogoQueVem>()), null, false);

        Assert.Equal("Pedro Kirchner / parceiro", lista[0].Lado1);
        Assert.Equal("Pedro / parceiro", lista[0].Lado1Curto);
    }

    [Fact]
    public void Time_sai_pelo_nome_do_time()
    {
        var time = new Dupla { NomeTime = "Los Corneteiros", Jogador1 = J("Organizador"), Jogador1Id = 1 };
        var jogo = P(1, Sexta, "Rodada 1", time, D(J("Ana Souza"), J("Bia Lima")));

        var lista = ListaDeJogos.Montar(OrdemNoHorario.Ordenar(new[] { jogo }, Array.Empty<JogoQueVem>()), null, false);

        Assert.Equal("Los Corneteiros", lista[0].Lado1);
        Assert.Equal("Los Corneteiros", lista[0].Lado1Curto);
    }

    // ── As artes: uma por dia, até oito jogos cada ─────────────────────────────────────────

    private static JogoDaLista Jogo(DateTime? quando, int i, bool previa = false) => new(
        quando, "6ª Masculina", previa ? $"Quartas de Final {i}" : "Grupo A", "Er Padel",
        previa ? "1º Grupo A" : $"Jogador {i} da Silva / Parceiro {i} Souza",
        previa ? "2º Grupo B" : $"Rival {i} Pereira / Colega {i} Antunes",
        previa ? "1º Grupo A" : $"Jogador{i} / Parceiro{i}",
        previa ? "2º Grupo B" : $"Rival{i} / Colega{i}",
        previa);

    // ⚠️ AS PARTES SAEM EQUILIBRADAS, e não "oito e o resto": 9 jogos em 8 + 1 deixava a segunda
    // arte com uma linha perdida no meio do card — visto na prévia, não no teste. 11 vira 6 + 5.
    [Fact]
    public void Divide_uma_arte_por_dia_e_dia_cheio_em_partes_equilibradas()
    {
        // De 20 em 20 minutos: 11 jogos de 50 em 50 a partir das 18h atravessam a meia-noite, e o
        // "dia" vira dois — foi o primeiro vermelho deste teste, no dado e não no código.
        var jogos = Enumerable.Range(1, 11).Select(i => Jogo(Sexta.AddMinutes(i * 20), i))
            .Concat(Enumerable.Range(1, 3).Select(i => Jogo(Sabado.AddMinutes(i * 20), i)))
            .ToList();

        var artes = CartaoDosJogos.Dividir(jogos);

        Assert.Equal(3, artes.Count);
        Assert.Equal((Sexta.Date, 1, 2, 6), (artes[0].Dia, artes[0].Parte, artes[0].Partes, artes[0].Jogos.Count));
        Assert.Equal((Sexta.Date, 2, 2, 5), (artes[1].Dia, artes[1].Parte, artes[1].Partes, artes[1].Jogos.Count));
        Assert.Equal((Sabado.Date, 1, 1, 3), (artes[2].Dia, artes[2].Parte, artes[2].Partes, artes[2].Jogos.Count));

        // A ordem dentro da arte é a da fila — a arte 2 continua de onde a 1 parou.
        Assert.Equal("Jogador7 / Parceiro7", artes[1].Jogos[0].Lado1Curto);
    }

    [Fact]
    public void Nove_jogos_viram_cinco_e_quatro_e_dezesseis_viram_oito_e_oito()
    {
        var nove = CartaoDosJogos.Dividir(Enumerable.Range(1, 9).Select(i => Jogo(Sexta.AddMinutes(i), i)).ToList());
        Assert.Equal(new[] { 5, 4 }, nove.Select(a => a.Jogos.Count));

        var dezesseis = CartaoDosJogos.Dividir(Enumerable.Range(1, 16).Select(i => Jogo(Sexta.AddMinutes(i), i)).ToList());
        Assert.Equal(new[] { 8, 8 }, dezesseis.Select(a => a.Jogos.Count));

        var dezessete = CartaoDosJogos.Dividir(Enumerable.Range(1, 17).Select(i => Jogo(Sexta.AddMinutes(i), i)).ToList());
        Assert.Equal(new[] { 6, 6, 5 }, dezessete.Select(a => a.Jogos.Count));
    }

    // Torneio por ordem de liberação sem hora nenhuma: os jogos não têm dia, e ainda assim
    // são a lista. Vão numa arte só, "sem data", depois dos que têm.
    [Fact]
    public void Jogo_sem_horario_vai_pra_arte_sem_data_no_fim()
    {
        var jogos = new List<JogoDaLista> { Jogo(Sexta, 1), Jogo(null, 2), Jogo(null, 3) };

        var artes = CartaoDosJogos.Dividir(jogos);

        Assert.Equal(2, artes.Count);
        Assert.Null(artes[1].Dia);
        Assert.Equal(2, artes[1].Jogos.Count);
        Assert.Equal("sem data", artes[1].Rotulo);
        Assert.Equal("sex 11/09", artes[0].Rotulo);
    }

    [Fact]
    public void Arte_em_partes_diz_qual_parte_e()
    {
        var jogos = Enumerable.Range(1, 9).Select(i => Jogo(Sexta.AddMinutes(i), i)).ToList();

        var artes = CartaoDosJogos.Dividir(jogos);

        Assert.Equal("sex 11/09 (1 de 2)", artes[0].Rotulo);
        Assert.Equal("sex 11/09 (2 de 2)", artes[1].Rotulo);
    }

    [Fact]
    public void Lista_vazia_nao_tem_arte()
    {
        Assert.Empty(CartaoDosJogos.Dividir(new List<JogoDaLista>()));
    }

    // ── O texto do WhatsApp ────────────────────────────────────────────────────────────────

    [Fact]
    public void O_texto_tem_o_dia_os_jogos_a_previa_e_o_link()
    {
        var jogos = new List<JogoDaLista>
        {
            Jogo(Sexta, 1),
            Jogo(Sabado, 1, previa: true),
        };

        var texto = TextoDaLista.Montar("2ª Etapa ER PADEL TOUR", "Los Corneteiros", jogos,
            "https://padelizou.com.br/Torneios/Jogos/26?timeFiltroId=10");

        var linhas = texto.Split('\n');
        Assert.Equal("*2ª Etapa ER PADEL TOUR* — Los Corneteiros", linhas[0]);
        Assert.Contains("*sex 11/09*", linhas);
        Assert.Contains("18:00 · 6ª Masculina · Grupo A · Er Padel", linhas);
        Assert.Contains("Jogador 1 da Silva / Parceiro 1 Souza x Rival 1 Pereira / Colega 1 Antunes", linhas);
        Assert.Contains("*sáb 12/09*", linhas);
        // A prévia diz que é prévia: mandar "1º Grupo A x 2º Grupo B" sem aviso faz parecer
        // que o sistema não sabe quem joga.
        Assert.Contains("09:00 · 6ª Masculina · Quartas de Final 1 · Er Padel · prévia", linhas);
        Assert.EndsWith("https://padelizou.com.br/Torneios/Jogos/26?timeFiltroId=10", texto);

        // O dia aparece UMA vez, como cabeçalho — não em cada linha.
        Assert.Equal(1, linhas.Count(l => l == "*sex 11/09*"));
    }

    [Fact]
    public void Sem_recorte_o_titulo_e_so_o_torneio()
    {
        var texto = TextoDaLista.Montar("Torneio de Teste", null, new List<JogoDaLista> { Jogo(Sexta, 1) }, "https://x");

        Assert.StartsWith("*Torneio de Teste*\n", texto);
    }

    // Torneio por ordem: a linha diz "por ordem" no lugar da hora, como a tela.
    [Fact]
    public void Jogo_sem_horario_diz_por_ordem()
    {
        var texto = TextoDaLista.Montar("T", null, new List<JogoDaLista> { Jogo(null, 1) }, "https://x");

        Assert.Contains("por ordem · 6ª Masculina · Grupo A · Er Padel", texto.Split('\n'));
    }

    // ── O recorte: a frase que diz quais filtros estavam ligados ──────────────────────────

    [Fact]
    public void O_recorte_descreve_os_filtros_ligados()
    {
        var frase = RecorteDaLista.Descrever(meusJogos: false, time: "Los Corneteiros",
            categorias: new[] { "3ª Categoria Feminina" }, clube: "Er Padel", quadra: null, fase: "Quartas de Final");

        Assert.Equal("Los Corneteiros · 3ª Feminina · Er Padel · Quartas de Final", frase);
    }

    [Fact]
    public void Sem_filtro_nao_ha_recorte()
    {
        Assert.Null(RecorteDaLista.Descrever(false, null, Array.Empty<string>(), null, null, null));
    }

    [Fact]
    public void Meus_jogos_e_fase_de_grupos_saem_com_o_nome_da_tela()
    {
        var frase = RecorteDaLista.Descrever(meusJogos: true, time: null, categorias: Array.Empty<string>(),
            clube: null, quadra: "Quadra 1", fase: FasesTorneio.FaseDeGrupos);

        Assert.Equal("meus jogos · Quadra 1 · fase de grupos", frase);
    }

    // ── O desenho ──────────────────────────────────────────────────────────────────────────

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

    private static FonteDoCartao Fontes() => new(PastaDasFontes());
    private static string WebRoot() => Path.GetDirectoryName(PastaDasFontes())!;

    private static int PixelsClaros(byte[] png)
    {
        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem.Width);
        Assert.Equal(CartaoCompartilhavel.Altura, imagem.Height);

        int claros = 0;
        for (int x = 0; x < imagem.Width; x += 2)
            for (int y = 0; y < imagem.Height; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 200 && c.Green > 200 && c.Blue > 200) claros++;
            }
        return claros;
    }

    private static GradeDesenhavel Grade(List<JogoDaLista> jogos, string? recorte = "Los Corneteiros") =>
        new("2ª Etapa ER PADEL TOUR", recorte, "Er Padel", CartaoDosJogos.Dividir(jogos));

    // ⚠️ A Poppins não tem fallback (ver FonteDoCartao): todo caractere que o card escreve
    // precisa ter glifo, senão sai um espaço em branco calado. O "x" do confronto, o "·" das
    // etiquetas, o "ª" das categorias e o "Á" de "SÁB" são os que este card acrescenta.
    [Fact]
    public void A_fonte_tem_os_glifos_que_o_card_escreve()
    {
        var fontes = Fontes();
        Assert.True(fontes.Disponivel);

        using var media = new SKFont(fontes.Media);
        using var forte = new SKFont(fontes.Forte);
        foreach (var texto in new[] { "x", "·", "ª", "Á", "º", "/" })
        {
            Assert.True(media.ContainsGlyphs(texto), $"A Poppins SemiBold não tem '{texto}'.");
            Assert.True(forte.ContainsGlyphs(texto), $"A Poppins Bold não tem '{texto}'.");
        }
    }

    [Fact]
    public void O_card_dos_jogos_desenha_letra_de_verdade()
    {
        var grade = Grade(Enumerable.Range(1, 4).Select(i => Jogo(Sexta.AddMinutes(i * 50), i)).ToList());

        var png = CartaoDosJogos.Desenhar(grade, grade.Artes[0], Fontes(), WebRoot());

        Assert.True(PixelsClaros(png) > 500);
    }

    [Fact]
    public void Card_cheio_com_previa_e_sem_recorte_ainda_desenha()
    {
        var jogos = Enumerable.Range(1, CartaoDosJogos.MaximoDeJogos)
            .Select(i => Jogo(Sexta.AddMinutes(i * 20), i, previa: i % 3 == 0))
            .ToList();
        var grade = Grade(jogos, recorte: null);

        var png = CartaoDosJogos.Desenhar(grade, grade.Artes[0], Fontes(), WebRoot());

        Assert.True(PixelsClaros(png) > 500);
    }

    [Fact]
    public void Arte_sem_data_desenha()
    {
        var grade = Grade(new List<JogoDaLista> { Jogo(null, 1), Jogo(null, 2) });

        var png = CartaoDosJogos.Desenhar(grade, grade.Artes[0], Fontes(), WebRoot());

        Assert.True(PixelsClaros(png) > 300);
    }

    // ── O controller: a mesma porta da aba Jogos ──────────────────────────────────────────

    // 🗣️ "nao deixe q nada vaze sem ser publicado" (Felipe, 09/09/2026). A aba esvazia a lista
    // pra quem não organiza enquanto a chave espera aprovação — e a arte e o texto saem da
    // MESMA lista, então saem vazios também. Aqui é 404: não há o que compartilhar.
    [Fact]
    public async Task Chave_em_aprovacao_nao_vaza_pela_arte_nem_pelo_texto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);

        var deFora = TestInfra.NovoTorneiosController(ctx, intruso.Id);

        Assert.IsType<NotFoundResult>(await deFora.JogosImagem(torneio.Id, Fontes()));
        Assert.IsType<NotFoundResult>(await deFora.CompartilharJogos(torneio.Id, Fontes()));
    }

    [Fact]
    public async Task Organizador_compartilha_mesmo_com_a_chave_em_aprovacao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var tela = Assert.IsType<ViewResult>(await controller.CompartilharJogos(torneio.Id, Fontes()));
        var vm = Assert.IsType<CompartilharJogosVM>(tela.Model);
        Assert.True(vm.Grade.TemOQueMostrar);
        Assert.NotEmpty(vm.Jogos);
        Assert.Contains("*Torneio de Teste*", vm.Texto);

        var imagem = Assert.IsType<FileContentResult>(await controller.JogosImagem(torneio.Id, Fontes()));
        Assert.Equal("image/png", imagem.ContentType);
        // Família de DIVULGAÇÃO: é a prévia do link no grupo que vive deste cache.
        Assert.Equal("public, max-age=3600", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Parte_que_nao_existe_da_404()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        Assert.IsType<NotFoundResult>(await controller.JogosImagem(torneio.Id, Fontes(), parte: 99));
        Assert.IsType<NotFoundResult>(await controller.JogosImagem(torneio.Id, Fontes(), parte: 0));
    }

    // Mesma porta do Details e do Jogos: torneio oculto conta o torneio inteiro pra quem não
    // deveria nem saber que ele existe.
    [Fact]
    public async Task Torneio_oculto_nao_compartilha_pra_quem_nao_e_de_dentro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);
        torneio.Oculto = true;
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var deFora = TestInfra.NovoTorneiosController(ctx, intruso.Id);

        Assert.IsType<NotFoundResult>(await deFora.JogosImagem(torneio.Id, Fontes()));
        Assert.IsType<NotFoundResult>(await deFora.CompartilharJogos(torneio.Id, Fontes()));
    }

    // Sem a Poppins o card sairia mudo (ver FonteDoCartao) — 404, e não imagem em branco.
    [Fact]
    public async Task Sem_fonte_a_imagem_nao_existe()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        var vazia = Path.Combine(Path.GetTempPath(), "sem-fontes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vazia);

        Assert.IsType<NotFoundResult>(await controller.JogosImagem(torneio.Id, new FonteDoCartao(vazia)));
    }

    // ── A tela ─────────────────────────────────────────────────────────────────────────────

    // O botão mora no FIM da lista de agendados (é onde o pedido o desenhou) e leva os filtros
    // da tela junto — sem eles, "compartilhar esta lista" compartilharia outra.
    [Fact]
    public void A_aba_de_jogos_tem_o_botao_no_fim_da_lista_com_os_filtros()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

        var lista = fonte.IndexOf("class=\"pdz-jogos-lista\"", StringComparison.Ordinal);
        var botao = fonte.IndexOf("Url.Action(\"CompartilharJogos\"", StringComparison.Ordinal);
        Assert.True(botao > lista, "O botão de compartilhar tem que vir DEPOIS da lista de agendados.");

        var trecho = fonte[botao..fonte.IndexOf(")", botao, StringComparison.Ordinal)];
        foreach (var filtro in new[] { "timeFiltroId", "categoriaFiltroIds", "soMeusJogos", "clubeFiltroId", "quadraFiltro", "faseFiltro" })
            Assert.Contains(filtro, trecho);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta Padelizou/Views a partir de " + AppContext.BaseDirectory);
    }
}
