using Microsoft.EntityFrameworkCore;
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

    // ─────────────────────────── A JANELA ───────────────────────────

    [Fact]
    public void A_votacao_abre_com_o_torneio_finalizado_e_fecha_sete_dias_depois_do_ultimo_jogo()
    {
        // Logo depois do último jogo: aberta.
        Assert.True(MvpDoTorneio.Aberta(true, "Finalizado", Domingo, Domingo.AddHours(1)));

        // No sexto dia ainda dá.
        Assert.True(MvpDoTorneio.Aberta(true, "Finalizado", Domingo, Domingo.AddDays(6)));

        // No sétimo, fecha — e a partir daí o resultado aparece.
        Assert.False(MvpDoTorneio.Aberta(true, "Finalizado", Domingo, Domingo.AddDays(7)));
        Assert.True(MvpDoTorneio.Encerrada(true, "Finalizado", Domingo, Domingo.AddDays(7)));
    }

    [Fact]
    public void Torneio_que_ainda_NAO_acabou_nao_tem_votacao()
    {
        // Pedido do Felipe: "a votação é feita após finalizar o torneio".
        foreach (var status in new[] { "Fase de Grupos", "Mata-Mata", "Inscrições Abertas", "Chaves em Sorteio" })
        {
            Assert.False(MvpDoTorneio.Aberta(true, status, Domingo, Domingo.AddHours(1)), status);
            Assert.False(MvpDoTorneio.Encerrada(true, status, Domingo, Domingo.AddHours(1)), status);
            Assert.False(MvpDoTorneio.TemVotacao(true, status, Domingo, Domingo.AddHours(1)), status);
        }
    }

    [Fact]
    public void Torneio_CANCELADO_nao_tem_MVP()
    {
        // Mesma régua do card de campeão e do ponto de ranking: o evento não aconteceu.
        Assert.False(MvpDoTorneio.Aberta(true, "Cancelado", Domingo, Domingo.AddHours(1)));
        Assert.False(MvpDoTorneio.TemVotacao(true, "Cancelado", Domingo, Domingo.AddHours(1)));
    }

    [Fact]
    public void Torneio_ANTIGO_ja_nasce_com_a_votacao_fechada()
    {
        // ⚠️ É ISTO que faz o recurso valer "pros próximos torneios" sem nenhum caso especial:
        // não há data de corte no código, nem migração de dados, nem flag. O torneio que acabou
        // mês passado simplesmente já está fora da janela.
        var mesPassado = Domingo.AddDays(-40);

        Assert.False(MvpDoTorneio.Aberta(true, "Finalizado", mesPassado, Domingo));
        Assert.True(MvpDoTorneio.Encerrada(true, "Finalizado", mesPassado, Domingo));
    }

    [Fact]
    public void Torneio_sem_jogo_finalizado_nenhum_nao_abre_votacao()
    {
        // Sem última bola não há de onde contar a semana. Torneio marcado como finalizado na
        // mão, sem placar nenhum, não elege ninguém.
        Assert.Null(MvpDoTorneio.UltimoJogo(new DateTime?[] { null, null }));
        Assert.False(MvpDoTorneio.Aberta(true, "Finalizado", null, Domingo));
        Assert.False(MvpDoTorneio.Encerrada(true, "Finalizado", null, Domingo));
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

    [Fact]
    public void Organizador_que_DESLIGA_a_votacao_some_com_ela_inteira()
    {
        // Nem cédula, nem resultado. O interruptor é sobre o torneio TER MVP, não sobre "parar
        // de receber voto" — um torneio que desligou não mostra vencedor nenhum.
        Assert.False(MvpDoTorneio.Aberta(false, "Finalizado", Domingo, Domingo.AddHours(1)));
        Assert.False(MvpDoTorneio.Encerrada(false, "Finalizado", Domingo, Domingo.AddDays(7)));
        Assert.False(MvpDoTorneio.TemVotacao(false, "Finalizado", Domingo, Domingo.AddHours(1)));
        Assert.False(MvpDoTorneio.TemVotacao(false, "Finalizado", Domingo, Domingo.AddDays(7)));
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
}
