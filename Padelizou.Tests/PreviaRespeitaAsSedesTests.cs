using Padelizou.Models;
using Padelizou.Services;
using static Padelizou.Services.ProximasFasesDaChave;

namespace Padelizou.Tests;

// A PRÉVIA ESCOLHE QUADRA COM AS MESMAS RÉGUAS DA GRADE DE VERDADE.
//
// 🗣️ Felipe, 09/09/2026, no print do Er em produção: *"no domingo (dia 13/09) nao tem radar, o
// radar vai ser só no sabado"*. A tela mostrava "Oitavas de Final 3 · Radar · Radar 1" às 08h de
// domingo — com o selo "prévia". A grade de verdade (GradeDeJogos.Encaixar) já respeitava a
// janela da quadra e a trava de clube da categoria; a projeção (ProximasFasesDaChave.Agendar)
// pegava a primeira quadra livre pelo NOME, sem olhar nenhuma das duas, e contava a capacidade
// como se todas as quadras abrissem o dia inteiro.
public class PreviaRespeitaAsSedesTests
{
    private static readonly DateTime Sexta = new(2026, 9, 11);
    private static readonly DateTime Sabado = new(2026, 9, 12);
    private static readonly TimeSpan FimDoDia = new(23, 0, 0);
    private static readonly TimeSpan Abertura = new(8, 0, 0);

    private const int ErPadel = 1;
    private const int Radar = 2;

    private static readonly string[] Quadras =
        { "Arena 1", "Arena 2", "Arena 3", "Arena 4", "Arena 5", "Radar 1", "Radar 2" };

    // O combinado do Er: cinco quadras em casa o torneio inteiro, duas no Radar só na manhã de
    // sábado (08h às 12h10).
    private static SedesDoTorneio Sedes(params Categoria[] categorias) =>
        SedesDoTorneio.Montar(ErPadel, 0,
            Quadras.Select(nome => new Quadra
            {
                Nome = nome,
                ClubeId = nome.StartsWith("Radar") ? Radar : ErPadel,
                DisponivelDe = nome.StartsWith("Radar") ? Sabado.AddHours(8) : null,
                DisponivelAte = nome.StartsWith("Radar") ? Sabado.AddHours(12).AddMinutes(10) : null,
            }),
            categorias,
            new Dictionary<int, string> { [ErPadel] = "Er Padel", [Radar] = "Radar" });

    private static ConfiguracaoDaGrade Grade(SedesDoTorneio sedes) =>
        new(50, Quadras.Length, Quadras, FimDoDia, Abertura, sedes);

    private static readonly string[] OitoGrupos =
        { "Grupo A", "Grupo B", "Grupo C", "Grupo D", "Grupo E", "Grupo F", "Grupo G", "Grupo H" };

