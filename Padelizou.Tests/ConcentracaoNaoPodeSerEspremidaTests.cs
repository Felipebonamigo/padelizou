using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// QUEM SÓ PODE JOGAR NUM TURNO ENTRA NELE ANTES DE QUEM PODE JOGAR EM QUALQUER UM.
//
// 🗣️ Felipe, 09/09/2026, no "Conferir grade" do Er em `dev`: quatro achados de **Concentração**,
// todos iguais — *"Dupla 1142 joga 15/09 às 20:30, fora de 'Os 2 jogos na sexta à noite'"* —, mais
// quatro de "Depois do fim do torneio" e um de "Mesma pessoa em dois jogos", TODOS no mesmo
// 15/09 20:30. E a própria tela dizendo: *"as quadras comportam o torneio. Sobrou uma restrição de
// horário empurrando jogo"*.
//
// 🕳️ A CONCENTRAÇÃO NÃO RESERVA VAGA — ela só PROÍBE o resto. `GradeDeJogos.Encaixar` percorre a
// fila e pega o primeiro jogo que serve naquele horário; um jogo SEM concentração serve em
// qualquer horário, então ele toma as vagas da sexta antes que as duplas concentradas cheguem na
// fila. Quando a sexta acaba, as concentradas não têm mais nenhum horário possível — o turno delas
// já passou —, e o encaixe as segura até o último recurso, que as despeja no fim da grade: fora do
// turno prometido, depois do fim do torneio, sem quadra e com gente repetida.
//
// ⚠️ É A MESMA FORMA DO CONSERTO DA QUADRA DE CASA: quem tem UMA opção passa na frente de quem tem
// TODAS. Restrição que admite uma janela só é servida primeiro, ou ela é sempre a última a
// conseguir vaga — e "última" aqui quer dizer "nunca", porque a janela dela já passou.
public class ConcentracaoNaoPodeSerEspremidaTests
{
    private const int Duracao = 50;

    // Sexta 11/09/2026 abrindo 18h, sábado 12/09 abrindo 8h — o calendário do Er.
    private static Torneio Torneio() => new()
    {
        Nome = "2ª Etapa ER Padel Tour", Codigo = "ER2",
        DataInicio = new DateTime(2026, 9, 11),
        DataFim = new DateTime(2026, 9, 13),
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = Duracao,
    };

    [Fact]
    public void Dupla_concentrada_na_sexta_joga_na_sexta_mesmo_com_a_fila_cheia()
    {
        var torneio = Torneio();

        // Duas duplas concentradas na sexta à noite, e MUITAS sem concentração nenhuma. Sem
        // prioridade, as sem restrição enchem a sexta e as concentradas sobram.
        var concentradas = new[] { 1001, 1002 };
        var duplas = new List<Dupla>();
        foreach (var id in concentradas)
            duplas.Add(new Dupla { Id = id, ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite });
        for (int id = 2001; id <= 2030; id++)
            duplas.Add(new Dupla { Id = id });

        var jogos = new List<Partida>();
        // A fila chega com os SEM concentração na frente — é o caso que expõe o defeito, e é o
        // que a intercalação de grupos produz naturalmente.
        for (int id = 2001; id <= 2029; id += 2)
            jogos.Add(new Partida { Codigo = $"L{id}", Fase = "Grupo A", CategoriaId = 1, Dupla1Id = id, Dupla2Id = id + 1 });
        jogos.Add(new Partida { Codigo = "C1", Fase = "Grupo B", CategoriaId = 1, Dupla1Id = 1001, Dupla2Id = 1002 });

        var concentracao = ConcentracaoDeJogos.De(torneio, duplas);

        var horarios = VagasDaGrade.Montar(torneio, torneio.AberturaDaGrade, jogos.Count,
            peloMenosAte: concentracao.AteQuando,
            jogosComJanela: VagasDaGrade.JogosComJanela(jogos, porDupla: concentracao.Janelas));

        GradeDeJogos.Encaixar(jogos, horarios, Duracao,
            ocupantesPorDupla: null, quadras: null, jaMarcados: null, quadrasPorCategoria: null,
            janelasProibidasPorDupla: null, sedes: null,
            janelasSoNosGruposPorDupla: concentracao.Janelas);

        var doConcentrado = jogos.Single(j => j.Dupla1Id == 1001);
        var sexta = new DateTime(2026, 9, 11);

        Assert.NotNull(doConcentrado.HorarioPrevisto);
        Assert.True(doConcentrado.HorarioPrevisto!.Value.Date == sexta,
            $"a dupla pediu os 2 jogos na SEXTA (11/09) e o jogo dela ficou "
            + $"{doConcentrado.HorarioPrevisto:dd/MM 'às' HH:mm}");
    }

