using Padelizou.Services;
using Xunit;
using Jogo = Padelizou.Services.EliminatoriasNoMesmoHorario.Jogo;

namespace Padelizou.Tests;

// O AVISO NO CHAVEAMENTO: JOGADOR QUE PODE CAIR EM DUAS ELIMINATÓRIAS AO MESMO TEMPO (22/11/2026).
//
// 🗣️ Felipe, depois do Matheus Gerlach na Quartas 1 da 3ª e na Quartas 3 da 4ª, as duas às 22:20 na
// Wallau: *"Tem que prever isso na hora do chaveamento, avisando que existe a possibilidade de tal
// jogador jogar as duas eliminatórias por categorias diferentes no mesmo horário"*.
//
// A régua: pra cada pessoa inscrita em mais de uma categoria, os jogos de mata-mata que PODEM ser
// dela (os reais em que ela está + as prévias do caminho dela, a régua do "só meus jogos") — e
// dois deles, de categorias diferentes, que se sobrepõem no tempo.
public class EliminatoriasNoMesmoHorarioTests
{
    private static readonly DateTime As2220 = new(2026, 11, 21, 22, 20, 0);
    private static readonly TimeSpan Cinquenta = TimeSpan.FromMinutes(50);

    [Fact]
    public void Duas_previas_de_categorias_diferentes_no_mesmo_horario_viram_aviso_com_nome_e_jogos()
    {
        var avisos = EliminatoriasNoMesmoHorario.Possiveis(new[]
        {
            ("Matheus Gerlach", (IReadOnlyList<Jogo>)new[]
            {
                new Jogo("3ª Categoria Masculina", "Quartas de Final 1", As2220, Cinquenta, Real: false),
                new Jogo("4ª Categoria Masculina", "Quartas de Final 3", As2220, Cinquenta, Real: false),
            }),
        });

        var aviso = Assert.Single(avisos);
        Assert.Contains("Matheus Gerlach", aviso);
        Assert.Contains("Quartas de Final 1 da 3ª Categoria Masculina", aviso);
        Assert.Contains("Quartas de Final 3 da 4ª Categoria Masculina", aviso);
        Assert.Contains("21/11 22:20", aviso);
    }

    [Fact]
    public void Jogos_que_nao_se_sobrepoem_nao_avisam()
    {
        var avisos = EliminatoriasNoMesmoHorario.Possiveis(new[]
        {
            ("P", (IReadOnlyList<Jogo>)new[]
            {
                new Jogo("3ª", "Semifinal 1", As2220, Cinquenta, Real: false),
                new Jogo("4ª", "Semifinal 1", As2220 + Cinquenta, Cinquenta, Real: false),
            }),
        });

        Assert.Empty(avisos);
    }

    [Fact]
    public void Sobreposicao_parcial_pela_duracao_da_fase_avisa()
    {
        // A final de 80 min às 22:20 ainda está em quadra às 23:10.
        var avisos = EliminatoriasNoMesmoHorario.Possiveis(new[]
        {
            ("P", (IReadOnlyList<Jogo>)new[]
            {
                new Jogo("3ª", "Final", As2220, TimeSpan.FromMinutes(80), Real: false),
                new Jogo("4ª", "Semifinal 2", As2220 + Cinquenta, Cinquenta, Real: false),
            }),
        });

        Assert.Single(avisos);
    }

    [Fact]
    public void Mesma_categoria_nao_avisa()
    {
        // Duas semifinais da mesma categoria no mesmo horário: a pessoa só joga uma delas.
        var avisos = EliminatoriasNoMesmoHorario.Possiveis(new[]
        {
            ("P", (IReadOnlyList<Jogo>)new[]
            {
                new Jogo("3ª", "Semifinal 1", As2220, Cinquenta, Real: false),
                new Jogo("3ª", "Semifinal 2", As2220, Cinquenta, Real: false),
            }),
        });

        Assert.Empty(avisos);
    }

    [Fact]
    public void Dois_jogos_reais_nao_entram_aqui_porque_ja_sao_o_aviso_de_pessoa_em_dois_jogos()
    {
        var avisos = EliminatoriasNoMesmoHorario.Possiveis(new[]
        {
            ("P", (IReadOnlyList<Jogo>)new[]
            {
                new Jogo("3ª", "Quartas de Final 1", As2220, Cinquenta, Real: true),
                new Jogo("4ª", "Quartas de Final 3", As2220, Cinquenta, Real: true),
            }),
        });

        Assert.Empty(avisos);
    }

    [Fact]
    public void Real_contra_previa_avisa()
    {
        var avisos = EliminatoriasNoMesmoHorario.Possiveis(new[]
        {
            ("P", (IReadOnlyList<Jogo>)new[]
            {
                new Jogo("3ª", "Quartas de Final 1", As2220, Cinquenta, Real: true),
                new Jogo("4ª", "Quartas de Final 3", As2220, Cinquenta, Real: false),
            }),
        });

        Assert.Single(avisos);
    }

    // Teste de FONTE (a suíte não renderiza Razor): o controller entrega a lista pra quem organiza, e
    // a tela de aprovar a chave, a aba Jogos e o Conferir a grade a mostram.
    [Fact]
    public void O_aviso_chega_nas_tres_telas()
    {
        var pasta = PastaDoProjeto();
        var controller = File.ReadAllText(Path.Combine(pasta, "Controllers", "TorneiosController.cs"));
        Assert.Contains("ViewBag.EliminatoriasNoMesmoHorario", controller, StringComparison.Ordinal);

        foreach (var view in new[] { "Details.cshtml", "_JogosDoTorneio.cshtml", "ConferirGrade.cshtml" })
            Assert.Contains("_EliminatoriasNoMesmoHorario",
                File.ReadAllText(Path.Combine(pasta, "Views", "Torneios", view)), StringComparison.Ordinal);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "Padelizou");
    }
}
