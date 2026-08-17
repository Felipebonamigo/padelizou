using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.Core;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O MVP DO TORNEIO: quem jogou elege o melhor entre os campeões, na semana seguinte ao fim.
//
// O que estes testes guardam, em ordem de importância:
//   · a JANELA é lida do relógio (nada de status gravado), e é isso que faz o recurso valer
//     "pros próximos torneios" sem data de corte escrita no código;
//   · o ELEITORADO é quem esteve na chave — não quem se inscreveu;
//   · o placar fica ESCONDIDO enquanto a votação está aberta;
//   · e o empate devolve os dois, em vez de inventar um critério que ninguém combinou.
public class MvpDoTorneioTests
{
    private static readonly DateTime Domingo = new(2026, 8, 9, 20, 0, 0);

    // Os testes desta classe falam do torneio NORMAL — o único que elege MVP desde
    // 16/08/2026. Estes três atalhos põem o formato Padrão por omissão pra a intenção de cada
    // teste continuar legível; o Americano tem bloco próprio no fim do arquivo, chamando o
    // serviço direto.
    private static bool Aberta(bool usa, string? status, DateTime? ultimo, DateTime agora,
        string? formato = FormatoDoTorneio.Padrao)
        => MvpDoTorneio.Aberta(usa, status, ultimo, agora, formato);

    private static bool Encerrada(bool usa, string? status, DateTime? ultimo, DateTime agora,
        string? formato = FormatoDoTorneio.Padrao)
        => MvpDoTorneio.Encerrada(usa, status, ultimo, agora, formato);

    private static bool TemVotacao(bool usa, string? status, DateTime? ultimo, DateTime agora,
        string? formato = FormatoDoTorneio.Padrao)
        => MvpDoTorneio.TemVotacao(usa, status, ultimo, agora, formato);

    // ─────────────────────────── A JANELA ───────────────────────────

    [Fact]
    public void A_votacao_abre_com_o_torneio_finalizado_e_fecha_sete_dias_depois_do_ultimo_jogo()
    {
        // Logo depois do último jogo: aberta.
        Assert.True(Aberta(true, "Finalizado", Domingo, Domingo.AddHours(1)));

        // No sexto dia ainda dá.
        Assert.True(Aberta(true, "Finalizado", Domingo, Domingo.AddDays(6)));

        // No sétimo, fecha — e a partir daí o resultado aparece.
        Assert.False(Aberta(true, "Finalizado", Domingo, Domingo.AddDays(7)));
        Assert.True(Encerrada(true, "Finalizado", Domingo, Domingo.AddDays(7)));
    }

    [Fact]
    public void Torneio_que_ainda_NAO_acabou_nao_tem_votacao()
    {
        // Pedido do Felipe: "a votação é feita após finalizar o torneio".
        foreach (var status in new[] { "Fase de Grupos", "Mata-Mata", "Inscrições Abertas", "Chaves em Sorteio" })
        {
            Assert.False(Aberta(true, status, Domingo, Domingo.AddHours(1)), status);
            Assert.False(Encerrada(true, status, Domingo, Domingo.AddHours(1)), status);
            Assert.False(TemVotacao(true, status, Domingo, Domingo.AddHours(1)), status);
        }
    }

    [Fact]
    public void Torneio_CANCELADO_nao_tem_MVP()
    {
        // Mesma régua do card de campeão e do ponto de ranking: o evento não aconteceu.
        Assert.False(Aberta(true, "Cancelado", Domingo, Domingo.AddHours(1)));
        Assert.False(TemVotacao(true, "Cancelado", Domingo, Domingo.AddHours(1)));
    }

    [Fact]
    public void Torneio_ANTIGO_ja_nasce_com_a_votacao_fechada()
    {
        // ⚠️ É ISTO que faz o recurso valer "pros próximos torneios" sem nenhum caso especial:
        // não há data de corte no código, nem migração de dados, nem flag. O torneio que acabou
        // mês passado simplesmente já está fora da janela.
        var mesPassado = Domingo.AddDays(-40);

        Assert.False(Aberta(true, "Finalizado", mesPassado, Domingo));
        Assert.True(Encerrada(true, "Finalizado", mesPassado, Domingo));
    }

    [Fact]
    public void Torneio_sem_jogo_finalizado_nenhum_nao_abre_votacao()
    {
        // Sem última bola não há de onde contar a semana. Torneio marcado como finalizado na
        // mão, sem placar nenhum, não elege ninguém.
        Assert.Null(MvpDoTorneio.UltimoJogo(new DateTime?[] { null, null }));
        Assert.False(Aberta(true, "Finalizado", null, Domingo));
        Assert.False(Encerrada(true, "Finalizado", null, Domingo));
    }

    [Fact]
    public void O_ultimo_jogo_e_o_MAIS_RECENTE_e_nulo_nao_atrapalha()
    {
        var fins = new DateTime?[] { Domingo.AddDays(-2), null, Domingo, Domingo.AddDays(-1) };
        Assert.Equal(Domingo, MvpDoTorneio.UltimoJogo(fins));
    }

