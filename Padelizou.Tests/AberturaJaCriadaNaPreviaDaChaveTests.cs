using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A QUARTAS QUE JÁ NASCEU MOSTRA A HORA DELA NA PRÉVIA DA CHAVE (21/11/2026, Wallau).
//
// 🗣️ Rafael, print da aba Chaves da 4ª Masculina: *"Porque fica 'a definir' o horário?"*. Quartas
// 1 e 3 com hora, Quartas 2 e 4 — as dos grupos que JÁ fecharam, com os nomes das duplas na
// tela — escrito "a definir". Essas duas já eram jogos de verdade (avanço parcial: a abertura
// nasce jogo a jogo, assim que os dois grupos dela fecham), marcados pras 20:40 na aba Jogos.
//
// A cadeia: enquanto a categoria tem grupo em aberto, a aba desenha a PRÉVIA por colocação, e a
// hora de cada vaga sai da projeção (`ProjecaoCompleta`). A projeção, de propósito, só promete o
// que FALTA (RoboDoChaveamento: `aberturaJaCriada`) — o jogo já criado não está nela. Então a
// vaga do jogo REAL era a única do quadro sem hora nenhuma.
public class AberturaJaCriadaNaPreviaDaChaveTests
{
    private static Partida Quartas(int id, int? numero, DateTime? hora, string? quadra = null) => new()
    {
        Id = id, CategoriaId = 7, Fase = "Quartas de Final", NumeroNaFase = numero,
        HorarioPrevisto = hora, NomeQuadra = quadra, ClubeId = 3, Status = "Agendada",
    };

    [Fact]
    public void Jogo_ja_criado_da_abertura_vira_a_hora_e_a_quadra_da_vaga_dele()
    {
        var as2040 = new DateTime(2026, 11, 21, 20, 40, 0);
        var reais = new[] { Quartas(997, 4, as2040, "Quadra 2"), Quartas(996, 2, as2040, "Quadra 1") };

        var porVaga = AberturaJaCriada.PorVaga(reais, "4ª Categoria Masculina");

        var q4 = porVaga[("Quartas de Final", 4)];
        Assert.Equal(as2040, q4.Horario);
        Assert.Equal("Quadra 2", q4.Quadra);
        Assert.Equal(7, q4.CategoriaId);
        Assert.Equal(3, q4.ClubeId);
        Assert.Equal("Quadra 1", porVaga[("Quartas de Final", 2)].Quadra);
    }

    [Fact]
    public void Numero_da_vaga_e_o_do_quadro_e_nao_a_ordem_de_criacao()
    {
        // A Quartas 4 nasceu ANTES da 2 (o grupo dela fechou primeiro): por Id ela seria a "1".
        var reais = new[] { Quartas(10, 4, new DateTime(2026, 11, 21, 20, 40, 0)),
                            Quartas(11, 2, new DateTime(2026, 11, 21, 21, 30, 0)) };

        var porVaga = AberturaJaCriada.PorVaga(reais, "X");

        Assert.Equal(new DateTime(2026, 11, 21, 20, 40, 0), porVaga[("Quartas de Final", 4)].Horario);
        Assert.False(porVaga.ContainsKey(("Quartas de Final", 1)));
    }

    [Fact]
    public void Jogo_de_grupo_nao_entra()
    {
        var grupo = new Partida { Id = 1, CategoriaId = 7, Fase = "Grupo A", HorarioPrevisto = DateTime.Today };

        Assert.Empty(AberturaJaCriada.PorVaga(new[] { grupo }, "X"));
    }

    // Teste de FONTE (a suíte não renderiza Razor): a prévia da aba Chaves completa o casamento
    // com o jogo já criado, e o controller entrega esses jogos pra ela.
    [Fact]
    public void A_previa_da_aba_chaves_usa_o_jogo_ja_criado_quando_a_projecao_nao_tem_a_vaga()
    {
        var pasta = PastaDoProjeto();
        var view = File.ReadAllText(Path.Combine(pasta, "Views", "Torneios", "Details.cshtml"));
        var inicio = view.IndexOf("var previstosDoQuadro", StringComparison.Ordinal);
        var fim = view.IndexOf("<partial name=\"_ChaveDoMataMata\"", inicio, StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        Assert.Contains("aberturaJaCriada.GetValueOrDefault((rodada.Fase, i + 1))", view[inicio..fim], StringComparison.Ordinal);
        var montagem = view.LastIndexOf("var rodadasProjetadas", inicio, StringComparison.Ordinal);
        Assert.True(montagem >= 0);
        Assert.Contains("AberturaJaCriada.PorVaga", view[montagem..inicio], StringComparison.Ordinal);

        var controller = File.ReadAllText(Path.Combine(pasta, "Controllers", "TorneiosController.cs"));
        Assert.Contains("ViewBag.AberturaJaCriadaPorCategoria", controller, StringComparison.Ordinal);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "Padelizou");
    }
}
