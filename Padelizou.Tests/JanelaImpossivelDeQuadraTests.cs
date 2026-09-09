using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// JANELA DE QUADRA QUE NÃO DEIXA NENHUM JOGO ACONTECER É DADO ERRADO — e o motor para de
// obedecê-la em silêncio.
//
// 🗣️ Felipe, 09/09/2026, na TERCEIRA vez: *"refiz a grade, ainda ta pulando pro dia 15 e ainda
// tem bastante coisa errada — ta sem o nome do clube que vai ser o jogo, e ainda pulando os dias,
// é a 3a vez q te falo sobre isso, e eu preciso publicar ainda hoje essa chave"*.
//
// 🕳️ AS DUAS QUEIXAS SÃO O MESMO DEFEITO, e a assinatura estava no print: os jogos REAIS estavam
// **sem quadra**. `GradeDeJogos.Encaixar` só grava `NomeQuadra` quando encontra quadra ABERTA
// naquele horário; sem nenhuma aberta, ele marca a hora, deixa o lugar em branco e escorrega pro
// horário seguinte — dia após dia, calado. Daí sai tudo junto: o pulo de 12 pra 15, o jogo sem
// local, e até dois jogos da MESMA dupla no mesmo minuto (o último recurso do encaixe, quando as
// vagas acabam).
//
// ⚠️ A CAUSA RAIZ É CONFIGURAÇÃO, e o motor não pode obedecer configuração impossível. Uma janela
// que não deixa NENHUMA quadra aberta em NENHUM horário do torneio não é uma restrição: é um
// dígito errado no `datetime-local`. Obedecê-la destrói a grade inteira; ignorá-la devolve o
// comportamento de quem nunca preencheu o campo — que é o que o organizador tinha antes.
//
// ⚠️ E O GUARDA É ESTREITO DE PROPÓSITO: basta UMA quadra abrir em UM horário do torneio pra ele
// não disparar. A janela legítima do local alugado ("das 8h às 14h de sábado") continua valendo
// inteira — ela deixa quadra aberta, então não é impossível.
public class JanelaImpossivelDeQuadraTests
{
    private static Torneio Torneio(DateTime? fim = null) => new()
    {
        Nome = "2ª Etapa ER Padel Tour", Codigo = "ER2",
        DataInicio = new DateTime(2026, 9, 12),
        DataFim = fim,
        HoraInicioDoDia = new TimeSpan(8, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50,
    };

    private static Quadra Quadra(string nome, DateTime? de, DateTime? ate) =>
        new() { Nome = nome, DisponivelDe = de, DisponivelAte = ate };

    [Fact]
    public void Janela_que_nao_abre_em_nenhum_horario_do_torneio_e_ignorada()
    {
        // O caso do Er: as duas quadras com janela apontando pra DEPOIS do torneio.
        var quadras = new[]
        {
            Quadra("Er Padel · Arena Loja 7", new DateTime(2026, 9, 20, 8, 0, 0), new DateTime(2026, 9, 20, 22, 0, 0)),
            Quadra("Er Padel · Arena Nclass", new DateTime(2026, 9, 21, 8, 0, 0), new DateTime(2026, 9, 21, 22, 0, 0)),
        };

        var sedes = SedesDoTorneio.Montar(clubePrincipalId: 1, minutosParaTrocarDeClube: 0,
            quadras, categorias: System.Array.Empty<Categoria>(), torneio: Torneio(new DateTime(2026, 9, 13)));

        // Dentro do torneio, as duas voltam a estar abertas — é o comportamento de quem nunca
        // preencheu o campo.
        Assert.True(sedes.QuadraAberta("Er Padel · Arena Loja 7", new DateTime(2026, 9, 12, 17, 10, 0)));
        Assert.True(sedes.QuadraAberta("Er Padel · Arena Nclass", new DateTime(2026, 9, 13, 8, 0, 0)));
    }

    // ⚠️ O LIMITE DO GUARDA, ESCRITO COM TODAS AS LETRAS: UMA vaga aberta já basta pra ele NÃO
    // disparar, mesmo que uma vaga seja ridiculamente pouco pros 97 jogos do torneio.
    //
    // É de propósito, e a alternativa foi considerada e recusada: fazer o guarda comparar vagas
    // com o NÚMERO DE JOGOS o transformaria de "esta configuração é impossível" em "esta
    // configuração é apertada" — e apertado é justamente o que o organizador escolhe quando aluga
    // quadra por hora. O motor não pode desfazer a escolha dele por achá-la apertada.
    //
    // Quem cobre o caso apertado é a CONFERÊNCIA, que não muda a grade: `PorQueNaoCoube` conta
    // jogos × vagas e `BuracosNaGrade` aponta o dia vazio.
    [Fact]
    public void Uma_vaga_aberta_ja_basta_pro_guarda_nao_disparar_mesmo_sendo_pouco()
    {
        var instante = new DateTime(2026, 9, 12, 8, 0, 0);
        var quadras = new[] { Quadra("Arena 1", instante, instante), Quadra("Arena 2", instante, instante) };

        var sedes = SedesDoTorneio.Montar(clubePrincipalId: 1, minutosParaTrocarDeClube: 0,
            quadras, categorias: System.Array.Empty<Categoria>(), torneio: Torneio(new DateTime(2026, 9, 13)));

        Assert.True(sedes.QuadraAberta("Arena 1", instante));                                  // a única vaga
        Assert.False(sedes.QuadraAberta("Arena 1", new DateTime(2026, 9, 12, 17, 10, 0)));     // e só ela
    }

    [Fact]
    public void Janela_legitima_do_local_alugado_continua_valendo_inteira()
    {
        // ⚠️ A CONTRAPARTIDA, e ela importa mais que o guarda: o Er aluga o Radar das 8h às 14h de
        // sábado. Essa janela DEIXA quadra aberta, então não é impossível — e o guarda não pode
        // encostar nela, senão o motor marca jogo no lugar fechado (o defeito de 08/09).
        var quadras = new[]
        {
            Quadra("Er Padel 1", null, null),
            Quadra("Radar 1", new DateTime(2026, 9, 12, 8, 0, 0), new DateTime(2026, 9, 12, 14, 0, 0)),
        };

        var sedes = SedesDoTorneio.Montar(clubePrincipalId: 1, minutosParaTrocarDeClube: 0,
            quadras, categorias: System.Array.Empty<Categoria>(), torneio: Torneio(new DateTime(2026, 9, 13)));

        Assert.True(sedes.QuadraAberta("Radar 1", new DateTime(2026, 9, 12, 10, 0, 0)));
        Assert.False(sedes.QuadraAberta("Radar 1", new DateTime(2026, 9, 12, 20, 0, 0)));
        Assert.False(sedes.QuadraAberta("Radar 1", new DateTime(2026, 9, 13, 10, 0, 0)));
    }

    [Fact]
    public void Uma_quadra_aberta_ja_basta_pro_guarda_nao_disparar()
    {
        // Uma quadra sem janela salva a grade — as outras, mesmo erradas, continuam fechadas. O
        // guarda é sobre a grade ser POSSÍVEL, não sobre cada quadra estar certa.
        var quadras = new[]
        {
            Quadra("Er Padel 1", null, null),
            Quadra("Radar 1", new DateTime(2026, 9, 20, 8, 0, 0), new DateTime(2026, 9, 20, 22, 0, 0)),
        };

        var sedes = SedesDoTorneio.Montar(clubePrincipalId: 1, minutosParaTrocarDeClube: 0,
            quadras, categorias: System.Array.Empty<Categoria>(), torneio: Torneio(new DateTime(2026, 9, 13)));

        Assert.False(sedes.QuadraAberta("Radar 1", new DateTime(2026, 9, 12, 10, 0, 0)));
    }

    [Fact]
    public void Sem_o_torneio_na_mao_nada_muda()
    {
        // Todo chamador que não passa o torneio (e há vários) continua com o comportamento antigo:
        // o guarda não pode mudar o motor por baixo de quem não pediu.
        var quadras = new[] { Quadra("Arena 1", new DateTime(2026, 9, 20, 8, 0, 0), new DateTime(2026, 9, 20, 22, 0, 0)) };

        var sedes = SedesDoTorneio.Montar(clubePrincipalId: 1, minutosParaTrocarDeClube: 0,
            quadras, categorias: System.Array.Empty<Categoria>());

        Assert.False(sedes.QuadraAberta("Arena 1", new DateTime(2026, 9, 12, 10, 0, 0)));
    }
}