    // ─────────────────────────── O INTERRUPTOR DO ORGANIZADOR ───────────────────────────

    [Fact]
    public void Torneio_novo_NASCE_com_a_votacao_LIGADA()
    {
        // "O padrão é vir ativo" (Felipe, 11/08/2026). Ao contrário do check-in, a votação não
        // dá trabalho nenhum ao organizador — ela abre sozinha e quem trabalha são os jogadores.
        Assert.True(new Torneio().UsaVotacaoDeMvp);
    }

    [Fact]
    public void A_migracao_liga_a_votacao_nos_torneios_que_JA_EXISTEM()
    {
        // ⚠️ ESTE TESTE OLHA O ARQUIVO DA MIGRAÇÃO, e não o banco, de propósito.
        //
        // O `= true` da propriedade em C# vale só pra objeto NOVO criado pelo app: quem já está
        // gravado recebe o que o `defaultValue` da migração disser. O EF gera `false` aqui
        // (ele olha o tipo, não o inicializador), e com isso TODO torneio de produção nasceria
        // com a votação desligada — o recurso estrearia invisível, sem erro nenhum.
        //
        // A correção é feita à mão no arquivo, então é o arquivo que precisa ser guardado:
        // regerar a migração desfaz o conserto em silêncio.
        var migracao = Path.Combine(RaizDoRepo(), "Padelizou", "Migrations",
            "20260811190842_VotacaoDeMvpOpcional.cs");

        Assert.True(File.Exists(migracao), $"Não achei a migração em {migracao}");

        var texto = File.ReadAllText(migracao);
        Assert.Contains("UsaVotacaoDeMvp", texto);
        Assert.Contains("defaultValue: true", texto);
        Assert.DoesNotContain("defaultValue: false", texto);
    }

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

