using Padelizou.Services;

namespace Padelizou.Tests;

// "De onde vem quem se cadastra?" só vira resposta útil se as quatro grafias do Rio Grande do
// Sul contarem como um estado, e as três de Gravataí como uma cidade. Contando por igualdade
// exata de texto, a tela abriria bonita e mentiria pra menos — que é o defeito mais caro,
// porque não parece defeito nenhum.
public class JogadoresPorRegiaoTests
{
    private static readonly DateTime Hoje = new(2026, 8, 11, 10, 0, 0);

    private static PessoaNaRegiao P(string? cidade, string? estado) => new(cidade, estado, Hoje);

    // ⚠️ Sobrecarga, e não um parâmetro opcional com `em ?? Hoje`: `null` aqui é o caso que
    // mais importa testar (conta anterior a 25/07/2026, sem data), e o valor padrão o
    // engoliria — o teste passaria verde sem nunca ter exercitado o que diz exercitar.
    private static PessoaNaRegiao P(string? cidade, string? estado, DateTime? em) => new(cidade, estado, em);

    private static GrupoDeRegiao Achar(IEnumerable<GrupoDeRegiao> grupos, string rotulo) =>
        grupos.Single(g => g.Rotulo == rotulo);

    // ---------- Estado ----------

    [Fact]
    public void As_quatro_grafias_do_Rio_Grande_do_Sul_sao_um_estado_so()
    {
        // Exatamente o que a base de produção tinha em 10/08/2026. Comparando na lata, o RS
        // apareceria em quatro linhas e nenhuma delas seria o tamanho real do estado.
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, new[]
        {
            P("Gravataí", "RS"), P("Canoas", "Rs"), P("Porto Alegre", "rs"),
            P("Viamão", "Rio Grande do Sul (RS)"),
            P("São Paulo", "SP"),
        });

