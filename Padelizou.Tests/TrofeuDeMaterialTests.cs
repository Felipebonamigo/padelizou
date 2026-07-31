using Padelizou.Services;

namespace Padelizou.Tests;

// O troféu é feito do material da categoria: diamante > ouro > prata > bronze > ferro >
// madeira > plástico, e vidro pras mistas (fora da escada de propósito). Antes era o mesmo
// desenho pra todos, com o fundo da pílula mudando de cor.
public class TrofeuDeMaterialTests
{
    [Theory]
    [InlineData("Categoria Open Masculino", "Diamante")]
    [InlineData("2ª Categoria Masculina", "Ouro")]
    [InlineData("3ª Categoria Feminina", "Prata")]
    [InlineData("4ª Categoria Masculina", "Bronze")]
    [InlineData("5ª Categoria Masculina", "Ferro")]
    [InlineData("6ª Categoria Feminina", "Madeira")]
    [InlineData("7ª Categoria Masculina", "Plastico")]
    [InlineData("Categoria Iniciantes Feminina", "Plastico")]
    public void Cada_categoria_do_catalogo_tem_seu_material(string categoria, string esperado)
    {
        Assert.Equal(esperado, TrofeuDeMaterial.Do(categoria).Chave);
    }

    [Theory]
    [InlineData("Categoria Mista A")]
    [InlineData("Categoria Mista D")]
    [InlineData("Torneio Misto de Verão")]
    public void As_mistas_ganham_vidro_e_ficam_fora_da_escada(string categoria)
    {
        // Vidro é material de troféu de verdade e NÃO entra na fila metálica: misto não é
        // "melhor" nem "pior" que uma numerada, e encaixá-lo obrigaria a responder uma
        // pergunta que ninguém fez.
        var m = TrofeuDeMaterial.Do(categoria);

        Assert.Equal("Vidro", m.Chave);
        Assert.Equal(0, m.Forca);
    }

    [Fact]
    public void Primeira_categoria_entra_junto_com_a_Open()
    {
        // Pela régua de força do próprio sistema a "1ª" é a segunda mais forte que existe;
        // sem esta linha o campeão dela saía com a taça genérica cinza.
        Assert.Equal("Diamante", TrofeuDeMaterial.Do("1ª Categoria Masculina").Chave);
    }

    [Fact]
    public void Nome_fora_do_padrao_cai_no_generico_sem_estourar()
    {
        // Nome de categoria é texto livre — o organizador escreve o que quiser.
        Assert.Equal("Geral", TrofeuDeMaterial.Do("Torneio dos Amigos").Chave);
        Assert.Equal("Geral", TrofeuDeMaterial.Do(null).Chave);
        Assert.Equal("Geral", TrofeuDeMaterial.Do("").Chave);
    }

    [Fact]
    public void A_escada_de_forca_esta_na_ordem_certa()
    {
        var forca = (string cat) => TrofeuDeMaterial.Do(cat).Forca;

        Assert.True(forca("Categoria Open Masculino") > forca("2ª Categoria Masculina"));
        Assert.True(forca("2ª Categoria Masculina") > forca("3ª Categoria Masculina"));
        Assert.True(forca("3ª Categoria Masculina") > forca("4ª Categoria Masculina"));
        Assert.True(forca("4ª Categoria Masculina") > forca("5ª Categoria Masculina"));
        Assert.True(forca("5ª Categoria Masculina") > forca("6ª Categoria Masculina"));
        Assert.True(forca("6ª Categoria Masculina") > forca("7ª Categoria Masculina"));
    }

    // ---------- A prateleira ----------

