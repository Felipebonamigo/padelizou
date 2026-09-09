using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "OS MELHORES FICAM NO CLUBE DELE" — quem vai pro local alugado é a categoria mais fraca.
//
// 🗣️ Felipe, 09/09/2026, pelo Er: *"ele quer que os jogos que vao para o radar sejam das
// categoria menos fortes (lembrando que as mais fortes sao terceira e quarta), para deixar os
// melhores no clube dele"*.
//
// A peça pra isso JÁ EXISTIA (`Categoria.PodeJogarNaSedeExtra`, 08/09/2026): desligada, a
// categoria não recebe quadra do local alugado. Marcar a 3ª e a 4ª resolve metade do pedido — a
// metade "os melhores não vão pro Radar".
//
// ⚠️ A OUTRA METADE É A QUE FALTAVA, e ela é o contrário do que a intuição diz. O `Encaixar`
// oferece as quadras DE CASA primeiro pra TODO jogo, inclusive pros que também poderiam jogar no
// alugado. Então a 6ª — que pode ir pro Radar — pega a quadra de casa, e a 3ª — que só pode jogar
// em casa — fica esperando o próximo horário, com a quadra do Radar VAZIA ao lado.
//
// O resultado é o pior dos dois mundos: o organizador paga a hora do Radar e ela fica ociosa,
// enquanto a categoria forte, que ele queria em casa, atrasa.
public class MelhoresNoClubeDeCasaTests
{
    private const int Duracao = 50;
    private static readonly DateTime Sabado = new(2026, 9, 12, 8, 0, 0);

    [Fact]
    public void Quem_pode_ir_pro_alugado_nao_toma_a_quadra_de_casa_de_quem_nao_pode()
    {
        // Uma quadra em casa e uma no Radar. Dois jogos no mesmo horário: um da 3ª (presa em
        // casa) e um da 6ª (pode ir pro Radar). O certo é a 6ª ir pro Radar e a 3ª ficar em casa,
        // e os DOIS jogarem no primeiro horário.
        var sedes = SedesDoTorneio.Montar(
            clubePrincipalId: 1, minutosParaTrocarDeClube: 0,
            quadras: new[]
            {
                new Quadra { Nome = "Er Padel 1", ClubeId = null },   // nulo = clube do torneio
                new Quadra { Nome = "Radar 1",    ClubeId = 2 },
            },
            categorias: new[]
            {
                new Categoria { Id = 3, Nome = "3ª Masculina", Codigo = "C3M", PodeJogarNaSedeExtra = false },
                new Categoria { Id = 6, Nome = "6ª Masculina", Codigo = "C6M", PodeJogarNaSedeExtra = true },
            });

        // A 6ª entra na fila PRIMEIRO — é o caso que expõe o problema: quem chega antes pega a
        // quadra de casa, e quem só pode jogar em casa chega depois.
        var daSexta = new Partida { Codigo = "A", Fase = "Grupo A", CategoriaId = 6, Dupla1Id = 61, Dupla2Id = 62 };
        var daTerceira = new Partida { Codigo = "B", Fase = "Grupo A", CategoriaId = 3, Dupla1Id = 31, Dupla2Id = 32 };
        var jogos = new List<Partida> { daSexta, daTerceira };

        var horarios = new List<DateTime>
        {
            Sabado, Sabado,                                   // 2 vagas no 1º horário
            Sabado.AddMinutes(Duracao), Sabado.AddMinutes(Duracao),
        };

        GradeDeJogos.Encaixar(jogos, horarios, Duracao, ocupantesPorDupla: null,
            quadras: new[] { "Er Padel 1", "Radar 1" }, jaMarcados: null,
            quadrasPorCategoria: null, janelasProibidasPorDupla: null, sedes: sedes);

        Assert.Equal(Sabado, daTerceira.HorarioPrevisto);
        Assert.Equal("Er Padel 1", daTerceira.NomeQuadra);

        Assert.Equal(Sabado, daSexta.HorarioPrevisto);
        Assert.Equal("Radar 1", daSexta.NomeQuadra);
    }

    [Fact]
    public void Sem_ninguem_preso_em_casa_a_sede_principal_continua_enchendo_primeiro()
    {
        // A contrapartida: sem categoria presa, nada muda — a sede de casa enche antes, que é o
        // que faz a hora alugada custar menos (comentário de GradeDeJogos.Encaixar).
        var sedes = SedesDoTorneio.Montar(
            clubePrincipalId: 1, minutosParaTrocarDeClube: 0,
            quadras: new[]
            {
                new Quadra { Nome = "Er Padel 1", ClubeId = null },
                new Quadra { Nome = "Radar 1",    ClubeId = 2 },
            },
            categorias: new[]
            {
                new Categoria { Id = 6, Nome = "6ª Masculina", Codigo = "C6M", PodeJogarNaSedeExtra = true },
            });

        var primeiro = new Partida { Codigo = "A", Fase = "Grupo A", CategoriaId = 6, Dupla1Id = 61, Dupla2Id = 62 };
        var jogos = new List<Partida> { primeiro };

        GradeDeJogos.Encaixar(jogos, new List<DateTime> { Sabado, Sabado }, Duracao,
            ocupantesPorDupla: null, quadras: new[] { "Er Padel 1", "Radar 1" }, jaMarcados: null,
            quadrasPorCategoria: null, janelasProibidasPorDupla: null, sedes: sedes);

        Assert.Equal("Er Padel 1", primeiro.NomeQuadra);
    }
}