    // A gestão do torneio salvando o interruptor. `null` é o formulário ANTIGO (aba aberta antes
    // do deploy, sem o campo); `false` é o organizador desmarcando de verdade — e essa diferença
    // só existe na tela porque há um <input type="hidden" value="false"> antes da caixa.
    private static async Task SalvarNaGestaoAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, bool? usaVotacaoDeMvp)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);
        await controller.Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: torneio.QuantidadeQuadras, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            usaVotacaoDeMvp: usaVotacaoDeMvp);
    }

    [Fact]
    public async Task Na_gestao_o_organizador_DESLIGA_e_RELIGA_a_votacao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");

        Assert.True(torneio.UsaVotacaoDeMvp);   // nasce ligada

        await SalvarNaGestaoAsync(ctx, torneio, organizador.Id, usaVotacaoDeMvp: false);
        Assert.False((await ctx.Torneios.FindAsync(torneio.Id))!.UsaVotacaoDeMvp);

        await SalvarNaGestaoAsync(ctx, torneio, organizador.Id, usaVotacaoDeMvp: true);
        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.UsaVotacaoDeMvp);
    }

    [Fact]
    public async Task Formulario_ANTIGO_da_gestao_nao_desliga_a_votacao_sem_ninguem_pedir()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");

        // ⚠️ `null` = aba aberta ANTES deste deploy, que não tem o campo no formulário. Se isso
        // fosse tratado como "desmarcado", salvar qualquer outra coisa na gestão por uma aba
        // velha desligaria a votação do torneio caladinho. Mantém o que está gravado.
        await SalvarNaGestaoAsync(ctx, torneio, organizador.Id, usaVotacaoDeMvp: null);
        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.UsaVotacaoDeMvp);

        // E o mesmo vale no sentido contrário: desligada, uma aba velha não RELIGA.
        torneio.UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        await SalvarNaGestaoAsync(ctx, torneio, organizador.Id, usaVotacaoDeMvp: null);
        Assert.False((await ctx.Torneios.FindAsync(torneio.Id))!.UsaVotacaoDeMvp);
    }

    [Fact]
    public void Organizador_que_DESLIGA_a_votacao_some_com_ela_inteira()
    {
        // Nem cédula, nem resultado. O interruptor é sobre o torneio TER MVP, não sobre "parar
        // de receber voto" — um torneio que desligou não mostra vencedor nenhum.
        Assert.False(Aberta(false, "Finalizado", Domingo, Domingo.AddHours(1)));
        Assert.False(Encerrada(false, "Finalizado", Domingo, Domingo.AddDays(7)));
        Assert.False(TemVotacao(false, "Finalizado", Domingo, Domingo.AddHours(1)));
        Assert.False(TemVotacao(false, "Finalizado", Domingo, Domingo.AddDays(7)));
    }

    [Fact]
    public async Task Com_a_votacao_DESLIGADA_o_voto_e_recusado_pelo_servidor()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        torneio.UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        // ⚠️ Quem recusa é o SERVIÇO, não a tela: esconder o botão não impede um POST montado
        // à mão, e o organizador desligou justamente pra não ter essa disputa no torneio dele.
        var recusa = await MvpDoTorneio.VotarAsync(
            ctx, torneio.Id, vice.Jogador1Id, campea.Jogador1Id, Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.VotosDeMvp);
    }

    [Fact]
    public async Task Religar_a_votacao_devolve_os_votos_que_ja_existiam()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, vice.Jogador1Id, campea.Jogador1Id, Domingo.AddHours(1));
        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, vice.Jogador2Id!.Value, campea.Jogador1Id, Domingo.AddHours(1));
        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, campea.Jogador2Id!.Value, campea.Jogador1Id, Domingo.AddHours(1));

        // Desliga: a tela some, mas os votos NÃO são apagados.
        torneio.UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        var desligada = await MvpDoTorneio.DoTorneioAsync(
            ctx, torneio.Id, vice.Jogador1Id, Domingo.AddDays(MvpDoTorneio.DiasParaVotar));
        Assert.False(desligada!.Aberta);
        Assert.False(desligada.Encerrada);
        Assert.Empty(desligada.Vencedores);
        Assert.Equal(3, ctx.VotosDeMvp.Count());

        // Religa: o resultado volta inteiro. Desligar é esconder, nunca destruir — a decisão
        // do organizador não pode apagar o voto de ninguém.
        torneio.UsaVotacaoDeMvp = true;
        await ctx.SaveChangesAsync();

        var religada = await MvpDoTorneio.DoTorneioAsync(
            ctx, torneio.Id, vice.Jogador1Id, Domingo.AddDays(MvpDoTorneio.DiasParaVotar));
        Assert.True(religada!.Encerrada);
        var eleito = Assert.Single(religada.Vencedores);
        Assert.Equal(campea.Jogador1Id, eleito.JogadorId);
    }

    // ─────────────────────────── O AVISO DE "VOTE NO MVP" ───────────────────────────

    private static IPushNotificationService PushDublado() =>
        Substitute.For<IPushNotificationService>();

    // Quantos avisos de MVP o dublê recebeu, e pra quem.
    // Quem recebeu o aviso, filtrando pelo TÍTULO — que desde 16/08/2026 tem duas versões: a
    // do MVP e a da enquete sozinha (Americano, ou torneio sem campeão). Passar o título
    // errado devolve lista vazia e o teste vira falso negativo, então ele é explícito.
    private static List<int> QuemFoiAvisado(IPushNotificationService push, string? titulo = null)
    {
        titulo ??= MvpDoTorneio.TituloDoAviso;
        return push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync)
                     && (string)c.GetArguments()[1]! == titulo)
            .Select(c => (int)c.GetArguments()[0]!)
            .ToList();
    }

    [Fact]
    public async Task Quando_o_torneio_acaba_TODO_MUNDO_que_jogou_e_avisado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var push = PushDublado();

        var avisados = await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(5));

        Assert.Equal(1, avisados);

        // Os quatro que estiveram na chave — campeões inclusive, que também votam (só não em
        // si mesmos).
        var eleitores = await MvpDoTorneio.EleitoresAsync(ctx, torneio.Id);
        Assert.Equal(4, eleitores.Count);
        Assert.Equal(eleitores.OrderBy(x => x), QuemFoiAvisado(push).OrderBy(x => x));
    }

    [Fact]
    public async Task O_aviso_sai_UMA_VEZ_SO_por_mais_que_o_varredor_passe()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var push = PushDublado();

        // ⚠️ O varredor passa a cada 5 minutos. Sem a coluna de carimbo, cada passada
        // repetiria o push pras 90 a 220 pessoas de um torneio real.
        Assert.Equal(1, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(5)));
        Assert.Equal(0, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(10)));
        Assert.Equal(0, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(15)));

        Assert.Equal(4, QuemFoiAvisado(push).Count);
        Assert.NotNull((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeMvpEnviadoEm);
    }

    [Fact]
    public async Task O_aviso_vai_pelo_app_e_SEM_email()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var push = PushDublado();

        await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(5));

        // ⚠️ Este aviso sai pra TODO MUNDO que jogou, de uma vez: num torneio real são 90 a 220
        // e-mails num minuto — a rajada que já queimou a cota duas vezes, levando junto duas
        // recuperações de senha. Perder um voto de MVP não custa nada; perder o "esqueci minha
        // senha" custa a conta. E WhatsApp está fora: o mesmo recado pra 200 pessoas é
        // exatamente o que a Meta chama de spam.
        var alcances = push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync))
            .Select(c => (AlcanceDoAviso)c.GetArguments()[4]!)
            .ToList();

        Assert.NotEmpty(alcances);
        Assert.All(alcances, a => Assert.Equal(AlcanceDoAviso.AppSemEmail, a));
    }

    [Fact]
    public async Task Torneio_com_a_votacao_DESLIGADA_nao_avisa_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        torneio.UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        var push = PushDublado();
        Assert.Equal(0, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(5)));
        Assert.Empty(QuemFoiAvisado(push));
    }

    [Fact]
    public async Task Torneio_FORA_da_janela_nao_avisa_e_nao_fica_sendo_varrido_pra_sempre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var push = PushDublado();

        // Passaram-se 10 dias desde o último jogo: a votação já fechou.
        var tarde = Domingo.AddDays(10);
        Assert.Equal(0, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, tarde));
        Assert.Empty(QuemFoiAvisado(push));

        // ⚠️ Mas o carimbo SAI mesmo assim: sem isso a varredura voltaria a este torneio a cada
        // 5 minutos, pra sempre, só pra descobrir de novo que não há o que mandar.
        Assert.NotNull((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeMvpEnviadoEm);
    }

    [Fact]
    public async Task Torneio_SEM_campeao_avisa_pela_ENQUETE_e_nao_promete_MVP()
    {
        // ⚠️ Este teste dizia "não avisa" até 16/08/2026, e a mudança foi de propósito: sem
        // cédula não há MVP, mas a ENQUETE do clube continua aberta — as pessoas jogaram no
        // clube, e é disso que ela trata. O que não pode é o aviso prometer uma votação que
        // não existe naquele torneio.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        // Tira a coroa: sobra torneio finalizado sem ninguém pra eleger.
        foreach (var d in ctx.Duplas.Where(d => d.UltimaFase == "Campeao").ToList()) d.UltimaFase = "Final";
        await ctx.SaveChangesAsync();

        var push = PushDublado();
        Assert.Equal(1, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(5)));

        // Quem jogou foi convidado pela ENQUETE — e ninguém recebeu o convite do MVP.
        Assert.NotEmpty(QuemFoiAvisado(push, MvpDoTorneio.TituloDoAvisoPara(temMvp: false)));
        Assert.Empty(QuemFoiAvisado(push));

        // E o corpo não promete votação nenhuma.
        await push.Received().EnviarParaJogadorAsync(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Is<string>(corpo => !corpo.Contains("melhor jogador")),
            Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task De_MADRUGADA_o_aviso_espera_a_manha()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var push = PushDublado();

        // 3h da manhã: um admin arrumando dado não acorda 200 pessoas.
        var madrugada = new DateTime(2026, 8, 10, 3, 0, 0);
        Assert.Equal(0, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, madrugada));
        Assert.Empty(QuemFoiAvisado(push));

        // ⚠️ Mas 22h e 23h PASSAM, ao contrário da janela civilizada do lembrete de pagamento
        // (9h–21h): torneio de padel terminando às 22h é o normal, e segurar o aviso até as 9h
        // perderia justamente o momento em que ele vale — todo mundo ainda no clube.
        Assert.True(MvpDoTorneio.HoraDeAvisar(new DateTime(2026, 8, 9, 22, 30, 0)));
        Assert.True(MvpDoTorneio.HoraDeAvisar(new DateTime(2026, 8, 9, 23, 50, 0)));
        Assert.False(MvpDoTorneio.HoraDeAvisar(new DateTime(2026, 8, 10, 4, 0, 0)));
    }

    [Fact]
    public async Task Torneio_que_AINDA_NAO_ACABOU_nao_avisa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        torneio.Status = "Mata-Mata";
        await ctx.SaveChangesAsync();

        var push = PushDublado();
        Assert.Equal(0, await AvisoDoMvpBackgroundService.VarrerAsync(ctx, push, Domingo.AddMinutes(5)));
        Assert.Empty(QuemFoiAvisado(push));

        // E o carimbo NÃO sai: este torneio ainda vai acabar, e quando acabar precisa avisar.
        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeMvpEnviadoEm);
    }

    // ─────────────────────────── A APURAÇÃO ───────────────────────────

    private static CandidatoAMvp Candidato(int id, string nome, int votos) =>
        new() { JogadorId = id, Nome = nome, Votos = votos };

    [Fact]
    public void Ganha_quem_tem_mais_votos()
    {
        var vencedores = MvpDoTorneio.Apurar(new[]
        {
            Candidato(1, "Ana", 3),
            Candidato(2, "Bruno", 7),
            Candidato(3, "Carla", 5),
        });

        var unico = Assert.Single(vencedores);
        Assert.Equal("Bruno", unico.Nome);
    }

    [Fact]
    public void Empate_no_topo_devolve_TODOS_os_empatados()
    {
        // ⚠️ Inventar desempate ("quem recebeu o voto primeiro") faria o sistema escolher um
        // MVP por um motivo que ninguém combinou. Dois MVPs é resposta honesta.
        var vencedores = MvpDoTorneio.Apurar(new[]
        {
            Candidato(1, "Ana", 6),
            Candidato(2, "Bruno", 6),
            Candidato(3, "Carla", 4),
        });

        Assert.Equal(2, vencedores.Count);
        Assert.Contains(vencedores, v => v.Nome == "Ana");
        Assert.Contains(vencedores, v => v.Nome == "Bruno");
    }

    [Fact]
    public void Abaixo_do_minimo_de_votos_NINGUEM_e_proclamado()
    {
        // "Melhor jogador do torneio, com 1 voto" é uma frase que não se sustenta — mesma
        // régua do "1º lugar com 0 pontos" que saiu do ranking.
        var poucos = MvpDoTorneio.Apurar(new[] { Candidato(1, "Ana", MvpDoTorneio.VotosMinimos - 1) });
        Assert.Empty(poucos);

        var suficientes = MvpDoTorneio.Apurar(new[] { Candidato(1, "Ana", MvpDoTorneio.VotosMinimos) });
        Assert.Single(suficientes);
    }

    [Fact]
    public void A_ordem_da_apuracao_e_TOTAL_e_nao_muda_entre_duas_leituras()
    {
        // Empate é o estado normal de uma votação pequena, e ordenação parcial faz a lista
        // trocar de ordem entre dois carregamentos da MESMA página.
        var entrada = new[]
        {
            Candidato(9, "Bruno", 5),
            Candidato(2, "Ana", 5),
            Candidato(7, "Ana", 5),
        };

        var primeira = MvpDoTorneio.Apurar(entrada);
        var segunda = MvpDoTorneio.Apurar(entrada.Reverse().ToArray());

        Assert.Equal(
            primeira.Select(c => c.JogadorId),
            segunda.Select(c => c.JogadorId));

        // Nome, e o id como último desempate (as duas "Ana").
        Assert.Equal(new[] { 2, 7, 9 }, primeira.Select(c => c.JogadorId));
    }

    // ─────────────────────────── A CÉDULA E O ELEITORADO ───────────────────────────

    // Um torneio finalizado, com uma partida terminada AGORA (votação aberta) e duas duplas.
    private static async Task<(Torneio torneio, Categoria categoria, List<Jogador> jogadores)>
        MontarTorneioFinalizadoAsync(DbPadelContext ctx, DateTime fimDoJogo)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        duplas[0].UltimaFase = "Campeao";
        duplas[1].UltimaFase = "Final";

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            VencedorId = duplas[0].Id,
            Status = "Finalizada",
            HorarioFimReal = fimDoJogo,
            Fase = "Final",
            Codigo = "P1",
        });
        await ctx.SaveChangesAsync();

        var jogadores = ctx.Jogadores.ToList();
        return (torneio, categoria, jogadores);
    }

    [Fact]
    public async Task A_cedula_sao_os_CAMPEOES_e_o_eleitorado_e_quem_esteve_na_chave()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        var candidatos = await MvpDoTorneio.CandidatosAsync(ctx, torneio.Id);
        var eleitores = await MvpDoTorneio.EleitoresAsync(ctx, torneio.Id);

        // Cédula: só os dois campeões.
        Assert.Equal(2, candidatos.Count);
        Assert.Contains(candidatos, c => c.JogadorId == campea.Jogador1Id);
        Assert.Contains(candidatos, c => c.JogadorId == campea.Jogador2Id!.Value);
        Assert.DoesNotContain(candidatos, c => c.JogadorId == vice.Jogador1Id);

        // Eleitorado: os quatro que jogaram, campeões inclusive.
        Assert.Equal(4, eleitores.Count);
        Assert.Contains(vice.Jogador1Id, eleitores);
        Assert.Contains(campea.Jogador1Id, eleitores);
    }

    [Fact]
    public async Task Quem_ficou_na_LISTA_DE_ESPERA_ou_SEM_PARCEIRO_nao_vota()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var esperando = new Jogador { Nome = "Quem Esperou", Cpf = "55500000001" };
        var semParceiro = new Jogador { Nome = "Quem Nao Achou Parceiro", Cpf = "55500000002" };
        ctx.Jogadores.AddRange(esperando, semParceiro);
        await ctx.SaveChangesAsync();

        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = esperando.Id, Jogador2Id = esperando.Id,
            EmListaDeEspera = true,
        });
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = semParceiro.Id, Jogador2Id = null,
        });
        await ctx.SaveChangesAsync();

        var eleitores = await MvpDoTorneio.EleitoresAsync(ctx, torneio.Id);

        // ⚠️ Os dois se INSCREVERAM e nenhum dos dois JOGOU. Deixá-los votar transformaria a
        // eleição num concurso de quem cadastra mais amigos.
        Assert.DoesNotContain(esperando.Id, eleitores);
        Assert.DoesNotContain(semParceiro.Id, eleitores);
    }

    [Fact]
    public async Task Campea_em_DUAS_categorias_aparece_UMA_vez_na_cedula()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");

        // A mesma pessoa ganha também a mista — é o caso comum de quem joga duas categorias.
        var outraCategoria = new Categoria { Nome = "Categoria Mista A", Codigo = "MISTA", TorneioId = torneio.Id };
        ctx.Categorias.Add(outraCategoria);
        await ctx.SaveChangesAsync();

        var parceiroNaMista = new Jogador { Nome = "Parceiro Da Mista", Cpf = "55500000003" };
        ctx.Jogadores.Add(parceiroNaMista);
        await ctx.SaveChangesAsync();

        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = outraCategoria.Id,
            Jogador1Id = campea.Jogador1Id,
            Jogador2Id = parceiroNaMista.Id,
            UltimaFase = "Campeao",
        });
        await ctx.SaveChangesAsync();

        var candidatos = await MvpDoTorneio.CandidatosAsync(ctx, torneio.Id);

        // ⚠️ Duas linhas pra mesma pessoa DIVIDIRIAM os votos dela entre elas, e ela perderia
        // pra quem tem uma categoria só. A cédula tem uma linha por PESSOA.
        var linha = Assert.Single(candidatos.Where(c => c.JogadorId == campea.Jogador1Id));
        Assert.Equal(2, linha.Categorias.Count);
    }

    [Fact]
    public async Task Linha_de_TIME_nao_vira_candidato()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var organizador = ctx.Jogadores.First(j => j.Nome == "Organizador");
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = organizador.Id,
            NomeTime = "Batata Padel",
            UltimaFase = "Campeao",
        });
        await ctx.SaveChangesAsync();

        var candidatos = await MvpDoTorneio.CandidatosAsync(ctx, torneio.Id);

        // O Jogador1Id da linha de TIME é o organizador que cadastrou — ele viraria candidato a
        // melhor jogador de um torneio que talvez nem tenha jogado.
        Assert.DoesNotContain(candidatos, c => c.JogadorId == organizador.Id);
    }

    // ─────────────────────────── O VOTO ───────────────────────────

    [Fact]
    public async Task Quem_NAO_jogou_o_torneio_nao_vota()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var deFora = new Jogador { Nome = "Passante", Cpf = "55500000009" };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");

        var recusa = await MvpDoTorneio.VotarAsync(
            ctx, torneio.Id, deFora.Id, campea.Jogador1Id, Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.VotosDeMvp);
    }

    [Fact]
    public async Task Nao_da_pra_votar_em_quem_NAO_e_campeao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        var recusa = await MvpDoTorneio.VotarAsync(
            ctx, torneio.Id, vice.Jogador1Id, vice.Jogador2Id!.Value, Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.VotosDeMvp);
    }

    [Fact]
    public async Task Ninguem_vota_em_si_mesmo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");

        var recusa = await MvpDoTorneio.VotarAsync(
            ctx, torneio.Id, campea.Jogador1Id, campea.Jogador1Id, Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.VotosDeMvp);
    }

    [Fact]
    public async Task Votar_de_novo_TROCA_a_escolha_em_vez_de_somar_outro_voto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        Assert.Null(await MvpDoTorneio.VotarAsync(
            ctx, torneio.Id, vice.Jogador1Id, campea.Jogador1Id, Domingo.AddHours(1)));
        Assert.Null(await MvpDoTorneio.VotarAsync(
            ctx, torneio.Id, vice.Jogador1Id, campea.Jogador2Id!.Value, Domingo.AddHours(2)));

        // Uma linha só, apontando pro segundo escolhido — mesma régua do Elogio.
        var voto = Assert.Single(ctx.VotosDeMvp);
        Assert.Equal(campea.Jogador2Id!.Value, voto.CandidatoId);
    }

    [Fact]
    public async Task Depois_que_a_janela_fecha_o_voto_e_RECUSADO()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        var recusa = await MvpDoTorneio.VotarAsync(
            ctx, torneio.Id, vice.Jogador1Id, campea.Jogador1Id,
            Domingo.AddDays(MvpDoTorneio.DiasParaVotar + 1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.VotosDeMvp);
    }

    [Fact]
    public async Task O_placar_fica_ESCONDIDO_enquanto_a_votacao_esta_aberta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, vice.Jogador1Id, campea.Jogador1Id, Domingo.AddHours(1));
        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, vice.Jogador2Id!.Value, campea.Jogador1Id, Domingo.AddHours(1));
        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, campea.Jogador2Id!.Value, campea.Jogador1Id, Domingo.AddHours(1));

        var aberta = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, vice.Jogador1Id, Domingo.AddHours(2));

        // ⚠️ Placar parcial em votação aberta vira efeito manada: quem abre a tela depois vota
        // no que já está ganhando. Enquanto está aberta só a PARTICIPAÇÃO é pública.
        Assert.True(aberta!.Aberta);
        Assert.Equal(3, aberta.TotalDeVotos);
        Assert.All(aberta.Candidatos, c => Assert.Equal(0, c.Votos));
        Assert.Empty(aberta.Vencedores);

        // Depois que fecha, o resultado aparece inteiro.
        var fechada = await MvpDoTorneio.DoTorneioAsync(
            ctx, torneio.Id, vice.Jogador1Id, Domingo.AddDays(MvpDoTorneio.DiasParaVotar));

        Assert.True(fechada!.Encerrada);
        var eleito = Assert.Single(fechada.Vencedores);
        Assert.Equal(campea.Jogador1Id, eleito.JogadorId);
        Assert.Equal(3, eleito.Votos);
    }

    [Fact]
    public async Task O_voto_de_quem_esta_olhando_volta_marcado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);

        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, vice.Jogador1Id, campea.Jogador1Id, Domingo.AddHours(1));

        var minhaVisao = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, vice.Jogador1Id, Domingo.AddHours(2));
        Assert.Equal(campea.Jogador1Id, minhaVisao!.MeuVoto);
        Assert.True(minhaVisao.SouEleitor);

        // Visitante deslogado vê a tela e não é eleitor.
        var visitante = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, null, Domingo.AddHours(2));
        Assert.Null(visitante!.MeuVoto);
        Assert.False(visitante.SouEleitor);
    }

    // ─────────────────────────── A CONQUISTA "MVP" DO PERFIL ───────────────────────────

    // Semeia N votos na campeã, direto na tabela: com a janela fechada o VotarAsync recusa,
    // e é justamente o cenário fechado que a conquista lê.
    private static async Task VotarNaCampeaAsync(DbPadelContext ctx, int torneioId, int candidatoId, int votos)
    {
        var eleitores = await MvpDoTorneio.EleitoresAsync(ctx, torneioId);
        foreach (var votanteId in eleitores.Where(e => e != candidatoId).Take(votos))
        {
            ctx.VotosDeMvp.Add(new VotoDeMvp
            {
                TorneioId = torneioId, VotanteId = votanteId, CandidatoId = candidatoId,
                CriadoEm = Domingo.AddHours(1),
            });
        }
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task A_conquista_conta_so_eleicao_FECHADA_e_com_votos_suficientes()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var vice = ctx.Duplas.First(d => d.UltimaFase == "Final");

        await VotarNaCampeaAsync(ctx, torneio.Id, campea.Jogador1Id, MvpDoTorneio.VotosMinimos);

        // Com a votação ainda ABERTA não há eleito — mesma razão de a tela esconder o parcial.
        Assert.Equal(0, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, campea.Jogador1Id, Domingo.AddDays(1)));

        // Fechou: a campeã tem a conquista, o vice não.
        var depoisDeFechar = Domingo.AddDays(MvpDoTorneio.DiasParaVotar);
        Assert.Equal(1, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, campea.Jogador1Id, depoisDeFechar));
        Assert.Equal(0, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, vice.Jogador1Id, depoisDeFechar));
    }

    [Fact]
    public async Task Abaixo_do_minimo_de_votos_ninguem_ganha_a_conquista()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");

        await VotarNaCampeaAsync(ctx, torneio.Id, campea.Jogador1Id, MvpDoTorneio.VotosMinimos - 1);

        Assert.Equal(0, await MvpDoTorneio.VezesEleitoMvpAsync(
            ctx, campea.Jogador1Id, Domingo.AddDays(MvpDoTorneio.DiasParaVotar)));
    }

    [Fact]
    public async Task Empate_da_a_conquista_pros_DOIS_e_desligar_a_votacao_a_recolhe()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var campea = ctx.Duplas.First(d => d.UltimaFase == "Campeao");
        var depoisDeFechar = Domingo.AddDays(MvpDoTorneio.DiasParaVotar);

        // Cada campeão com o mínimo de votos, empatados no topo. Os votos entram direto na
        // tabela (a apuração lê a tabela; quem valida eleitor é o VotarAsync, na entrada).
        for (int i = 0; i < MvpDoTorneio.VotosMinimos * 2; i++)
        {
            var votante = new Jogador { Nome = $"Eleitor Extra {i}", Cpf = $"888000000{i:00}" };
            ctx.Jogadores.Add(votante);
            await ctx.SaveChangesAsync();
            ctx.VotosDeMvp.Add(new VotoDeMvp
            {
                TorneioId = torneio.Id,
                VotanteId = votante.Id,
                CandidatoId = i % 2 == 0 ? campea.Jogador1Id : campea.Jogador2Id!.Value,
                CriadoEm = Domingo.AddHours(1),
            });
        }
        await ctx.SaveChangesAsync();

        // Empate proclama os dois — inventar desempate escolheria um MVP por um motivo que
        // ninguém combinou.
        Assert.Equal(1, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, campea.Jogador1Id, depoisDeFechar));
        Assert.Equal(1, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, campea.Jogador2Id!.Value, depoisDeFechar));

        // O organizador desligou a votação: o MVP some da tela E da conquista — o interruptor
        // é sobre o torneio TER isso. Os votos ficam; religar devolve tudo.
        var t = ctx.Torneios.First(x => x.Id == torneio.Id);
        t.UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, campea.Jogador1Id, depoisDeFechar));
    }

    // ─────────────────────── O AMERICANO NÃO ELEGE MVP (16/08/2026) ───────────────────────

    [Theory]
    [InlineData(FormatoDoTorneio.Americano)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas)]
    public void Americano_nao_tem_votacao_de_MVP_em_estado_nenhum(string formato)
    {
        // Decisão do Felipe: "é apenas para os torneios normais". No rodízio não há final nem
        // dupla fixa, e a cédula encolheria a um punhado de amigos escolhendo entre si.
        Assert.False(MvpDoTorneio.ElegeMvp(formato));

        // Nem durante a janela…
        Assert.False(Aberta(true, "Finalizado", Domingo, Domingo.AddHours(1), formato));
        Assert.False(TemVotacao(true, "Finalizado", Domingo, Domingo.AddHours(1), formato));

        // …nem DEPOIS dela: sem isto, um Americano encerrado abriria a tela dizendo "este
        // torneio não elegeu um MVP" — falando de uma eleição que nunca existiu ali.
        Assert.False(Encerrada(true, "Finalizado", Domingo, Domingo.AddDays(8), formato));
        Assert.False(TemVotacao(true, "Finalizado", Domingo, Domingo.AddDays(8), formato));
    }

    [Fact]
    public void O_torneio_normal_continua_elegendo()
    {
        Assert.True(MvpDoTorneio.ElegeMvp(FormatoDoTorneio.Padrao));
        Assert.True(Aberta(true, "Finalizado", Domingo, Domingo.AddHours(1), FormatoDoTorneio.Padrao));

        // ⚠️ Formato nulo é o torneio ANTIGO, gravado antes de a coluna existir — e ele é
        // Padrão por natureza (o Americano veio depois). Tratar nulo como "não elege" tiraria
        // o MVP de torneios reais que já o tinham.
        Assert.True(MvpDoTorneio.ElegeMvp(null));
    }

    [Theory]
    [InlineData(FormatoDoTorneio.Americano)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas)]
    public async Task No_Americano_a_ENQUETE_do_clube_tambem_NAO_vale(string formato)
    {
        // ⚠️ ISTO JÁ FOI O CONTRÁRIO, e por pouco tempo: entre 16 e 17/08/2026 a enquete
        // valia no Americano, pra não abrir buraco no dado do "Melhor Clube do ano". O Felipe
        // decidiu que o rodízio fica fora das duas coisas — decisão de produto dele, tomada
        // sabendo que a coleta de 2027 passa a sair só dos torneios normais.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var doBanco = ctx.Torneios.First(t => t.Id == torneio.Id);
        doBanco.Formato = formato;
        await ctx.SaveChangesAsync();

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, null, Domingo.AddHours(1));

        // Nem eleição…
        Assert.False(votacao!.Aberta);
        Assert.False(votacao.Encerrada);
        Assert.False(votacao.TemVotacao);

        // …nem avaliação.
        Assert.False(EnqueteDoTorneio.Aberta(
            votacao.StatusDoTorneio, votacao.UltimoJogo, Domingo.AddHours(1), votacao.Formato));

        // ⚠️ E o SERVIDOR recusa, não só a tela esconde: quem montar o POST à mão não passa
        // por view nenhuma.
        var quemJogou = (await MvpDoTorneio.EleitoresAsync(ctx, torneio.Id)).First();
        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, quemJogou, notaClube: 5, notaOrganizacao: 4, Domingo.AddHours(2));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task No_torneio_normal_com_o_MVP_DESLIGADO_a_enquete_continua()
    {
        // A enquete segue o FORMATO, não o INTERRUPTOR: desligar a eleição é dizer "não quero
        // disputa entre os jogadores", não "não quero que falem do meu clube". É aqui que a
        // tela existe só pra enquete — o caso que sustenta o desenho dela.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarTorneioFinalizadoAsync(ctx, Domingo);
        var doBanco = ctx.Torneios.First(t => t.Id == torneio.Id);
        doBanco.UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, null, Domingo.AddHours(1));
        Assert.False(votacao!.TemVotacao);

        Assert.True(EnqueteDoTorneio.Aberta(
            votacao.StatusDoTorneio, votacao.UltimoJogo, Domingo.AddHours(1), votacao.Formato));

        var quemJogou = (await MvpDoTorneio.EleitoresAsync(ctx, torneio.Id)).First();
        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, quemJogou, notaClube: 5, notaOrganizacao: 4, Domingo.AddHours(2));

        Assert.Null(recusa);
        Assert.Single(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public void O_aviso_do_Americano_NAO_promete_votacao()
    {
        // Prometer "escolha o melhor jogador" e levar a uma tela sem cédula é o jeito mais
        // rápido de ensinar que o nosso aviso mente.
        var comMvp = MvpDoTorneio.CorpoDoAviso("Copa do Clube", temMvp: true);
        var soEnquete = MvpDoTorneio.CorpoDoAviso("Rodizio de Sabado", temMvp: false);

        Assert.Contains("melhor jogador", comMvp);
        Assert.DoesNotContain("melhor jogador", soEnquete);
        Assert.Contains("nota", soEnquete);

        Assert.Equal(MvpDoTorneio.TituloDoAviso, MvpDoTorneio.TituloDoAvisoPara(true));
        Assert.NotEqual(MvpDoTorneio.TituloDoAviso, MvpDoTorneio.TituloDoAvisoPara(false));
    }
}
