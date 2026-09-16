using System.IO;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — O ORGANIZADOR DECIDE SE O TORNEIO TEM PALPITÔMETRO, E EM QUAIS CATEGORIAS.
//
// 🗣️ Felipe: *"na parte de criar torneio, coloque la para o organizador decidir se vai habilitar
// o palpitometro ou nao, se vai ser apenas da masculina/feminina ou em ambos, deixe nascendo
// como permitido e nas tanto feminino como masculino"*.
//
// 🔑 UMA COLUNA SÓ (`Torneio.PalpitometroEm`), E NÃO UM `bool` MAIS UM ALCANCE. Dois campos
// podem discordar — desligado com "Feminina" gravado ao lado —, e aí passam a existir duas
// verdades sobre a mesma pergunta. É a mesma decisão que o `GamesSoDaFinal` tomou ao recusar um
// `bool FinalSeparada`, e é o que deixa a tela ser um rádio de quatro opções, igual ao
// `quemMarcaPlacar`: rádio sempre manda o marcado, sem pegadinha de campo escondido.
//
// 🔑 A RÉGUA DO SEXO É A DO PADELÍMETRO (`FaixasDePadelimetro.EhFeminina`), não uma nova: nome
// com "Fem" é feminina, todo o resto é masculina. A decisão do Felipe nas perguntas de hoje foi
// exatamente essa — Mista, Casal e Lendas entram junto com a masculina, em vez de ficarem de
// fora das duas escolhas e sem palpitômetro nenhum.
public class PalpitometroPorCategoriaTests
{
    // ─────────────────────────── A RÉGUA ───────────────────────────

    [Fact]
    public void Torneio_novo_NASCE_com_o_palpitometro_em_TODAS_as_categorias()
    {
        // 🗣️ *"deixe nascendo como permitido e nas tanto feminino como masculino"*. É o
        // comportamento que o sistema já tem hoje — o interruptor existe pra quem não quer.
        Assert.Equal(AlcanceDoPalpitometro.Todas, new Torneio().PalpitometroEm);
    }

