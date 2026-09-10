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