    [Fact]
    public void A_previa_nao_poe_jogo_no_local_alugado_fora_da_janela_dele()
    {
        // Grupos fechados às 19h40 de sábado: as oitavas caem na noite de sábado e no domingo,
        // quando o Radar já não existe.
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(OitoGrupos, 2, Sabado.AddHours(19).AddMinutes(40), "6ª");

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia }, Grade(Sedes()));

        Assert.NotEmpty(jogos);
        var noRadar = jogos
            .Where(j => j.Quadra != null && j.Quadra.StartsWith("Radar"))
            .Select(j => $"{j.FaseNumerada} {j.Horario:dd/MM HH:mm} {j.Quadra}")
            .ToList();
        Assert.True(noRadar.Count == 0,
            $"o Radar só abre na manhã de sábado, e a prévia prometeu: {string.Join(", ", noRadar)}");
    }

    [Fact]
    public void Fora_da_janela_a_previa_conta_so_as_quadras_abertas()
    {
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(OitoGrupos, 2, Sabado.AddHours(19).AddMinutes(40), "6ª");

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia }, Grade(Sedes()));

        var lotados = jogos.GroupBy(j => j.Horario).Where(g => g.Count() > 5)
            .Select(g => $"{g.Key:dd/MM HH:mm} com {g.Count()} jogos").ToList();
        Assert.True(lotados.Count == 0,
            $"à noite e no domingo são 5 quadras, e a prévia promete mais: {string.Join(", ", lotados)}");
    }

    // A contrapartida, pra régua não virar "nunca no Radar": dentro da janela ele entra.
    [Fact]
    public void Dentro_da_janela_a_previa_usa_o_local_alugado()
    {
        // Grupos fechados às 23h de sexta (o último horário de começar jogo): as oitavas abrem às 8h
        // de sábado, com o Radar aberto.
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(OitoGrupos, 2, Sexta.AddHours(23), "6ª");

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia }, Grade(Sedes()));

        Assert.Contains(jogos, j => j.Quadra != null && j.Quadra.StartsWith("Radar"));
        Assert.Equal(7, jogos.Count(j => j.Horario == Sabado.AddHours(8)));
    }

    // 🗣️ *"os jogos que vao para o radar sejam das categoria menos fortes [...] para deixar os
    // melhores no clube dele"* — a 4ª está presa em casa, e a prévia tem que saber disso.
    [Fact]
    public void A_previa_nao_manda_categoria_presa_em_casa_pro_local_alugado()
    {
        var quarta = new Categoria { Id = 4, Nome = "4ª Masculina", Codigo = "4M", PodeJogarNaSedeExtra = false };
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(
            OitoGrupos, 2, Sexta.AddHours(23), quarta.Nome, quarta.Id);

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia }, Grade(Sedes(quarta)));

        Assert.NotEmpty(jogos);
        var noRadar = jogos
            .Where(j => j.Quadra != null && j.Quadra.StartsWith("Radar"))
            .Select(j => $"{j.FaseNumerada} {j.Horario:dd/MM HH:mm} {j.Quadra}")
            .ToList();
        Assert.True(noRadar.Count == 0,
            $"a 4ª fica no Er Padel, e a prévia mandou pro Radar: {string.Join(", ", noRadar)}");
        // Cinco em casa às 8h; as outras três escorregam pras 8h50, em vez de ir pro Radar.
        Assert.Equal(5, jogos.Count(j => j.Horario == Sabado.AddHours(8)));
        Assert.Equal(3, jogos.Count(j => j.Horario == Sabado.AddHours(8).AddMinutes(50)));
    }

    // ── O CLUBE DO JOGO PREVISTO (10/09/2026) ────────────────────────────────────────────
    // 🗣️ Felipe, num print do quadro do 2ª Etapa ER PADEL TOUR: *"quartas de final ta sem clube"*.

    [Fact]
    public void O_jogo_previsto_carimba_o_clube_da_quadra_que_ele_recebeu()
    {
        // A contrapartida da régua abaixo, e ela vem primeiro: o clube sai da QUADRA escolhida.
        // Sem este teste, "clube do torneio quando não há quadra" vira "clube do torneio sempre",
        // e a manhã de sábado inteira no Radar apareceria escrita "Er Padel".
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(OitoGrupos, 2, Sexta.AddHours(23), "6ª", 6);

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia }, Grade(Sedes()));

        Assert.Contains(jogos, j => j.Quadra != null && j.Quadra.StartsWith("Radar"));
        Assert.All(jogos, j => Assert.Equal(
            j.Quadra != null && j.Quadra.StartsWith("Radar") ? Radar : ErPadel, j.ClubeId));
    }

    [Fact]
    public void A_previa_com_hora_digitada_na_mao_continua_dizendo_o_clube()
    {
        // "Definir horário na mão" numa prévia grava a reserva com `NomeQuadra = null`
        // (TorneiosController.DefinirHorario: *"hora digitada não traz quadra: o robô escolhe"*),
        // e a partir daí o jogo previsto não tinha quadra NEM clube: o cartão do quadro mostrava
        // só "12/09 18:50", enquanto os cards de grupo ao lado diziam "Radar" e "Er Padel".
        //
        // O clube ele TEM: quando a reserva vira jogo de verdade ela pula o encaixe
        // (RoboDoChaveamento: `paraEncaixar = candidatos.Except(reservados)`) e nasce sem quadra,
        // então `OrdemDeLiberacao.CarimbarOClube` a carimba com o clube do torneio. A prévia diz
        // desde já o que o carimbo vai dizer.
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(
            new[] { "Grupo A", "Grupo B" }, 2, Sabado.AddHours(12), "6ª Feminina", 6);
        var naMao = Sabado.AddHours(18).AddMinutes(50);
        var reservas = new[] { new HorarioReservado(6, "Semifinal", 1, naMao, null) };

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia }, Grade(Sedes()), reservas: reservas);

        var reservado = jogos.Single(j => j.FaseNumerada == "Semifinal 1");
        Assert.Equal(naMao, reservado.Horario);
        Assert.Null(reservado.Quadra);          // o balcão é quem escolhe — a prévia não inventa uma
        Assert.Equal(ErPadel, reservado.ClubeId);
    }

    [Fact]
    public void Sem_nome_de_quadra_sobrando_no_horario_o_jogo_previsto_ainda_diz_o_clube()
    {
        // 🗣️ Felipe, no quadro do Er: *"tem uma parte com e uma sem"* — as oitavas de sábado à
        // noite saíam sem lugar NENHUM, e as quartas do domingo com "Er Padel · Arena Loja 7".
        //
        // 🕳️ A projeção CONTA vagas pelas quadras abertas do cadastro (`QuadrasAbertasEm`) e
        // NOMEIA pela lista de quadras em uso (RoboDoChaveamento.QuadrasEmUsoAsync, que completa o
        // cadastro só até `Torneio.QuantidadeQuadras`). Quando a lista tem menos nomes do que o
        // cadastro tem quadras abertas naquele horário, o jogo cabe na conta e não sobra nome pra
        // ele — nasce com hora e sem quadra, que é o caso que NomesDeQuadra já descreve. Sem
        // quadra, ele ficava sem clube também.
        //
        // Aqui: 5 Arenas abertas à noite no cadastro, mas só 3 nomes de Arena na lista.
        var semTodosOsNomes = new[] { "Arena 1", "Arena 2", "Arena 3", "Radar 1", "Radar 2" };
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(
            OitoGrupos, 2, Sabado.AddHours(19).AddMinutes(40), "6ª", 6);

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia },
            new ConfiguracaoDaGrade(50, Quadras.Length, semTodosOsNomes, FimDoDia, Abertura, Sedes()));

        Assert.Contains(jogos, j => j.Horario != null && j.Quadra == null);
        var semLugar = jogos
            .Where(j => j.Horario != null && j.ClubeId == null)
            .Select(j => $"{j.FaseNumerada} {j.Horario:dd/MM HH:mm}")
            .ToList();
        Assert.True(semLugar.Count == 0,
            $"jogo previsto com hora e sem clube nenhum pra mostrar: {string.Join(", ", semLugar)}");
    }

    // O jogo previsto carrega a categoria: é por ela que a etiqueta escreve "Er Padel" quando a
    // prévia não tem quadra pra dizer (LugarDoJogo.Etiqueta).
    [Fact]
    public void O_jogo_previsto_sabe_de_que_categoria_e()
    {
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(
            new[] { "Grupo A", "Grupo B" }, 2, Sexta.AddHours(23), "4ª Masculina", 4);

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia },
            new ConfiguracaoDaGrade(50, 2, Array.Empty<string>(), FimDoDia, Abertura));

        Assert.All(jogos, j => Assert.Equal(4, j.CategoriaId));
    }
}
