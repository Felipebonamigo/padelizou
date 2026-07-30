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
    public void Escada_de_vitorias_vai_de_10_a_200()
    {
        Assert.Equal(new[] { 10, 25, 50, 100, 150, 200 },
            CatalogoConquistas.EscadaDeVitorias.Select(v => v.Marca));
    }

    [Theory]
    [InlineData(10, 1)]    // só o primeiro degrau
    [InlineData(49, 2)]
    [InlineData(200, 6)]   // a escada inteira
    [InlineData(999, 6)]   // passar do topo não inventa degrau novo
    public void Cada_degrau_de_vitoria_acende_na_marca(int vitorias, int quantosAcesos)
    {
        var codigos = CatalogoConquistas.EscadaDeVitorias.Select(v => v.Codigo).ToHashSet();
        var acesos = CatalogoConquistas.Montar(Ninguem() with { Vitorias = vitorias })
            .Count(c => codigos.Contains(c.Codigo) && c.Conquistada);

        Assert.Equal(quantosAcesos, acesos);
    }

    [Fact]
    public void Escada_de_campeao_vai_de_bi_a_deca_sem_pular_degrau()
    {
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            CatalogoConquistas.EscadaDeCampeao.Select(c => c.Titulos));

        Assert.Equal(
            new[] { "Bicampeão", "Tricampeão", "Tetracampeão", "Pentacampeão", "Hexacampeão",
                    "Heptacampeão", "Octacampeão", "Nonacampeão", "Decacampeão" },
            CatalogoConquistas.EscadaDeCampeao.Select(c => c.Nome));
    }

    [Theory]
    [InlineData(3, "Tricampeao", true)]
    [InlineData(3, "Tetracampeao", false)]
    [InlineData(4, "Tetracampeao", true)]
    [InlineData(10, "Decacampeao", true)]
    [InlineData(9, "Decacampeao", false)]
    public void Cada_titulo_acende_no_seu_numero(int titulos, string codigo, bool esperado)
    {
        Assert.Equal(esperado, Tem(Ninguem() with { Titulos = titulos }, codigo));
    }

    [Fact]
    public void Quem_e_decacampeao_tem_toda_a_escada_atras_acesa()
    {
        // Escada é acumulativa: o decacampeão não perde o troféu de bicampeão pelo caminho.
        var todas = CatalogoConquistas.Montar(Ninguem() with { Titulos = 10, Finais = 1 });
        var daEscada = CatalogoConquistas.EscadaDeCampeao.Select(c => c.Codigo).ToHashSet();

        Assert.All(todas.Where(c => daEscada.Contains(c.Codigo)), c => Assert.True(c.Conquistada));
        Assert.True(todas.Single(c => c.Codigo == "Campeao").Conquistada);
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
    public void O_total_e_as_duas_escadas_mais_as_conquistas_soltas()
    {
        // Eram 12 (3 fileiras exatas de 4). Com as escadas de vitórias e de títulos são 25, e
        // a última fileira sobra com 1 — por isso a grade do perfil ganhou
        // `justify-content-center`. Este número não é decoração: se alguém somar conquista sem
        // olhar a tela, este teste é o aviso.
        var total = CatalogoConquistas.Montar(Ninguem()).Count;

        Assert.Equal(25, total);
        Assert.Equal(6, CatalogoConquistas.EscadaDeVitorias.Length);
        Assert.Equal(9, CatalogoConquistas.EscadaDeCampeao.Length);
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

        foreach (var antigo in new[] { "Estreia", "Mensalista", "Organizador", "DoTime", "Campeao",
                                       "Professor", "Bicampeao", "DezVitorias" })
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
