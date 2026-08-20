using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O RESUMO SEMANAL de torneios e os avisos novos do CINTURÃO (20/08/2026).
public class ResumoSemanalECinturaoTests
{
    // Uma segunda-feira depois do meio-dia, pra régua do resumo.
    private static readonly DateTime SegundaAoMeioDia = new(2026, 8, 24, 12, 30, 0);

    // ── O RESUMO SEMANAL: a régua ────────────────────────────────────────────────────────

    [Fact]
    public void So_envia_na_segunda_depois_do_meio_dia()
    {
        Assert.True(ResumoSemanalDeTorneios.HoraDeEnviar(SegundaAoMeioDia, null));
        // Domingo não; segunda de manhã não.
        Assert.False(ResumoSemanalDeTorneios.HoraDeEnviar(new DateTime(2026, 8, 23, 12, 30, 0), null));
        Assert.False(ResumoSemanalDeTorneios.HoraDeEnviar(new DateTime(2026, 8, 24, 9, 0, 0), null));
    }

    // O marcador é quem impede a segunda-feira inteira de reenviar a cada meia hora — e a
    // semana seguinte de ficar muda.
    [Fact]
    public void O_marcador_segura_a_repeticao_mas_libera_a_semana_seguinte()
    {
        Assert.False(ResumoSemanalDeTorneios.HoraDeEnviar(SegundaAoMeioDia, SegundaAoMeioDia.AddHours(-1)));
        Assert.True(ResumoSemanalDeTorneios.HoraDeEnviar(SegundaAoMeioDia, SegundaAoMeioDia.AddDays(-7)));
    }

    [Fact]
    public void Semana_vazia_nao_tem_texto()
    {
        Assert.Null(ResumoSemanalDeTorneios.Texto(Array.Empty<string>()));
    }

    [Fact]
    public void Um_torneio_so_e_chamado_pelo_nome()
    {
        var texto = ResumoSemanalDeTorneios.Texto(new[] { "Copa do Sul" })!;

        Assert.Contains("Copa do Sul", texto.Corpo);
    }

    // Mais que três vira "e mais N": a notificação corta o que passa de duas linhas.
    [Fact]
    public void Muitos_torneios_viram_contagem_em_vez_de_rosario()
    {
        var texto = ResumoSemanalDeTorneios.Texto(new[] { "A", "B", "C", "D", "E" })!;

        Assert.Contains("5 torneios", texto.Titulo);
        Assert.Contains("(e mais 2)", texto.Corpo);
        Assert.DoesNotContain("D", texto.Corpo.Split('(')[0]);
    }

    // ── O RESUMO SEMANAL: a varredura ────────────────────────────────────────────────────

    [Fact]
    public async Task A_varredura_envia_uma_vez_e_o_marcador_cala_a_segunda_passada()
    {
        var ctx = TestInfra.NovoContexto();
        var push = Substitute.For<IPushNotificationService>();

        ctx.Torneios.Add(new Torneio
        {
            Id = 1, Nome = "Copa da Semana", Codigo = "CS1", Status = "Inscrições Abertas",
            AvisoDeTorneioNovoEm = SegundaAoMeioDia.AddDays(-2),
        });
        // Anunciado há dez dias não é "desta semana" — ficaria repetindo pra sempre.
        ctx.Torneios.Add(new Torneio
        {
            Id = 2, Nome = "Copa Velha", Codigo = "CV1", Status = "Inscrições Abertas",
            AvisoDeTorneioNovoEm = SegundaAoMeioDia.AddDays(-10),
        });
        ctx.Jogadores.Add(new Jogador { Id = 100, Nome = "Interessado", Cpf = "52998224725" });
        ctx.SaveChanges();

        var enviados = await ResumoSemanalDeTorneiosBackgroundService.VarrerAsync(ctx, push, SegundaAoMeioDia);

        Assert.Equal(1, enviados);
        await push.Received(1).EnviarParaJogadorAsync(100, Arg.Any<string>(),
            Arg.Is<string>(c => c.Contains("Copa da Semana") && !c.Contains("Copa Velha")),
            "/Torneios", AlcanceDoAviso.AppSemEmail);

        // A passada seguinte da MESMA segunda: o marcador foi gravado, nada sai de novo.
        push.ClearReceivedCalls();
        Assert.Equal(0, await ResumoSemanalDeTorneiosBackgroundService.VarrerAsync(
            ctx, push, SegundaAoMeioDia.AddMinutes(30)));
        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // Quem desligou o aviso de torneio aberto não ganha o resumo pela porta dos fundos.
    [Fact]
    public async Task Quem_desligou_torneios_abertos_nao_recebe_o_resumo()
    {
        var ctx = TestInfra.NovoContexto();
        var push = Substitute.For<IPushNotificationService>();

        ctx.Torneios.Add(new Torneio
        {
            Id = 1, Nome = "Copa", Codigo = "CP1", Status = "Inscrições Abertas",
            AvisoDeTorneioNovoEm = SegundaAoMeioDia.AddDays(-1),
        });
        ctx.Jogadores.Add(new Jogador
        {
            Id = 100, Nome = "Desligado", Cpf = "52998224725", NotificarTorneiosAbertos = false,
        });
        ctx.SaveChanges();

        await ResumoSemanalDeTorneiosBackgroundService.VarrerAsync(ctx, push, SegundaAoMeioDia);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O CINTURÃO: a categoria fica sabendo ─────────────────────────────────────────────

    [Fact]
    public async Task A_troca_do_cinturao_e_anunciada_a_quem_joga_os_desafios_da_categoria()
    {
        var ctx = TestInfra.NovoContexto();
        var push = Substitute.For<IPushNotificationService>();

        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 5, Nome = "4ª Masculina", Codigo = "4M", Tipo = "Masculina",
        });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Ana", Cpf = "52998224725" },
            new Jogador { Id = 2, Nome = "Bia", Cpf = "11144477735" });
        ctx.SaveChanges();