    [Fact]
    public void So_conta_campeonato_e_agrupa_por_categoria()
    {
        var campanhas = new (string?, string?)[]
        {
            ("2ª Categoria Masculina", "Campeao"),
            ("2ª Categoria Masculina", "Campeao"),  // bicampeão da MESMA categoria: soma junto
            ("2ª Categoria Feminina", "Campeao"),   // mesmo material, taça separada
            ("3ª Categoria Masculina", "Campeao"),
            ("2ª Categoria Masculina", "Final"),    // vice não é troféu
            ("Categoria Open Masculino", "Semifinal"),
        };

        var prateleira = TrofeuDeMaterial.Contar(campanhas);

        // Três taças: as duas de ouro não viram uma só, porque o nome da categoria é
        // justamente o que o jogador quer mostrar.
        Assert.Equal(3, prateleira.Count);

        Assert.Equal("2ª Categoria Masculina", prateleira[0].Categoria);
        Assert.Equal("Ouro", prateleira[0].Material.Chave);
        Assert.Equal(2, prateleira[0].Titulos);

        Assert.Equal("2ª Categoria Feminina", prateleira[1].Categoria);
        Assert.Equal("Ouro", prateleira[1].Material.Chave);
        Assert.Equal(1, prateleira[1].Titulos);

        Assert.Equal("3ª Categoria Masculina", prateleira[2].Categoria);
        Assert.Equal("Prata", prateleira[2].Material.Chave);
    }

    [Fact]
    public void Mesma_categoria_escrita_diferente_conta_junto()
    {
        // O nome vem digitado pelo organizador; um espaço sobrando não pode partir o
        // bicampeonato em duas taças de 1×.
        var prateleira = TrofeuDeMaterial.Contar(new (string?, string?)[]
        {
            ("2ª Categoria Masculina", "Campeao"),
            (" 2ª CATEGORIA MASCULINA ", "Campeao"),
        });

        Assert.Single(prateleira);
        Assert.Equal(2, prateleira[0].Titulos);
        Assert.Equal("2ª Categoria Masculina", prateleira[0].Categoria); // a 1ª grafia vista
    }

    [Fact]
    public void O_mais_forte_vem_primeiro_e_o_vidro_fecha_a_fila()
    {
        var campanhas = new (string?, string?)[]
        {
            ("Categoria Mista A", "Campeao"),
            ("6ª Categoria Masculina", "Campeao"),
            ("Categoria Open Masculino", "Campeao"),
        };

        var ordem = TrofeuDeMaterial.Contar(campanhas).Select(t => t.Material.Chave).ToList();

        Assert.Equal(new[] { "Diamante", "Madeira", "Vidro" }, ordem);
    }

    [Fact]
    public void Sem_titulo_nenhum_a_prateleira_fica_vazia()
    {
        // Prateleira com sete taças vazias diria "esse jogador não ganhou nada" de um jeito
        // pior que não ter prateleira: quem nunca foi campeão simplesmente não vê a seção.
        var campanhas = new (string?, string?)[]
        {
            ("2ª Categoria Masculina", "Final"),
            ("3ª Categoria Masculina", "Grupos"),
        };

        Assert.Empty(TrofeuDeMaterial.Contar(campanhas));
        Assert.Empty(TrofeuDeMaterial.Contar(Array.Empty<(string?, string?)>()));
    }

    [Fact]
    public void A_pilula_das_listas_usa_a_mesma_regra_do_trofeu()
    {
        // Duas cópias da regra divergiriam no dia em que uma categoria nova entrasse: a lista
        // mostraria um material e o perfil, outro. TierDaCategoria delega pra cá.
        foreach (var cat in new[] { "Categoria Open Masculino", "2ª Categoria Masculina",
                                    "Categoria Mista B", "1ª Categoria Feminina", "Sei lá" })
        {
            var tier = EstatisticasService.TierDaCategoria(cat);
            var material = TrofeuDeMaterial.Do(cat);

            Assert.Equal(material.Chave, tier.Chave);
            Assert.Equal(material.Nome, tier.Nome);
            Assert.Equal(material.CorFundo, tier.CorFundo);
            Assert.Equal(material.CorTexto, tier.CorTexto);
        }
    }

    [Fact]
    public void Todo_tier_e_taca_inclusive_o_diamante()
    {
        // O diamante já foi desenhado como pedra lapidada e destoava dos outros sete: na
        // prateleira parecia sobra de uma versão anterior. Agora o material aparece na COR,
        // nunca num formato diferente.
        foreach (var cat in new[] { "Categoria Open Masculino", "1ª Categoria Feminina",
                                    "2ª Categoria Masculina", "Categoria Mista A", "Sei lá" })
        {
            Assert.Equal("bi-trophy-fill", EstatisticasService.TierDaCategoria(cat).Icone);
        }
    }
}
