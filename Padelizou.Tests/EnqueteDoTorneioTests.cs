using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A ENQUETE PÓS-TORNEIO: quem jogou dá nota pro clube, pra organização e pro sistema, na mesma
// janela de 7 dias do MVP. Existe por causa de 2027 — o "Melhor Clube do ano" precisa de um ano
// de coleta.
//
// O que estes testes guardam:
//   · a janela é a MESMA do MVP, mas o interruptor UsaVotacaoDeMvp NÃO manda aqui;
//   · quem responde é quem JOGOU (lista de espera e sem-parceiro ficam de fora);
//   · responder de novo TROCA a resposta, nunca soma outra;
//   · a média só existe com 3+ respostas — antes disso é uma pessoa com voz de consenso;
//   · o comentário ASSINADO vai pro mural na hora e o ANÔNIMO não vai sozinho — e, quando vai,
//     vai sem o nome. É a promessa feita na tela de resposta, e é o que estes testes prendem.
public class EnqueteDoTorneioTests
{
    private static readonly DateTime Domingo = new(2026, 8, 9, 20, 0, 0);

    // Um torneio finalizado com o último jogo AGORA: janela aberta.
    private static async Task<(Torneio torneio, List<Dupla> duplas)> MontarAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();
        duplas[0].UltimaFase = "Campeao";
        duplas[1].UltimaFase = "Final";

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, VencedorId = duplas[0].Id,
            Status = "Finalizada", HorarioFimReal = Domingo, Fase = "Final", Codigo = "P1",
        });
        await ctx.SaveChangesAsync();
        return (torneio, duplas);
    }

    // As notas sozinhas — o caminho de quem responde em cinco segundos, sem escrever nada.
    private static RespostaDaEnquete So(int clube, int organizacao, int? sistema = null) =>
        new() { NotaClube = clube, NotaOrganizacao = organizacao, NotaSistema = sistema };

    [Fact]
    public async Task Quem_jogou_avalia_e_a_resposta_fica_gravada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, So(5, 4, sistema: 5), Domingo.AddHours(2));

        Assert.Null(recusa);
        var gravada = Assert.Single(ctx.AvaliacoesDeTorneio);
        Assert.Equal(5, gravada.NotaClube);
        Assert.Equal(4, gravada.NotaOrganizacao);
        Assert.Equal(5, gravada.NotaSistema);
    }

    [Fact]
    public async Task Responder_de_novo_TROCA_a_resposta_em_vez_de_somar_outra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var quem = duplas[1].Jogador1Id;

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, So(5, 5), Domingo.AddHours(1));
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, So(2, 3), Domingo.AddHours(3));

        var unica = Assert.Single(ctx.AvaliacoesDeTorneio);
        Assert.Equal(2, unica.NotaClube);
        Assert.Equal(3, unica.NotaOrganizacao);
        Assert.NotNull(unica.AtualizadoEm);
    }

    [Fact]
    public async Task So_quem_JOGOU_responde()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await MontarAsync(ctx);

        var deFora = new Jogador { Nome = "So Assistiu", Cpf = "55500000010" };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, deFora.Id, So(5, 5), Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task Fora_da_janela_de_7_dias_a_enquete_recusa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, So(4, 4),
            Domingo.AddDays(MvpDoTorneio.DiasParaVotar));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task O_interruptor_do_MVP_nao_manda_na_enquete()
    {
        // O organizador desligou a DISPUTA entre jogadores — a coleta sobre o clube é nossa,
        // e continua valendo. Sem isso, o dado do "Melhor Clube 2027" nasceria com buraco
        // exatamente nos torneios de confraternização, que são clube como qualquer outro.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        ctx.Torneios.First(t => t.Id == torneio.Id).UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, So(4, 5), Domingo.AddHours(2));

        Assert.Null(recusa);
        Assert.Single(ctx.AvaliacoesDeTorneio);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(6, 5)]
    [InlineData(5, 0)]
    public async Task Nota_fora_de_1_a_5_recusa(int clube, int organizacao)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, So(clube, organizacao), Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task Nota_do_sistema_e_OPCIONAL_mas_zero_nao_passa()
    {
        // Nula é a resposta de quem avaliou antes de a pergunta existir — e é o que um
        // formulário sem a nota manda. Zero é outra coisa: é valor fora da escala chegando
        // como se fosse resposta, e ele derrubaria a média do Padelizou sem ninguém ter dado
        // nota baixa.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        Assert.Null(await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, So(4, 4, sistema: null), Domingo.AddHours(1)));
        Assert.Null(Assert.Single(ctx.AvaliacoesDeTorneio).NotaSistema);

        Assert.NotNull(await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, So(4, 4, sistema: 0), Domingo.AddHours(2)));
    }

    [Fact]
    public async Task A_media_so_aparece_com_3_respostas_e_sai_certa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        // Os 4 que jogaram: 3 respondem.
        var jogadores = new[]
        {
            duplas[0].Jogador1Id, duplas[0].Jogador2Id!.Value, duplas[1].Jogador1Id,
        };
        var notas = new[] { (5, 4), (4, 4), (3, 1) };

        for (int i = 0; i < 2; i++)
            await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[i],
                So(notas[i].Item1, notas[i].Item2), Domingo.AddHours(1));

        // Com 2 respostas: contagem sim, média não.
        var cedo = await EnqueteDoTorneio.ResumoAsync(ctx, torneio.Id);
        Assert.Equal(2, cedo.Respostas);
        Assert.False(cedo.TemMedia);

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[2],
            So(notas[2].Item1, notas[2].Item2), Domingo.AddHours(1));

        var resumo = await EnqueteDoTorneio.ResumoAsync(ctx, torneio.Id);
        Assert.Equal(3, resumo.Respostas);
        Assert.True(resumo.TemMedia);
        Assert.Equal(4.0, resumo.MediaClube);          // (5+4+3)/3
        Assert.Equal(3.0, resumo.MediaOrganizacao);    // (4+4+1)/3
    }

    [Fact]
    public async Task A_media_do_SISTEMA_tem_contagem_propria()
    {
        // Três respostas destravam a média do clube, mas se só duas delas deram nota ao
        // Padelizou a média do sistema continua escondida. Contar as três aqui seria fazer
        // média de duas notas com voz de três — e quem respondeu antes de a pergunta existir
        // não disse nada sobre o sistema.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var jogadores = new[]
        {
            duplas[0].Jogador1Id, duplas[0].Jogador2Id!.Value, duplas[1].Jogador1Id,
        };
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[0], So(5, 5, sistema: 5), Domingo.AddHours(1));
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[1], So(5, 5, sistema: 3), Domingo.AddHours(1));
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[2], So(5, 5), Domingo.AddHours(1));

        var resumo = await EnqueteDoTorneio.ResumoAsync(ctx, torneio.Id);
        Assert.True(resumo.TemMedia);
        Assert.Equal(2, resumo.RespostasSobreOSistema);
        Assert.Null(resumo.MediaSistema);

        // A terceira nota AO SISTEMA destrava.
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[2], So(5, 5, sistema: 4), Domingo.AddHours(2));
        var depois = await EnqueteDoTorneio.ResumoAsync(ctx, torneio.Id);
        Assert.Equal(4.0, depois.MediaSistema);        // (5+3+4)/3
    }

    [Fact]
    public async Task Quem_ficou_na_lista_de_espera_nao_avalia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await MontarAsync(ctx);
        var categoria = ctx.Categorias.First(c => c.TorneioId == torneio.Id);

        var esperou = new Jogador { Nome = "Quem Esperou", Cpf = "55500000011" };
        ctx.Jogadores.Add(esperou);
        await ctx.SaveChangesAsync();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = esperou.Id, Jogador2Id = esperou.Id,
            EmListaDeEspera = true,
        });
        await ctx.SaveChangesAsync();

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, esperou.Id, So(5, 5), Domingo.AddHours(1));

        Assert.NotNull(recusa);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════
    // OS COMENTÁRIOS: assinado × sem identificação
    // ═════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Comentario_ASSINADO_entra_no_mural_na_hora_e_com_o_nome()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, duplas[1].Jogador1Id, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5,
            ComentarioClube = "Quadras impecáveis.",
            Anonimo = false,
        }, Domingo.AddHours(1));

        var publicados = await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id);
        var unico = Assert.Single(publicados);
        Assert.Equal("Quadras impecáveis.", unico.SobreOClube);
        Assert.NotNull(unico.Autor);
        Assert.False(unico.Anonimo);
    }

    [Fact]
    public async Task Comentario_SEM_IDENTIFICACAO_nao_entra_no_mural_sozinho()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, duplas[1].Jogador1Id, new RespostaDaEnquete
        {
            NotaClube = 2, NotaOrganizacao = 3,
            ComentarioClube = "O vestiário estava sujo.",
            Anonimo = true,
        }, Domingo.AddHours(1));

        Assert.Empty(await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id));

        // Mas chega a quem recebe — e sem o nome, que é o combinado da tela de resposta.
        var paraQuemOrganiza = Assert.Single(await EnqueteDoTorneio.ParaModerarAsync(ctx, torneio.Id));
        Assert.Equal("O vestiário estava sujo.", paraQuemOrganiza.SobreOClube);
        Assert.True(paraQuemOrganiza.Anonimo);
        Assert.Null(paraQuemOrganiza.Autor);
        Assert.Null(paraQuemOrganiza.AutorId);
        Assert.Null(paraQuemOrganiza.FotoDoAutor);
    }

    [Fact]
    public async Task Publicado_pelo_organizador_o_anonimo_sai_SEM_o_nome()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var organizador = ctx.TorneioOrganizadores.First(o => o.TorneioId == torneio.Id).JogadorId;

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, duplas[1].Jogador1Id, new RespostaDaEnquete
        {
            NotaClube = 4, NotaOrganizacao = 4,
            ComentarioOrganizacao = "Chave saiu no horário.",
            Anonimo = true,
        }, Domingo.AddHours(1));

        var avaliacaoId = ctx.AvaliacoesDeTorneio.First().Id;
        var recusa = await EnqueteDoTorneio.PublicarAsync(
            ctx, avaliacaoId, organizador, publicar: true, Domingo.AddHours(2));

        Assert.Null(recusa);
        var noMural = Assert.Single(await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id));
        Assert.Equal("Chave saiu no horário.", noMural.SobreAOrganizacao);
        Assert.Null(noMural.Autor);
    }

    [Fact]
    public async Task Quem_nao_organiza_nem_cuida_do_clube_NAO_publica()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, duplas[1].Jogador1Id, new RespostaDaEnquete
        {
            NotaClube = 4, NotaOrganizacao = 4, ComentarioClube = "Bom clube.", Anonimo = true,
        }, Domingo.AddHours(1));

        // Outro jogador do mesmo torneio: jogou, mas não manda no que é publicado.
        var recusa = await EnqueteDoTorneio.PublicarAsync(
            ctx, ctx.AvaliacoesDeTorneio.First().Id, duplas[0].Jogador1Id, true, Domingo.AddHours(2));

        Assert.NotNull(recusa);
        Assert.Empty(await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id));
    }

    [Fact]
    public async Task O_dono_do_clube_tambem_publica()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var dono = new Jogador { Nome = "Dono do Clube", Cpf = "55500000012" };
        ctx.Jogadores.Add(dono);
        await ctx.SaveChangesAsync();
        var clube = new Clube { Nome = "Clube da Esquina", DonoId = dono.Id };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();
        ctx.Torneios.First(t => t.Id == torneio.Id).ClubeId = clube.Id;
        await ctx.SaveChangesAsync();

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, duplas[1].Jogador1Id, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, ComentarioClube = "Quadra nova, ótima.", Anonimo = true,
        }, Domingo.AddHours(1));

        var recusa = await EnqueteDoTorneio.PublicarAsync(
            ctx, ctx.AvaliacoesDeTorneio.First().Id, dono.Id, true, Domingo.AddHours(2));

        Assert.Null(recusa);

        // E ele lê o que disseram do clube dele, atravessando os torneios da casa.
        var doClube = Assert.Single(await EnqueteDoTorneio.DoClubeAsync(ctx, clube.Id));
        Assert.Equal("Quadra nova, ótima.", doClube.SobreOClube);
        Assert.Null(doClube.Autor);
    }

    [Fact]
    public async Task Trocar_de_assinado_para_anonimo_TIRA_do_mural()
    {
        // A pessoa voltou atrás. O texto dela não pode seguir exposto com o nome só porque já
        // estava lá — quem escolhe é quem escreveu.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var quem = duplas[1].Jogador1Id;

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 3, NotaOrganizacao = 3, ComentarioClube = "Faltou água na quadra 2.",
        }, Domingo.AddHours(1));
        Assert.Single(await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id));

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 3, NotaOrganizacao = 3, ComentarioClube = "Faltou água na quadra 2.",
            Anonimo = true,
        }, Domingo.AddHours(2));

        Assert.Empty(await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id));
    }

    [Fact]
    public async Task O_que_o_moderador_tirou_do_mural_nao_volta_quando_a_pessoa_reenvia()
    {
        // Sem esta trava, editar uma vírgula seria o jeito de furar a moderação: o comentário
        // assinado voltaria pro mural sozinho, e quem tirou não saberia.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var quem = duplas[1].Jogador1Id;
        var organizador = ctx.TorneioOrganizadores.First(o => o.TorneioId == torneio.Id).JogadorId;

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 1, NotaOrganizacao = 1, ComentarioClube = "Texto que o organizador tirou.",
        }, Domingo.AddHours(1));

        await EnqueteDoTorneio.PublicarAsync(
            ctx, ctx.AvaliacoesDeTorneio.First().Id, organizador, publicar: false, Domingo.AddHours(2));
        Assert.Empty(await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id));

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 1, NotaOrganizacao = 1, ComentarioClube = "Texto que o organizador tirou!",
        }, Domingo.AddHours(3));

        Assert.Empty(await EnqueteDoTorneio.PublicadosAsync(ctx, torneio.Id));
    }

    [Fact]
    public async Task Palavrao_barra_o_comentario_publico_e_passa_no_anonimo()
    {
        // A assimetria é o desenho: o público é mural, o anônimo é conversa privada com quem
        // organiza — e recusar ali seria calar justamente a crítica que a enquete existe pra
        // ouvir. Ela só alcança outras pessoas se um humano publicar, e aí o humano leu.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var quem = duplas[1].Jogador1Id;

        var recusa = await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 1, NotaOrganizacao = 1, ComentarioClube = "A quadra estava uma bosta.",
        }, Domingo.AddHours(1));
        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);

        var anonimo = await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 1, NotaOrganizacao = 1, ComentarioClube = "A quadra estava uma bosta.",
            Anonimo = true,
        }, Domingo.AddHours(1));
        Assert.Null(anonimo);
        Assert.Single(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task Comentario_maior_que_o_limite_recusa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var recusa = await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, duplas[1].Jogador1Id,
            new RespostaDaEnquete
            {
                NotaClube = 5, NotaOrganizacao = 5,
                ComentarioOrganizacao = new string('a', EnqueteDoTorneio.TamanhoMaximoDoComentario + 1),
            }, Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════
    // O TEXTO SOBRE O PADELIZOU: vai pra caixa de feedback, não pra avaliação
    // ═════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task O_texto_sobre_o_sistema_vira_feedback_do_site_invisivel_e_SEM_nota()
    {
        // Sem nota de propósito: lá a escala é 0-10 (NPS) e aqui é 1-5 estrelas. Converter
        // inventaria opinião — 3 estrelas, que é o meio, viraria "6, detrator".
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, duplas[1].Jogador1Id, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, NotaSistema = 3,
            ComentarioSistema = "O aviso da chave chegou tarde.",
            Anonimo = true,
        }, Domingo.AddHours(1));

        var feedback = Assert.Single(ctx.FeedbacksSite);
        Assert.Equal("O aviso da chave chegou tarde.", feedback.Texto);
        Assert.Equal(torneio.Id, feedback.TorneioId);
        Assert.Null(feedback.Nota);
        Assert.False(feedback.Exibir);      // nasce invisível, como todo feedback
        Assert.True(feedback.Anonimo);

        // E a nota do sistema fica na avaliação, na escala em que foi dada.
        Assert.Equal(3, Assert.Single(ctx.AvaliacoesDeTorneio).NotaSistema);
    }

    [Fact]
    public async Task Reenviar_TROCA_o_texto_do_sistema_em_vez_de_somar_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var quem = duplas[1].Jogador1Id;

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, ComentarioSistema = "Primeira versão.",
        }, Domingo.AddHours(1));
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, ComentarioSistema = "Segunda versão.",
        }, Domingo.AddHours(2));

        Assert.Equal("Segunda versão.", Assert.Single(ctx.FeedbacksSite).Texto);
    }

    [Fact]
    public async Task Apagar_o_texto_do_sistema_some_com_ele_a_menos_que_ja_esteja_no_ar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var quem = duplas[1].Jogador1Id;

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, ComentarioSistema = "Escrevi sem querer.",
        }, Domingo.AddHours(1));
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, ComentarioSistema = "   ",
        }, Domingo.AddHours(2));

        Assert.Empty(ctx.FeedbacksSite);

        // Mas o que o admin já publicou na home não some em silêncio.
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, ComentarioSistema = "Adorei o app.",
        }, Domingo.AddHours(3));
        ctx.FeedbacksSite.First().Exibir = true;
        await ctx.SaveChangesAsync();

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, new RespostaDaEnquete
        {
            NotaClube = 5, NotaOrganizacao = 5, ComentarioSistema = null,
        }, Domingo.AddHours(4));

        Assert.Single(ctx.FeedbacksSite);
    }
}
