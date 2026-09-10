using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// OS HORÁRIOS QUE O TORNEIO DE FATO USA — a lista que o "Definir horário" oferece.
//
// 🗣️ Felipe, 10/09/2026, com o seletor de data/hora do navegador aberto na tela do Er: *"ao
// alterar o horario, deixe para que fique mais facil seguindo a ordem padrão do jogo (nesse
// torneio é de 50 em 50 min)"*.
//
// 🕳️ O campo era um `datetime-local` livre: pra levar um jogo das 19:40 pras 20:30 era preciso
// rolar a roda de minutos de 40 até 30 passando por 41, 42, 43… e ACERTAR o passo da grade de
// cabeça. Errar por um minuto não dá erro nenhum — dá um jogo às 20:31 que desalinha a quadra
// do resto do torneio e só aparece no Conferir grade.
//
// A régua: os horários saem do MESMO motor da grade (GradeDeJogos.Horarios com uma quadra é a
// sequência de slots), então "de 50 em 50" não é número digitado em lugar nenhum — é a duração
// da partida do torneio, e a virada do dia é a do expediente dele.
public class HorariosDaGradeTests
{
    private static Torneio Torneio(int quadras = 3, int duracao = 50, string fimDoDia = "21:20") => new()
    {
        Nome = "Torneio dos Horários",
        Codigo = "HOR1",
        DataInicio = new DateTime(2026, 9, 12),
        HoraInicioDoDia = new TimeSpan(8, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = TimeSpan.Parse(fimDoDia),
        QuantidadeQuadras = quadras,
        TempoPrevistoPartidaMinutos = duracao,
        DataFim = new DateTime(2026, 9, 13),
    };

    private static Partida Jogo(string quando, string quadra = "Quadra 1") => new()
    {
        Codigo = "J",
        Status = "Agendada",
        Fase = "Fase de Grupos",
        HorarioPrevisto = DateTime.Parse(quando),
        NomeQuadra = quadra,
    };

    private static DateTime As(string quando) => DateTime.Parse(quando);

    [Fact]
    public void Os_slots_andam_no_passo_da_partida_do_torneio()
    {
        // "De 50 em 50" é a duração configurada, não um número escrito aqui: um torneio de 11
        // minutos anda de 11 em 11 pela mesma linha de código.
        var slots = HorariosDaGrade.Montar(Torneio(), new[] { Jogo("2026-09-12 09:40") });

        Assert.Equal(
            new[] { As("2026-09-12 08:00"), As("2026-09-12 08:50"), As("2026-09-12 09:40"), As("2026-09-12 10:30") },
            slots.Select(s => s.Horario).Take(4));
    }

    [Fact]
    public void O_dia_vira_na_abertura_do_dia_seguinte_e_nao_na_madrugada()
    {
        // O expediente fecha 21:20 e o dia seguinte abre 8h: depois do último slot do dia vem
        // 08:00 do dia seguinte, nunca 22:10. Quem sabe disso é GradeDeJogos.DepoisDe, e é dele
        // que a lista sai — uma segunda conta aqui marcaria jogo na madrugada.
        var slots = HorariosDaGrade.Montar(Torneio(), new[] { Jogo("2026-09-13 09:00") });
        var doDia12 = slots.Where(s => s.Horario.Day == 12).ToList();

        Assert.Equal(As("2026-09-12 21:20"), doDia12.Max(s => s.Horario));
        Assert.Contains(slots, s => s.Horario == As("2026-09-13 08:00"));
        Assert.DoesNotContain(slots, s => s.Horario == As("2026-09-12 22:10"));
    }

    [Fact]
    public void Cada_slot_diz_quantas_quadras_ainda_estao_livres()
    {
        // É o que evita a escolha que o servidor vai recusar: pôr o jogo num horário em que a
        // quadra dele já tem dono (Services/HorarioNaMao).
        var slots = HorariosDaGrade.Montar(Torneio(quadras: 3), new[]
        {
            Jogo("2026-09-12 08:50", "Quadra 1"),
            Jogo("2026-09-12 08:50", "Quadra 2"),
            Jogo("2026-09-12 09:40", "Quadra 1"),
        });

        var oitoECinquenta = slots.Single(s => s.Horario == As("2026-09-12 08:50"));
        Assert.Equal(2, oitoECinquenta.Ocupadas);
        Assert.Equal(1, oitoECinquenta.Livres);
        Assert.False(oitoECinquenta.Lotado);

        Assert.Equal(3, slots.Single(s => s.Horario == As("2026-09-12 08:00")).Livres);
    }

    [Fact]
    public void Horario_com_todas_as_quadras_tomadas_sai_marcado_como_lotado()
    {
        var slots = HorariosDaGrade.Montar(Torneio(quadras: 2), new[]
        {
            Jogo("2026-09-12 08:50", "Quadra 1"),
            Jogo("2026-09-12 08:50", "Quadra 2"),
        });

        Assert.True(slots.Single(s => s.Horario == As("2026-09-12 08:50")).Lotado);
    }

    [Fact]
    public void O_horario_quebrado_de_um_jogo_entra_na_lista_no_lugar_dele()
    {
        // Jogo já mexido na mão pras 20:13. Ele não é slot da grade, mas EXISTE — e sem ele na
        // lista o seletor abriria sem nada marcado, como se o jogo não tivesse hora.
        var slots = HorariosDaGrade.Montar(Torneio(), new[] { Jogo("2026-09-12 20:13") });

        var horarios = slots.Select(s => s.Horario).ToList();
        Assert.Contains(As("2026-09-12 20:13"), horarios);
        Assert.Equal(horarios.OrderBy(h => h), horarios);
        Assert.Equal(1, slots.Single(s => s.Horario == As("2026-09-12 20:13")).Ocupadas);
    }

    [Fact]
    public void A_lista_passa_do_ultimo_jogo_pra_dar_pra_onde_empurrar()
    {
        // Uma lista que para no último jogo não deixa ATRASAR nada — e atrasar é metade do que
        // o organizador faz com este botão no dia de jogo.
        var slots = HorariosDaGrade.Montar(Torneio(), new[] { Jogo("2026-09-13 12:10") });

        Assert.True(slots.Count(s => s.Horario > As("2026-09-13 12:10")) >= 4,
            $"a lista acaba em {slots.Max(s => s.Horario):dd/MM HH:mm} e o último jogo é 13/09 12:10");
    }

    [Fact]
    public void Torneio_sem_jogo_nenhum_ainda_oferece_o_primeiro_dia()
    {
        var slots = HorariosDaGrade.Montar(Torneio(), Array.Empty<Partida>());

        Assert.NotEmpty(slots);
        Assert.Equal(As("2026-09-12 08:00"), slots.First().Horario);
    }

    [Fact]
    public void Onde_o_local_alugado_esta_fechado_cabem_menos_quadras()
    {
        // O Radar só abre no sábado de manhã: no domingo o horário tem 1 quadra, não 2. É a
        // mesma pergunta que a grade e a prévia fazem (SedesDoTorneio.QuadrasAbertasEm) — dizer
        // "2 livres" onde só existe 1 mandaria o organizador pra um lugar fechado.
        var sedes = SedesDoTorneio.Montar(1, 0,
            new[]
            {
                new Quadra { Nome = "Arena 1", ClubeId = 1 },
                new Quadra
                {
                    Nome = "Radar 1",
                    ClubeId = 2,
                    DisponivelDe = new DateTime(2026, 9, 12, 8, 0, 0),
                    DisponivelAte = new DateTime(2026, 9, 12, 12, 10, 0),
                },
            },
            Array.Empty<Categoria>(),
            new Dictionary<int, string> { [1] = "Er Padel", [2] = "Radar" });

        var slots = HorariosDaGrade.Montar(Torneio(quadras: 2), new[] { Jogo("2026-09-13 09:00") }, sedes);

        Assert.Equal(2, slots.Single(s => s.Horario == As("2026-09-12 08:00")).Quadras);
        Assert.Equal(1, slots.Single(s => s.Horario == As("2026-09-13 08:00")).Quadras);
    }

    [Fact]
    public void Os_horarios_previstos_das_eliminatorias_tambem_entram()
    {
        // A prévia (Services/ProximasFasesDaChave) tem hora e é trocável desde 10/09: sem o
        // horário dela na lista, abrir o "Definir horário" numa prévia mostraria o seletor
        // vazio — o mesmo furo do horário quebrado, por outra porta.
        var slots = HorariosDaGrade.Montar(Torneio(), new[] { Jogo("2026-09-12 09:40") },
            tambem: new DateTime?[] { As("2026-09-12 20:13") });

        Assert.Contains(As("2026-09-12 20:13"), slots.Select(s => s.Horario));
    }

    // ═══ O SELETOR NA TELA ═══
    //
    // A view não é renderizada pela suíte (não há Razor aqui), mas ESTA regra não é de aparência:
    // é a diferença entre salvar o que o organizador escolheu e salvar o que ele não viu. Mesmo
    // recurso do ComentarioRazorDentroDeTagTests: ler a fonte e travar a regra.
    private static string ModalDeDefinirHorario()
    {
        var wwwroot = Path.GetDirectoryName(TestInfra.PastaDasFontesDeVerdade())!;
        var view = Path.Combine(Path.GetDirectoryName(wwwroot)!, "Views", "Torneios", "_JogosDoTorneio.cshtml");
        var fonte = File.ReadAllText(view);

        int inicio = fonte.IndexOf("<select name=\"horario\" id=\"definirHorario\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "o seletor de horário do modal 'Definir horário' sumiu da view");
        return fonte[inicio..fonte.IndexOf("</select>", inicio, StringComparison.Ordinal)];
    }

    [Fact]
    public void O_seletor_abre_sem_horario_escolhido_e_exige_a_escolha()
    {
        // 🕳️ Sem uma opção VAZIA, o seletor já nasce com o primeiro horário do torneio marcado:
        // abrir o modal num jogo sem hora e apertar Salvar mandaria o jogo pras 08:00 do primeiro
        // dia sem ninguém ter escolhido nada — e o campo livre de antes, vazio, não fazia isso.
        // Com a opção vazia e o `required`, o navegador exige a escolha, como o campo antigo.
        var seletor = ModalDeDefinirHorario();

        Assert.Contains("required", seletor);
        Assert.Contains("<option value=\"\"", seletor);
    }
}