        // Um desafio antigo põe os jogadores 30 e 40 na roda da categoria (lado desafiado de
        // outra dupla) — são eles a audiência do anúncio.
        ctx.Desafios.Add(new Desafio
        {
            CategoriaPadraoId = 5, Status = Desafio.Recusado, PropostoEm = DateTime.Now.AddDays(-30),
            DesafianteJogador1Id = 1, DesafianteJogador2Id = 2,
            DesafiadoJogador1Id = 30, DesafiadoJogador2Id = 40,
            DataHora = DateTime.Now.AddDays(-29),
            Formato = FormatoDoDesafio.UmSet,
        });

        // O desafio confirmado que entrega o cinturão VAGO à dupla 1+2.
        var conquista = new Desafio
        {
            CategoriaPadraoId = 5, Status = Desafio.Confirmado, PropostoEm = DateTime.Now.AddDays(-2),
            DesafianteJogador1Id = 1, DesafianteJogador2Id = 2,
            DesafiadoJogador1Id = 30, DesafiadoJogador2Id = 40,
            DataHora = DateTime.Now.AddDays(-1),
            Formato = FormatoDoDesafio.UmSet,
            GamesDesafiante = 6, GamesDesafiado = 3,
        };
        ctx.Desafios.Add(conquista);
        ctx.SaveChanges();

        var efeito = await new MovimentacaoDoCinturao(ctx, push).AplicarAsync(conquista, DateTime.Now);

        Assert.Equal(EfeitoNoCinturao.PegouVago, efeito);

        // A audiência (30 e 40) ouve o anúncio com o nome dos donos novos...
        foreach (var id in new[] { 30, 40 })
            await push.Received(1).EnviarParaJogadorAsync(id, "O cinturão mudou de mão! 🥊",
                Arg.Is<string>(c => c.Contains("Ana") && c.Contains("Bia") && c.Contains("4ª Masculina")),
                "/Desafios/Ranking", AlcanceDoAviso.AppSemEmail);

        // ...e os donos novos NÃO ouvem o anúncio da plateia — o deles é o pessoal.
        foreach (var id in new[] { 1, 2 })
            await push.DidNotReceive().EnviarParaJogadorAsync(id, "O cinturão mudou de mão! 🥊",
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O CINTURÃO: o reinado em risco ───────────────────────────────────────────────────

    [Fact]
    public void O_desafio_pelo_cinturao_tem_texto_proprio()
    {
        var texto = AvisoDoDesafio.RecebidoPeloCinturao("Ana e Bia", "4ª Masculina", "Clube X",
            new DateTime(2026, 8, 26, 19, 0, 0));

        Assert.Equal("Seu reinado está em risco! 🥊", texto.Titulo);
        Assert.Contains("cinturão da 4ª Masculina", texto.Corpo);
        Assert.Contains("26/08 às 19h00", texto.Corpo);
        // O corpo lembra a regra que dói: ignorar também custa o cinturão.
        Assert.Contains("3 desafios", texto.Corpo);
    }

    // ⚠️ A guarda no CONTROLLER: o Desafiar tem que escolher entre os dois textos olhando o
    // dono atual. Se a chamada sumir, todo desafio volta ao texto genérico — sem erro nenhum.
    [Fact]
    public void O_desafiar_pergunta_ao_reinado_antes_de_escolher_o_texto()
    {
        var arquivo = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Controllers", "DesafiosController.cs"));

        Assert.Contains("RecebidoPeloCinturao(", arquivo);
        Assert.Contains("ReinadosNoCinturao", arquivo);
    }

    // ── O CONVITE AO PALPITE nas chaves publicadas ───────────────────────────────────────

    // O convite pega carona no aviso de chaves (um push só, nunca dois): se alguém reescrever
    // o texto e o palpite sumir, este teste é o que grita.
    [Fact]
    public void O_aviso_das_chaves_convida_a_palpitar()
    {
        Assert.Contains("Palpitrômetro", AvisosDoDiaDeJogo.CorpoDasChaves(new DateTime(2026, 9, 5, 9, 30, 0)));
        Assert.Contains("palpite", AvisosDoDiaDeJogo.CorpoDasChaves(null));
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Controllers")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto subindo a partir de " + AppContext.BaseDirectory);
    }
}
