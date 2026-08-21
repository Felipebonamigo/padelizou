using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 21/08/2026 — A GRADE PASSA A OLHAR SOBREPOSIÇÃO, NÃO INSTANTE EXATO.
//
// ⚠️ ISTO CONSERTA UM BUG QUE EXISTIA EM TORNEIO DE UM CLUBE SÓ. O `Encaixar` guardava quem
// estava ocupado num `Dictionary<DateTime, HashSet<int>>` — chaveado pelo INSTANTE. Ele só
// enxergava conflito quando dois jogos caíam no mesmo minuto cravado.
//
// E o torneio produz horário fora do reticulado o tempo todo: o "Refazer grade" do meio do dia
// parte de `DateTime.Now` (TorneiosController.Chaves.AberturaDoRecalculo). Aperte o botão às
// 20h13 e os jogos novos nascem em 20:13, 21:03…, enquanto os intocados seguem em 20:00, 20:50.
// Duas grades desalinhadas, e o dicionário nunca cruzava as duas:
//
//   • a mesma PESSOA podia ser chamada pras 20:13 com o jogo dela das 20:00 ainda em quadra;
//   • a mesma QUADRA podia receber dois jogos a 20 minutos de distância.
//
// Nada reclamava — nem tela, nem teste, nem log. Estes testes são a rede que faltava.
public class GradeComHorarioQuebradoTests
{
    private const int Duracao = 50;
    private static readonly DateTime Vinte = new(2026, 8, 15, 20, 0, 0);

    private static Partida Jogo(int a, int b, int categoria = 1) =>
        new() { Dupla1Id = a, Dupla2Id = b, CategoriaId = categoria };

    // Quem joga em cada dupla — sem isto o Encaixar compara por dupla, e duas duplas
    // diferentes com a MESMA pessoa não conflitariam.
    private static Dictionary<int, int[]> Ocupantes(params (int Dupla, int[] Pessoas)[] mapa) =>
        mapa.ToDictionary(x => x.Dupla, x => x.Pessoas);

    // ⚠️ O TESTE QUE PROVA O BUG. Sem a correção ele FALHA: o jogo novo cai às 20:13 com o
    // jogador 7 ainda em quadra desde as 20:00.
    [Fact]
    public void Ninguem_e_chamado_pra_duas_quadras_quando_o_recalculo_parte_de_um_minuto_quebrado()
    {
        // O jogo que já estava em quadra, no reticulado antigo.
        var emQuadra = Jogo(1, 2);
        emQuadra.HorarioPrevisto = Vinte;
        emQuadra.NomeQuadra = "Quadra A";

        // O "refazer grade" às 20h13: as vagas novas nascem fora do reticulado.
        var quebrado = Vinte.AddMinutes(13);
        var vagas = new List<DateTime> { quebrado, quebrado.AddMinutes(Duracao), quebrado.AddMinutes(Duracao * 2) };

        // O jogador 7 está nas DUAS duplas — na que está em quadra e na que vai ser marcada.
        var jogos = new List<Partida> { Jogo(3, 4) };
        var ocupantes = Ocupantes((1, new[] { 7, 8 }), (2, new[] { 9, 10 }),
                                  (3, new[] { 7, 11 }), (4, new[] { 12, 13 }));

        GradeDeJogos.Encaixar(jogos, vagas, Duracao, ocupantes,
            new[] { "Quadra A", "Quadra B" }, new[] { emQuadra });

        // Ele espera o jogo das 20:00 acabar — 20:13 sobrepõe, 21:03 não.
        Assert.Equal(quebrado.AddMinutes(Duracao), jogos[0].HorarioPrevisto);
    }

    // ⚠️ O MESMO FURO, PELA QUADRA. Consertar só a pessoa seria conserto pela metade: o
    // agendamento âncora por CATEGORIA já desalinha categorias entre si dentro do mesmo
    // torneio, sem precisar de "refazer grade" nenhum.
    [Fact]
    public void Duas_partidas_nao_dividem_a_mesma_quadra_em_horarios_que_se_cruzam()
    {
        var emQuadra = Jogo(1, 2);
        emQuadra.HorarioPrevisto = Vinte;
        emQuadra.NomeQuadra = "Quadra A";

        // 20 minutos depois: instante diferente, mesma faixa de tempo.
        var vagas = new List<DateTime> { Vinte.AddMinutes(20), Vinte.AddMinutes(20 + Duracao) };

        // Gente completamente diferente — o único impedimento possível é a QUADRA.
        var jogos = new List<Partida> { Jogo(3, 4) };
        var ocupantes = Ocupantes((1, new[] { 1, 2 }), (2, new[] { 3, 4 }),
                                  (3, new[] { 5, 6 }), (4, new[] { 7, 8 }));

        GradeDeJogos.Encaixar(jogos, vagas, Duracao, ocupantes,
            new[] { "Quadra A" }, new[] { emQuadra });

        // Só existe uma quadra e ela está tomada até as 20:50 — a vaga das 20:20 é pulada.
        Assert.Equal(Vinte.AddMinutes(20 + Duracao), jogos[0].HorarioPrevisto);
        Assert.Equal("Quadra A", jogos[0].NomeQuadra);
    }

