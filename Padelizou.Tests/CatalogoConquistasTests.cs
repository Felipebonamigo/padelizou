using Padelizou.Services;

namespace Padelizou.Tests;

// As conquistas são calculadas na hora a partir do que o jogador já fez — não há tabela no
// banco, então nunca há conquista "esquecida de dar". A regra mora no CatalogoConquistas,
// puro; aqui cada degrau é conferido nos dois lados do limiar.
public class CatalogoConquistasTests
{
    private static DadosParaConquistas Ninguem() => new(
        JogouAlgumaVez: false, JogosSemanais: 0, EhOrganizador: false, TemTime: false,
        EhProfessor: false, Titulos: 0, Finais: 0, TotalTorneios: 0, Vitorias: 0,
        ElogiosRecebidos: 0, AulasComoAluno: 0);

    private static bool Tem(DadosParaConquistas d, string codigo) =>
        CatalogoConquistas.Montar(d).Single(c => c.Codigo == codigo).Conquistada;

    [Fact]
    public void Conta_nova_nao_tem_conquista_nenhuma()
    {
        Assert.DoesNotContain(CatalogoConquistas.Montar(Ninguem()), c => c.Conquistada);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, false)]
    public void Mensalista_pede_4_jogos_fixos(int jogos, bool esperado)
    {
        Assert.Equal(esperado, Tem(Ninguem() with { JogosSemanais = jogos }, "Mensalista"));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    public void Veterano_pede_5_torneios(int torneios, bool esperado)
    {
        Assert.Equal(esperado, Tem(Ninguem() with { TotalTorneios = torneios }, "Veterano"));
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(9, false)]
    public void Dez_vitorias_pede_exatamente_dez(int vitorias, bool esperado)
    {
        Assert.Equal(esperado, Tem(Ninguem() with { Vitorias = vitorias }, "DezVitorias"));
    }

    [Fact]
    public void Quem_foi_campeao_tambem_e_finalista()
    {
        // O resumo conta "Finais" como finais PERDIDAS em alguns fluxos — mas ninguém vira
        // campeão sem ter chegado à final. Campeão sem o selo de Finalista seria um perfil
        // dizendo que a pessoa nunca chegou onde ela venceu.
        var campeao = Ninguem() with { Titulos = 1, Finais = 0 };

        Assert.True(Tem(campeao, "Finalista"));
        Assert.True(Tem(campeao, "Campeao"));
        Assert.False(Tem(campeao, "Bicampeao"));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public void Bicampeao_pede_2_titulos(int titulos, bool esperado)
    {
        Assert.Equal(esperado, Tem(Ninguem() with { Titulos = titulos }, "Bicampeao"));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    public void Querido_da_quadra_pede_5_elogios(int elogios, bool esperado)
    {
        Assert.Equal(esperado, Tem(Ninguem() with { ElogiosRecebidos = elogios }, "QueridoDaQuadra"));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(2, false)]
    public void Aluno_aplicado_pede_3_aulas_realizadas(int aulas, bool esperado)
    {
        Assert.Equal(esperado, Tem(Ninguem() with { AulasComoAluno = aulas }, "AlunoAplicado"));
    }

    [Fact]
    public void Sao_12_conquistas_para_fechar_a_grade_de_4_por_fileira()
    {
        // A grade do perfil é col-3 (4 por fileira). 12 fecha 3 fileiras exatas; 13 deixaria
        // uma sobra solta — quem adicionar a 13ª precisa decidir isso de propósito.
        Assert.Equal(12, CatalogoConquistas.Montar(Ninguem()).Count);
    }

    [Fact]
    public void Codigos_unicos_e_toda_conquista_explica_como_destravar()
    {
        var todas = CatalogoConquistas.Montar(Ninguem());

        Assert.Equal(todas.Count, todas.Select(c => c.Codigo).Distinct().Count());
        // Conquista bloqueada é uma meta: sem descrição ela é só um badge cinza mudo.
        Assert.All(todas, c => Assert.False(string.IsNullOrWhiteSpace(c.Descricao)));
        Assert.All(todas, c => Assert.False(string.IsNullOrWhiteSpace(c.Icone)));
    }

    [Fact]
    public void Os_codigos_antigos_continuam_existindo()
    {
        // "Estreia", "Mensalista", "Organizador", "DoTime", "Campeao" e "Professor" já
        // estavam no ar antes da ampliação — sumir com um deles apagaria conquista que
        // alguém já viu no próprio perfil.
        var codigos = CatalogoConquistas.Montar(Ninguem()).Select(c => c.Codigo).ToList();

        foreach (var antigo in new[] { "Estreia", "Mensalista", "Organizador", "DoTime", "Campeao", "Professor" })
            Assert.Contains(antigo, codigos);
    }

    [Fact]
    public void Catalogo_de_elogios_nao_tem_codigo_repetido_e_todos_resolvem()
    {
        Assert.Equal(CatalogoElogios.Todos.Count,
            CatalogoElogios.Todos.Select(t => t.Codigo).Distinct().Count());

        // Obter é o que o controller usa pra validar o POST — todo tipo do catálogo
        // precisa se encontrar.
        Assert.All(CatalogoElogios.Todos, t => Assert.NotNull(CatalogoElogios.Obter(t.Codigo)));
    }
}