        Assert.Equal(2, grupos.Count);
        Assert.Equal(4, Achar(grupos, "RS").Total);
        Assert.Equal(1, Achar(grupos, "SP").Total);
    }

    [Fact]
    public void Quem_nao_escreveu_a_UF_herda_a_do_resto_da_propria_cidade()
    {
        // 44 das 172 contas ativas não tinham estado nenhum. Sem esta regra, um quarto da base
        // cairia em "não informado" e a tela por estado não decidiria nada.
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, new[]
        {
            P("Gravataí", "RS"),
            P("GRAVATAI", null),      // mesma cidade, escrita de outro jeito, sem UF
            P("gravatai", ""),
        });

        var rs = Assert.Single(grupos);
        Assert.Equal("RS", rs.Rotulo);
        Assert.Equal(3, rs.Total);
    }

    [Fact]
    public void Cidade_com_duas_UFs_discordando_nao_e_herdada_por_ninguem()
    {
        // São Francisco existe em meia dúzia de estados. Duas UFs brigando é "não sei" — e
        // "não sei" não vira chute, senão a tela inventa um estado pra pessoa.
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, new[]
        {
            P("São Francisco", "MG"),
            P("São Francisco", "SP"),
            P("São Francisco", null),
        });

        Assert.Equal(1, Achar(grupos, "MG").Total);
        Assert.Equal(1, Achar(grupos, "SP").Total);
        Assert.Equal(1, Achar(grupos, JogadoresPorRegiao.RotuloSemRegiao).Total);
    }

    [Fact]
    public void Quem_nao_disse_onde_mora_vira_uma_linha_com_nome_e_numero()
    {
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, new[]
        {
            P("Canoas", "RS"), P(null, null), P("", "  "),
        });

        // O buraco da medição aparece — some-lo faria a tela mentir sobre o tamanho da base.
        var sem = Achar(grupos, JogadoresPorRegiao.RotuloSemRegiao);
        Assert.True(sem.NaoInformada);
        Assert.Equal(2, sem.Total);
    }

    [Fact]
    public void O_nao_informado_fica_no_fim_por_maior_que_seja()
    {
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, new[]
        {
            P(null, null), P(null, null), P(null, null),
            P("Canoas", "RS"),
        });

        Assert.Equal("RS", grupos[0].Rotulo);
        Assert.Equal(JogadoresPorRegiao.RotuloSemRegiao, grupos[1].Rotulo);
    }

    // ---------- Cidade ----------

    [Fact]
    public void As_tres_grafias_de_Gravatai_sao_uma_cidade_so_e_a_melhor_vai_pra_tela()
    {
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorCidade, new[]
        {
            P("GRAVATAI", "RS"), P("Gravataí", "RS"), P("gravatai", "RS"),
            P("Canoas", "RS"),
        });

        Assert.Equal(2, grupos.Count);

        // Quem escreveu certo escreve por todo mundo (a escolha mora em CidadesSemRepetir).
        var gravatai = Achar(grupos, "Gravataí - RS");
        Assert.Equal(3, gravatai.Total);
    }

    [Fact]
    public void Homonimas_de_estados_diferentes_continuam_separadas()
    {
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorCidade, new[]
        {
            P("São Francisco", "MG"), P("São Francisco", "SP"),
        });

        Assert.Equal(2, grupos.Count);
        Assert.Equal(1, Achar(grupos, "São Francisco - MG").Total);
        Assert.Equal(1, Achar(grupos, "São Francisco - SP").Total);
    }

    [Fact]
    public void Sem_cidade_e_uma_linha_so_mesmo_com_estados_diferentes()
    {
        // Sem cidade não é região nenhuma, é campo vazio — separar por UF criaria "não
        // informado (RS)" e "não informado (SP)", duas linhas que não são dois lugares.
        var grupos = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorCidade, new[]
        {
            P(null, "RS"), P("", "SP"), P(null, null),
        });

        var sem = Assert.Single(grupos);
        Assert.Equal(JogadoresPorRegiao.RotuloSemRegiao, sem.Rotulo);
        Assert.Equal(3, sem.Total);
    }

    [Fact]
    public void A_chave_da_URL_nao_muda_quando_a_proxima_pessoa_digita_diferente()
    {
        // O link que o admin guardar tem que continuar caindo na mesma cidade depois que
        // alguém se cadastrar escrevendo "GRAVATAI".
        var so = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorCidade, new[] { P("Gravataí", "RS") });
        var depois = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorCidade, new[]
        {
            P("Gravataí", "RS"), P("GRAVATAI", "RS"),
        });

        Assert.Equal("gravatai-rs", so[0].Chave);
        Assert.Equal(so[0].Chave, depois[0].Chave);
    }

    // ---------- A série ----------

    [Fact]
    public void Cada_regiao_conta_so_quem_entrou_dentro_da_fatia()
    {
        var grupo = Assert.Single(JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, new[]
        {
            P("Canoas", "RS", new DateTime(2026, 8, 10, 23, 59, 0)),
            P("Canoas", "RS", new DateTime(2026, 8, 11, 0, 0, 0)),
            P("Canoas", "RS", new DateTime(2026, 8, 11, 23, 59, 0)),
            P("Canoas", "RS", new DateTime(2026, 8, 12, 0, 0, 0)),
        }));

        // O dia 11 inteiro, e nada de antes nem de depois: o fim é exclusivo.
        Assert.Equal(2, grupo.Entre(new DateTime(2026, 8, 11), new DateTime(2026, 8, 12)));
    }

    [Fact]
    public void Cadastro_antigo_sem_data_conta_no_total_e_em_fatia_nenhuma()
    {
        // `CriadoEm` nulo é conta anterior a 25/07/2026, quando a coluna não existia. Sem
        // contar separado, a cidade apareceria com 3 jogadores e nenhum cadastro na série —
        // e isso pareceria defeito.
        var grupo = Assert.Single(JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, new[]
        {
            P("Canoas", "RS", null), P("Canoas", "RS", null),
            P("Canoas", "RS", Hoje),
        }));

        Assert.Equal(3, grupo.Total);
        Assert.Equal(2, grupo.SemData);
        Assert.Equal(1, grupo.Entre(Hoje.Date, Hoje.Date.AddDays(1)));
    }

    // ---------- Entrada torta ----------

    [Theory]
    [InlineData(null, JogadoresPorRegiao.PorEstado)]
    [InlineData("", JogadoresPorRegiao.PorEstado)]
    [InlineData("bairro", JogadoresPorRegiao.PorEstado)]
    [InlineData("CIDADE", JogadoresPorRegiao.PorCidade)]
    [InlineData("  cidade  ", JogadoresPorRegiao.PorCidade)]
    public void Recorte_desconhecido_cai_no_estado(string? entrada, string esperado) =>
        Assert.Equal(esperado, JogadoresPorRegiao.Normalizar(entrada));

    [Fact]
    public void Base_vazia_nao_estoura()
    {
        Assert.Empty(JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, Array.Empty<PessoaNaRegiao>()));
        Assert.Empty(JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorCidade, Array.Empty<PessoaNaRegiao>()));
    }

    [Fact]
    public void O_rodape_conta_quantas_UFs_foram_deduzidas()
    {
        // Número deduzido tem que se apresentar como deduzido — senão a tela afirma o que
        // ninguém escreveu.
        var pessoas = new[]
        {
            P("Gravataí", "RS"),      // escreveu
            P("GRAVATAI", null),      // herdou
            P("gravatai", ""),        // herdou
            P(null, null),            // não dá pra saber
        };

        Assert.Equal(2, JogadoresPorRegiao.QuantasHerdaramAUfDaCidade(pessoas));
    }
}
