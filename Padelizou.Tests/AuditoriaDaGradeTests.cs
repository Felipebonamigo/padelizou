using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O BOTÃO "CONFERIR A GRADE" — 🗣️ Felipe, 09/09/2026: "faz esse botão e sobe".
//
// Nasceu de um beco: o Felipe pediu duas vezes que eu conferisse a grade do torneio dele em
// `dev`, e esta sessão NÃO ALCANÇA o `dev` (o proxy recusa o CONNECT com 403). A saída não é
// pedir print — é virar a auditoria em tela, pra ele apertar e ver, em qualquer torneio.
//
// ⚠️ O PONTO DE ARQUITETURA: a TELA e o TESTE chamam o MESMO serviço. Uma auditoria escrita
// duas vezes é uma auditoria que um dia diz "tudo certo" sobre uma regra que mudou no outro
// lado — e ela seria acreditada, porque é justamente ela que existe pra ser acreditada.
public class AuditoriaDaGradeTests
{
    private static readonly DateTime Sexta = new(2026, 10, 9);
    private static readonly DateTime Sabado = new(2026, 10, 10);

    private static Torneio Torneio() => new()
    {
        Nome = "T", Codigo = "T1",
        DataInicio = Sexta.AddHours(18),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50,
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    private static Dupla Dupla(int id, int j1, int j2) =>
        new() { Id = id, Jogador1Id = j1, Jogador2Id = j2, Categoria = new Categoria { Id = 1, Nome = "3ª", Codigo = "C3" } };

    private static Partida Jogo(int d1, int d2, DateTime? quando, string fase = "Grupo A", string? quadra = null) =>
        new() { Codigo = "X", Fase = fase, CategoriaId = 1, Dupla1Id = d1, Dupla2Id = d2, HorarioPrevisto = quando, NomeQuadra = quadra };

    // Grade limpa não inventa achado — é o caso que o organizador vai ver na maioria das vezes,
    // e uma tela que sempre acha alguma coisa deixa de ser lida.
    [Fact]
    public void Grade_limpa_nao_tem_achado()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[] { Jogo(1, 2, Sabado.AddHours(9)) };

        Assert.Empty(AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma));
    }

    [Fact]
    public void Acusa_jogo_dentro_do_impedimento_da_dupla()
    {
        var comImpedimento = Dupla(1, 10, 11);
        comImpedimento.ImpedimentoSabadoManha = true;
        var duplas = new[] { comImpedimento, Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9)) }, duplas, SedesDoTorneio.Nenhuma);

        var achado = Assert.Single(achados);
        Assert.Equal(AuditoriaDaGrade.Impedimento, achado.Regra);
        Assert.Contains("Sábado de manhã", achado.Descricao);
    }

    // ⚠️ O ACHADO MAIS GRAVE DOS SEIS: a pessoa não se divide. Foi ele que apareceu na
    // auditoria de hoje com 4 quadras, e é o que o organizador descobre sendo chamado duas
    // vezes no microfone.
    [Fact]
    public void Acusa_a_mesma_pessoa_em_dois_jogos_no_mesmo_horario()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 10, 30) };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(9)),
            Jogo(3, 2, Sabado.AddHours(9)),   // o jogador 10 está nas duplas 1 e 3
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos);
    }

    [Fact]
    public void Acusa_concentracao_nao_atendida_na_fase_de_grupos()
    {
        var concentrada = Dupla(1, 10, 11);
        concentrada.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        var duplas = new[] { concentrada, Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9)) }, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.Concentracao);
    }

    // ⚠️ E NÃO ACUSA NA ELIMINATÓRIA: a concentração vale só na fase de grupos (decisão do
    // Felipe). Acusar aqui encheria a tela de achado que não é problema — e tela que grita
    // demais ninguém lê.
    [Fact]
    public void Nao_acusa_concentracao_fora_da_fase_de_grupos()
    {
        var concentrada = Dupla(1, 10, 11);
        concentrada.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        var duplas = new[] { concentrada, Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9), fase: "Semifinal") }, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.Concentracao);
    }

    [Fact]
    public void Acusa_eliminatoria_no_sabado_a_noite_de_categoria_que_pediu_pra_nao_ter()
    {
        var torneio = Torneio();
        var categoria = new Categoria { Id = 1, Nome = "5ª Feminina", Codigo = "C5F", EliminatoriaNoSabadoANoite = false };
        torneio.Categorias.Add(categoria);

        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(torneio,
            new[] { Jogo(1, 2, Sabado.AddHours(21), fase: "Semifinal") }, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.NoiteDeSabado);
    }

    [Fact]
    public void Acusa_quadra_usada_fora_da_janela_do_local_alugado()
    {
        var sedes = SedesDoTorneio.Montar(1, 0,
            new[]
            {
                new Quadra { Nome = "Casa", ClubeId = 1 },
                new Quadra { Nome = "Alugada", ClubeId = 2, DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12) },
            },
            Array.Empty<Categoria>());

        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(15), quadra: "Alugada") }, duplas, sedes);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.QuadraFechada);
    }

    [Fact]
    public void Acusa_jogo_sem_horario()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, null) }, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.SemHorario);
    }

    // O organizador precisa saber ONDE olhar: o achado carrega a hora e o nome de quem está
    // envolvido, senão vira "tem algo errado em algum lugar", que não resolve nada.
    [Fact]
    public void O_achado_diz_quando_e_quem()
    {
        var comImpedimento = Dupla(1, 10, 11);
        comImpedimento.ImpedimentoSabadoManha = true;
        comImpedimento.NomeTime = "Fulano e Sicrano";   // NomeDeExibicao sai daqui

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9)) },
            new[] { comImpedimento, Dupla(2, 20, 21) }, SedesDoTorneio.Nenhuma);

        var achado = Assert.Single(achados);
        Assert.Equal(Sabado.AddHours(9), achado.Quando);
        Assert.Contains("Fulano e Sicrano", achado.Descricao);
    }
}