    // ⚠️ A MUDANÇA DE COMPORTAMENTO DECLARADA (21/08/2026): com quadra cadastrada e nenhuma
    // livre no intervalo, a vaga é PULADA em vez de o jogo nascer com hora e SEM quadra.
    //
    // É o incidente do Interno de 05/08/2026 fechado na origem — uma semifinal entrou ao vivo
    // sem quadra enquanto os outros quatro jogos tinham a delas, e o organizador não conseguia
    // nem arrumar na mão, porque o seletor de quadra oferece a mesma lista que faltou.
    [Fact]
    public void Jogo_nao_nasce_com_hora_e_sem_quadra_quando_todas_estao_tomadas()
    {
        var tomada = Jogo(1, 2);
        tomada.HorarioPrevisto = Vinte;
        tomada.NomeQuadra = "Quadra A";

        var vagas = new List<DateTime> { Vinte, Vinte.AddMinutes(Duracao), Vinte.AddMinutes(Duracao * 2) };
        var jogos = new List<Partida> { Jogo(3, 4) };

        GradeDeJogos.Encaixar(jogos, vagas, Duracao, null, new[] { "Quadra A" }, new[] { tomada });

        Assert.NotNull(jogos[0].NomeQuadra);
        Assert.Equal(Vinte.AddMinutes(Duracao), jogos[0].HorarioPrevisto);
    }

    // ⚠️ MAS A VÁLVULA CONTINUA: quando as vagas que sobram não dão conta dos jogos que faltam,
    // marcar sem quadra volta a ser melhor que não marcar. Sem isto, a correção acima criaria
    // um mal pior — jogo sem horário nenhum, que não aparece em tela alguma.
    [Fact]
    public void Sem_vaga_pra_todo_mundo_o_jogo_ainda_nasce_mesmo_sem_quadra_livre()
    {
        var tomada = Jogo(1, 2);
        tomada.HorarioPrevisto = Vinte;
        tomada.NomeQuadra = "Quadra A";

        // Uma vaga só, e a única quadra está ocupada nela.
        var vagas = new List<DateTime> { Vinte };
        var jogos = new List<Partida> { Jogo(3, 4) };

        GradeDeJogos.Encaixar(jogos, vagas, Duracao, null, new[] { "Quadra A" }, new[] { tomada });

        Assert.Equal(Vinte, jogos[0].HorarioPrevisto);
    }

    // ⚠️ A REDE CONTRA REGRESSÃO: no caminho normal — tudo alinhado no mesmo reticulado — a
    // troca de instante por intervalo não pode mudar NADA. Sem este teste, a mudança não entra.
    [Fact]
    public void A_grade_alinhada_de_sempre_continua_dando_o_mesmo_resultado()
    {
        var jogos = new List<Partida> { Jogo(1, 2), Jogo(3, 4), Jogo(5, 6), Jogo(7, 8) };
        var vagas = GradeDeJogos
            .Horarios(Vinte, new TimeSpan(23, 50, 0), quadras: 2, Duracao, jogos.Count)
            .ToList();

        GradeDeJogos.Encaixar(jogos, vagas, Duracao, null, new[] { "Quadra A", "Quadra B" });

        // Dois por horário, um em cada quadra, na ordem da fila — como sempre foi.
        Assert.Equal(new DateTime?[] { Vinte, Vinte, Vinte.AddMinutes(Duracao), Vinte.AddMinutes(Duracao) },
            jogos.Select(j => j.HorarioPrevisto).ToArray());
        Assert.Equal(new[] { "Quadra A", "Quadra B", "Quadra A", "Quadra B" },
            jogos.Select(j => j.NomeQuadra));
    }
}
