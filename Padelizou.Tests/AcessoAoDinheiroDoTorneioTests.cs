using Padelizou.Services;

namespace Padelizou.Tests;

// Quem organiza junto faz tudo do torneio — placar, chaves, configuração, marcar quem já
// acertou — menos ver o dinheiro.
//
// A razão é do dono do torneio, não técnica: ele chama um amigo pra ajudar na mesa no dia do
// jogo, e ajudar na mesa não é abrir o caixa dele. Antes era tudo ou nada: quem entrava pra
// lançar placar via quanto o torneio faturou.
public class AcessoAoDinheiroDoTorneioTests
{
    [Fact]
    public void Quem_criou_o_torneio_ve_o_dinheiro()
    {
        Assert.True(AcessoAoDinheiroDoTorneio.PodeVer("Criador", ehAdminDaPlataforma: false));
    }

    [Theory]
    [InlineData("Organizador")]   // o que a caixinha "Adicionar novo organizador" grava
    [InlineData("Ajudante")]      // o padrão do modelo
    public void Quem_foi_adicionado_pra_ajudar_nao_ve(string nivel)
    {
        Assert.False(AcessoAoDinheiroDoTorneio.PodeVer(nivel, ehAdminDaPlataforma: false));
    }

    [Fact]
    public void Nivel_Total_tambem_ve()
    {
        // Sinônimo de "Criador" que existe numa linha de produção ajustada na mão antes desta
        // regra (torneio 19). Sem aceitar, um organizador de verdade ficaria trancado fora do
        // próprio caixa por causa de uma palavra.
        Assert.True(AcessoAoDinheiroDoTorneio.PodeVer("Total", ehAdminDaPlataforma: false));
    }

    [Fact]
    public void Nivel_desconhecido_NAO_ve()
    {
        // Lista fechada e não o contrário: se um nível novo aparecer amanhã, o erro seguro é
        // ele não ver dinheiro e alguém reclamar — não ele ver e ninguém perceber.
        Assert.False(AcessoAoDinheiroDoTorneio.PodeVer("Mesario", ehAdminDaPlataforma: false));
        Assert.False(AcessoAoDinheiroDoTorneio.PodeVer("", ehAdminDaPlataforma: false));
    }

    [Fact]
    public void Quem_nao_organiza_nada_nao_ve()
    {
        // Vem null quando não existe linha de TorneioOrganizador pra essa pessoa.
        Assert.False(AcessoAoDinheiroDoTorneio.PodeVer(null, ehAdminDaPlataforma: false));
    }

    [Fact]
    public void Admin_do_Padelizou_ve_pra_dar_suporte()
    {
        // Mesma razão pela qual ele já manda em qualquer torneio: no dia do jogo o problema é
        // urgente e "me adiciona como organizador" depende de quem está travado.
        Assert.True(AcessoAoDinheiroDoTorneio.PodeVer(null, ehAdminDaPlataforma: true));
        Assert.True(AcessoAoDinheiroDoTorneio.PodeVer("Ajudante", ehAdminDaPlataforma: true));
    }

    [Theory]
    [InlineData("criador")]
    [InlineData("CRIADOR")]
    [InlineData("  Criador ")]
    public void A_caixa_e_o_espaco_da_palavra_nao_decidem_quem_ve(string nivel)
    {
        // O valor é escrito por código hoje, mas já foi ajustado na mão uma vez — e uma linha
        // com "criador" minúsculo trancaria o dono fora do próprio dinheiro sem explicação.
        Assert.True(AcessoAoDinheiroDoTorneio.PodeVer(nivel, ehAdminDaPlataforma: false));
    }

    [Fact]
    public void Os_avisos_existem_e_dizem_o_que_precisam_dizer()
    {
        // O aviso do Felipe: quem adiciona alguém tem que saber o que está dando e o que não.
        Assert.Contains("placares", AcessoAoDinheiroDoTorneio.AvisoAoAdicionar);
        Assert.Contains("paga", AcessoAoDinheiroDoTorneio.AvisoAoAdicionar);
        Assert.Contains("só com você", AcessoAoDinheiroDoTorneio.AvisoAoAdicionar);

        // E quem ajuda precisa entender por que a tela não tem valor nenhum, senão lê como
        // página quebrada.
        Assert.Contains("marcar quem já acertou", AcessoAoDinheiroDoTorneio.ExplicacaoParaQuemAjuda);
    }
}