    [Theory]
    [InlineData("3ª Categoria Feminina", true)]
    [InlineData("6ª Feminina", true)]
    [InlineData("Open Feminino", true)]
    [InlineData("3ª Categoria Masculina", false)]
    [InlineData("Open Masculino", false)]
    public void So_femininas_libera_a_feminina_e_barra_a_masculina(string categoria, bool libera)
    {
        Assert.Equal(libera, AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Feminina, categoria));
        Assert.Equal(!libera, AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Masculina, categoria));
    }

    [Theory]
    [InlineData("Mista A")]
    [InlineData("Casal")]
    [InlineData("Lendas")]
    public void Mista_Casal_e_Lendas_entram_junto_com_a_MASCULINA(string categoria)
    {
        // Decisão do Felipe (14/09/2026), perguntado: a régua é binária, a mesma do Padelímetro.
        // A alternativa — só o nome com "Masc" conta — deixaria estas três sem palpitômetro nas
        // DUAS escolhas restritas, que é justamente o buraco que ninguém percebe até o torneio
        // estar no ar.
        Assert.True(AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Masculina, categoria));
        Assert.False(AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Feminina, categoria));
    }

    [Theory]
    [InlineData("3ª Categoria Masculina")]
    [InlineData("3ª Categoria Feminina")]
    [InlineData("Mista A")]
    public void Todas_libera_qualquer_categoria_e_Nenhuma_nao_libera_nenhuma(string categoria)
    {
        Assert.True(AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Todas, categoria));
        Assert.False(AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Nenhuma, categoria));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lixo")]
    public void Valor_DESCONHECIDO_cai_em_TODAS_e_nao_apaga_o_palpitometro_de_ninguem(string? gravado)
    {
        // ⚠️ O caminho por onde isto chega não é hipotético: é o backfill da migration numa
        // linha antiga, e é o POST montado à mão. Um valor que não se reconhece NÃO pode
        // significar "desligado" — seria o palpitômetro sumindo, calado, do torneio de quem
        // nunca tocou nesta tela.
        Assert.Equal(AlcanceDoPalpitometro.Todas, AlcanceDoPalpitometro.Normalizar(gravado));
        Assert.True(AlcanceDoPalpitometro.Libera(gravado, "3ª Categoria Feminina"));
        Assert.False(AlcanceDoPalpitometro.Existe(gravado));
    }

    [Fact]
    public void A_regua_do_sexo_e_a_MESMA_do_padelimetro_e_nao_uma_copia()
    {
        // Uma segunda definição de "esta categoria é feminina" é o tipo de divergência que só
        // aparece no dia em que uma das duas muda: a tela mostraria o palpitômetro onde o
        // Padelímetro diz que é feminina e o alcance diz que não.
        var fonte = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Services", "AlcanceDoPalpitometro.cs")));

        Assert.Contains("FaixasDePadelimetro.EhFeminina", fonte);
    }

    // ─────────────────────── O SERVIDOR RECUSA O VOTO ───────────────────────

    [Fact]
    public async Task Fora_do_alcance_o_servidor_RECUSA_o_palpite()
    {
        // ⚠️ A trava não pode morar só na view. Esconder o bloco não fecha a porta: o POST de
        // /Partidas/Votar é montado à mão sem passar por tela nenhuma, e quem já tinha a lista
        // aberta quando o organizador desligou continua com o botão na mão.
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas, torneio) = await MontarJogoAgendadoAsync(ctx, "3ª Categoria Feminina");
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Masculina;
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "55551000001");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PalpiteService(ctx).RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id));

        Assert.Empty(ctx.PalpitesPartida.ToList());
    }

    [Fact]
    public async Task Desligado_o_palpitometro_nao_aceita_voto_em_categoria_NENHUMA()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas, torneio) = await MontarJogoAgendadoAsync(ctx, "3ª Categoria Masculina");
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Nenhuma;
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "55551000002");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PalpiteService(ctx).RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id));
    }

    [Fact]
    public async Task Dentro_do_alcance_o_palpite_passa_como_sempre()
    {
        // O controle do teste acima: uma trava que recusa tudo passaria naquele e quebraria o
        // recurso inteiro sem ninguém notar até o torneio estar em quadra.
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas, torneio) = await MontarJogoAgendadoAsync(ctx, "3ª Categoria Feminina");
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Feminina;
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "55551000003");
        var resumo = await new PalpiteService(ctx).RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id);

        Assert.Equal(duplas[0].Id, resumo.MeuVotoDuplaId);
        Assert.Equal(1, resumo.TotalVotos);
    }

    [Fact]
    public async Task Desligar_DEPOIS_nao_apaga_o_que_ja_foi_palpitado_e_ainda_deixa_RETIRAR()
    {
        // Decisão do Felipe (14/09/2026): o palpite já dado fica gravado e continua contando no
        // ranking de palpiteiros — ninguém perde ponto que já ganhou porque o organizador mudou
        // de ideia no meio do torneio.
        //
        // ⚠️ E RETIRAR CONTINUA VALENDO, de propósito: a trava é sobre gravar palpite NOVO. Se
        // ela fechasse a saída também, quem palpitou ficaria preso a uma aposta que a tela nem
        // mostra mais — e o único jeito de sair seria pedir pro organizador religar.
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas, torneio) = await MontarJogoAgendadoAsync(ctx, "3ª Categoria Feminina");
        var servico = new PalpiteService(ctx);
        var torcedor = await NovoTorcedorAsync(ctx, "55551000004");

        await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id);

        torneio.PalpitometroEm = AlcanceDoPalpitometro.Nenhuma;
        await ctx.SaveChangesAsync();

        Assert.Single(ctx.PalpitesPartida.ToList());

        var resumo = await servico.RetirarPalpiteAsync(partida.Id, torcedor.Id);
        Assert.Null(resumo.MeuVotoDuplaId);
    }

    // ─────────────────────── A ESCOLHA, NA CRIAÇÃO E NA GESTÃO ───────────────────────

    [Fact]
    public async Task A_CRIACAO_grava_o_alcance_que_o_organizador_marcou()
    {
        using var ctx = ContextoParaCriar();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var torneio = TorneioValido();
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Feminina;

        await controller.Create(torneio, new[] { 3 }, null, null, null, null);

        Assert.Equal(AlcanceDoPalpitometro.Feminina, Assert.Single(ctx.Torneios).PalpitometroEm);
    }

    [Fact]
    public async Task Na_CRIACAO_um_alcance_INVENTADO_cai_no_padrao_em_vez_de_ser_gravado()
    {
        // POST montado à mão. Texto desconhecido gravado deixaria a tela de gestão com os quatro
        // rádios apagados — e ninguém saberia o que o torneio faz até abrir a lista de jogos.
        using var ctx = ContextoParaCriar();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var torneio = TorneioValido();
        torneio.PalpitometroEm = "SoQuemEuGosto";

        await controller.Create(torneio, new[] { 3 }, null, null, null, null);

        Assert.Equal(AlcanceDoPalpitometro.Todas, Assert.Single(ctx.Torneios).PalpitometroEm);
    }

    [Fact]
    public async Task Na_GESTAO_o_organizador_muda_o_alcance_depois()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Todas;
        await ctx.SaveChangesAsync();

        await SalvarNaGestaoAsync(ctx, torneio, organizador.Id, AlcanceDoPalpitometro.Nenhuma);
        Assert.Equal(AlcanceDoPalpitometro.Nenhuma, (await ctx.Torneios.FindAsync(torneio.Id))!.PalpitometroEm);

        await SalvarNaGestaoAsync(ctx, torneio, organizador.Id, AlcanceDoPalpitometro.Masculina);
        Assert.Equal(AlcanceDoPalpitometro.Masculina, (await ctx.Torneios.FindAsync(torneio.Id))!.PalpitometroEm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SoQuemEuGosto")]
    public async Task Formulario_ANTIGO_ou_POST_torto_NAO_mexem_no_alcance_gravado(string? veioNoPost)
    {
        // ⚠️ Nulo = aba aberta antes deste deploy, que não tem o campo. Tratado como "desligar",
        // salvar qualquer outra coisa na gestão por uma aba velha tiraria o palpitômetro do
        // torneio sem ninguém pedir — a mesma lição do `usaVotacaoDeMvp`.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Feminina;
        await ctx.SaveChangesAsync();

        await SalvarNaGestaoAsync(ctx, torneio, organizador.Id, veioNoPost);

        Assert.Equal(AlcanceDoPalpitometro.Feminina, (await ctx.Torneios.FindAsync(torneio.Id))!.PalpitometroEm);
    }

    [Fact]
    public void A_proxima_edicao_do_torneio_HERDA_o_alcance()
    {
        // É configuração do torneio, como o MVP e o check-in: quem desligou o palpitômetro numa
        // etapa do circuito quer ele desligado na seguinte.
        var copia = DuplicacaoDeTorneio.CopiarConfiguracao(new Torneio
        {
            Nome = "Etapa 1",
            Codigo = "E1",
            PalpitometroEm = AlcanceDoPalpitometro.Masculina,
        });

        Assert.Equal(AlcanceDoPalpitometro.Masculina, copia.PalpitometroEm);
    }

    // ─────────────────────────── A TELA ───────────────────────────

    [Fact]
    public async Task A_lista_de_jogos_leva_o_alcance_pra_tela()
    {
        // As duas telas que desenham jogo (Details e Jogos) saem do mesmo
        // CarregarViewBagJogosAsync — é lá que o alcance entra, uma vez só.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Feminina;
        await ctx.SaveChangesAsync();

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).Jogos(torneio.Id, null, null));

        Assert.Equal(AlcanceDoPalpitometro.Feminina, view.ViewData["PalpitometroEm"]);
    }

    [Theory]
    [InlineData("_JogoEmLinha.cshtml")]
    [InlineData("_JogosDoTorneio.cshtml")]
    public void As_DUAS_apresentacoes_do_jogo_consultam_o_alcance_antes_de_desenhar(string view)
    {
        // ⚠️ SÃO DUAS, e é por isso que este teste é Theory: a lista (`_JogoEmLinha`) e o cartão
        // do AO VIVO (`_JogosDoTorneio`) têm markup PRÓPRIO, cada um com o seu palpitômetro.
        // Foi assim que o "desfazer o play" nasceu só na linha — e um palpitômetro que some da
        // lista e continua no cartão ao vivo é pior do que não ter o interruptor.
        var fonte = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", view)));

        Assert.Contains("AlcanceDoPalpitometro.Libera", fonte);
        Assert.Contains("PalpitometroEm", fonte);
    }

    [Theory]
    [InlineData("Create.cshtml")]
    [InlineData("Details.cshtml")]
    public void A_criacao_e_a_gestao_oferecem_as_QUATRO_opcoes(string view)
    {
        // Rádio de quatro, e não caixa + rádio: uma caixa "usar palpitômetro?" separada poderia
        // discordar do alcance gravado ao lado dela (ver o cabeçalho deste arquivo).
        var fonte = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", view)));

        foreach (var opcao in new[]
                 {
                     AlcanceDoPalpitometro.Nenhuma, AlcanceDoPalpitometro.Todas,
                     AlcanceDoPalpitometro.Masculina, AlcanceDoPalpitometro.Feminina,
                 })
        {
            Assert.Contains($"value=\"{opcao}\"", fonte);
        }
    }

    [Fact]
    public void A_migracao_deixa_TODO_torneio_que_JA_EXISTE_com_o_palpitometro_ligado()
    {
        // ⚠️ MESMA ARMADILHA DO `UsaVotacaoDeMvp`, e por isso o teste olha o ARQUIVO: o `= Todas`
        // da propriedade em C# vale só pra objeto NOVO criado pelo app. Quem já está gravado
        // recebe o que o `defaultValue` da migration disser, e o EF escreve string VAZIA aqui.
        // Deixando como veio, todo torneio de produção nasceria com uma coluna que nenhuma tela
        // reconhece — e o `Normalizar` até segura isso em "Todas", mas o banco ficaria com uma
        // verdade que a tela de gestão não consegue mostrar em rádio nenhum.
        var migracao = Directory.GetFiles(
            Path.Combine(RaizDoRepo(), "Padelizou", "Migrations"), "*_PalpitometroPorCategoria.cs");

        var arquivo = Assert.Single(migracao);
        var texto = File.ReadAllText(arquivo);

        Assert.Contains("PalpitometroEm", texto);
        Assert.Contains($"defaultValue: \"{AlcanceDoPalpitometro.Todas}\"", texto);
    }

    // ─────────────────────────── INFRA ───────────────────────────

    private static Torneio TorneioValido() => new()
    {
        Nome = "Copa do Palpite",
        ClubeId = 1,
        Status = "Inscrições Abertas",
        SetsFaseGrupos = 1,
        GamesFaseGrupos = 6,
        RestricaoCategoria = "Livre",
        FormaPagamento = "Externo",
    };

    private static DbPadelContext ContextoParaCriar()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 3,
            Nome = "3ª Categoria Masculina",
            Codigo = "3CatM",
            Tipo = "Masculina",
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task SalvarNaGestaoAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, string? palpitometroEm)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);
        await controller.Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: torneio.QuantidadeQuadras, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            palpitometroEm: palpitometroEm);
    }

    private static async Task<(Partida partida, List<Dupla> duplas, Torneio torneio)> MontarJogoAgendadoAsync(
        DbPadelContext ctx, string nomeDaCategoria)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.GamesFaseGrupos = 6;
        torneio.SetsFaseGrupos = 1;
        categoria.Nome = nomeDaCategoria;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = "Grupos",
            Codigo = "J1",
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();

        return (partida, duplas, torneio);
    }

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string cpf)
    {
        var torcedor = new Jogador { Nome = "Torcedor " + cpf, Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    private static string PastaDoProjeto() => Path.Combine(RaizDoRepo(), "Padelizou");

    private static string RaizDoRepo()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            if (Directory.Exists(Path.Combine(pasta, "Padelizou", "Migrations"))) return pasta;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Não achei a raiz do repositório a partir de " + AppContext.BaseDirectory);
    }
}