    // ⚠️ A PRIORIDADE NÃO PODE COMPRAR O DESCANSO (10/09/2026). 🗣️ Felipe, num print dos grupos
    // do Er em produção: *"varios jogos seguidos, temos q evitar isso, acho que ja tem uma regra
    // pra isso, nao?"* — Alexandre/Felipe 21:20 e 22:10, Maickel/Rodrigo 12:10 e 13:00.
    //
    // 🕳️ Tinha a regra (OrdemDasRodadas, 07/09: intercalar os grupos por rodada), e a prioridade
    // da concentração passou por cima dela: ela puxa os DOIS jogos da dupla pra frente da fila, e
    // o segundo entra no horário seguinte ao primeiro. "Os 2 jogos na sexta à noite" quer dizer
    // no mesmo TURNO — a sexta tem sete rodadas, cabe folga —, não colados.
    [Fact]
    public void Dupla_concentrada_nao_joga_dois_horarios_seguidos_quando_o_turno_tem_espaco()
    {
        var torneio = Torneio();

        var duplas = new List<Dupla>
        {
            new() { Id = 1001, ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite },
            new() { Id = 1002 }, new() { Id = 1003 },
        };
        for (int id = 2001; id <= 2030; id++) duplas.Add(new Dupla { Id = id });

        // A concentrada joga DUAS vezes (grupo de 3), e a fila tem outros 15 jogos sem restrição
        // — mais que o suficiente pra ocupar o horário entre os dois dela.
        var jogos = new List<Partida>
        {
            new() { Codigo = "C1", Fase = "Grupo B", CategoriaId = 1, Dupla1Id = 1001, Dupla2Id = 1002 },
            new() { Codigo = "C2", Fase = "Grupo B", CategoriaId = 1, Dupla1Id = 1001, Dupla2Id = 1003 },
        };
        for (int id = 2001; id <= 2029; id += 2)
            jogos.Add(new Partida { Codigo = $"L{id}", Fase = "Grupo A", CategoriaId = 1, Dupla1Id = id, Dupla2Id = id + 1 });

        var concentracao = ConcentracaoDeJogos.De(torneio, duplas);
        var horarios = VagasDaGrade.Montar(torneio, torneio.AberturaDaGrade, jogos.Count,
            peloMenosAte: concentracao.AteQuando,
            jogosComJanela: VagasDaGrade.JogosComJanela(jogos, porDupla: concentracao.Janelas));

        GradeDeJogos.Encaixar(jogos, horarios, Duracao,
            ocupantesPorDupla: null, quadras: null, jaMarcados: null, quadrasPorCategoria: null,
            janelasProibidasPorDupla: null, sedes: null,
            janelasSoNosGruposPorDupla: concentracao.Janelas);

        var daConcentrada = jogos.Where(j => j.Dupla1Id == 1001).Select(j => j.HorarioPrevisto!.Value).OrderBy(h => h).ToList();
        var sexta = new DateTime(2026, 9, 11);

        Assert.All(daConcentrada, h => Assert.Equal(sexta, h.Date));             // os 2 na sexta
        var folga = (daConcentrada[1] - daConcentrada[0]).TotalMinutes / Duracao;
        Assert.True(folga >= GradeDeJogos.HorariosDeDescanso + 1,
            $"a dupla concentrada jogou {daConcentrada[0]:HH:mm} e {daConcentrada[1]:HH:mm} — "
            + $"{folga - 1:0} horário(s) de descanso, e a régua pede {GradeDeJogos.HorariosDeDescanso}");
    }

    [Fact]
    public void Sem_ninguem_concentrado_a_ordem_da_fila_e_a_de_sempre()
    {
        // A contrapartida: a prioridade só existe quando alguém está concentrado. Sem isso, o
        // primeiro da fila continua pegando a primeira vaga — que é o comportamento de sempre e o
        // que os testes de descanso e de ordem das fases medem.
        var torneio = Torneio();

        var jogos = new List<Partida>
        {
            new() { Codigo = "A", Fase = "Grupo A", CategoriaId = 1, Dupla1Id = 2001, Dupla2Id = 2002 },
            new() { Codigo = "B", Fase = "Grupo A", CategoriaId = 1, Dupla1Id = 2003, Dupla2Id = 2004 },
        };

        var horarios = VagasDaGrade.Montar(torneio, torneio.AberturaDaGrade, jogos.Count);

        GradeDeJogos.Encaixar(jogos, horarios, Duracao,
            ocupantesPorDupla: null, quadras: null, jaMarcados: null, quadrasPorCategoria: null,
            janelasProibidasPorDupla: null, sedes: null, janelasSoNosGruposPorDupla: null);

        Assert.Equal(torneio.AberturaDaGrade, jogos[0].HorarioPrevisto);
    }
}
