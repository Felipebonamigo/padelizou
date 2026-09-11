using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — A PRÉVIA DO MATA-MATA DESENHA A CHAVE DE DOIS JEITOS, E OS DOIS PRECISAM FICAR.
//
// 🗣️ Felipe, escolhendo entre três maquetes: a chave com as linhas, *"no pc, é de cima pra baixo
// e no mobile é arrastavel da esquerda pra direita"* — o desenho da chave da Libertadores no
// Google, com a rodada atual ocupando dois terços da tela e a seguinte espiando na beirada.
//
// ⚠️ POR QUE ISTO É UM TESTE, e não só um comentário na view: são DOIS blocos de marcação num
// arquivo só, cada um escondido por largura. Apagar um deles (num "limpar duplicação", que é
// exatamente o que os dois parecem de relance) deixa metade dos usuários sem quadro nenhum, e
// NENHUM outro teste da suíte fica vermelho — a suíte não renderiza Razor e o C# não sabe de
// largura de tela. Este arquivo é a única coisa entre esse refactor e a tela em branco.
//
// 📌 FALSIFICADOS ANTES DE ENTRAR (nascem verdes, então precisam provar que discriminam):
//   · tirando `d-none d-md-block` do partial → "o quadro de cima pra baixo sumiu";
//   · tirando o bloco `pdz-chd` → "a chave deitada do celular sumiu";
//   · devolvendo o sufixo "(passou direto)" ao rótulo → o teste do selo reprova;
//   · tirando `scroll-snap-type` do trilho → "o trilho do celular perdeu o encaixe".
public class QuadroDaPreviaTemOsDoisDesenhosTests
{
    private static string Partial() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios",
                                      "_ChaveProjetadaArvore.cshtml"));

    private static string Css() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

    [Fact]
    public void No_computador_a_chave_desce_de_cima_pra_baixo()
    {
        var fonte = Partial();

        Assert.Contains("pdz-arv-rolagem d-none d-md-block", fonte);
        Assert.Contains("--pdz-arv-colunas:", fonte);
        Assert.Contains("grid-column:", fonte);
    }

    [Fact]
    public void No_celular_a_chave_deita_e_arrasta()
    {
        var fonte = Partial();

        Assert.Contains("pdz-chd d-md-none", fonte);
        // O que no computador é coluna, aqui é linha — é a mesma conta com o eixo trocado.
        Assert.Contains("--pdz-chd-linhas:", fonte);
        Assert.Contains("grid-row:", fonte);
    }

    [Fact]
    public void O_trilho_do_celular_encaixa_a_rodada_seguinte()
    {
        var css = Css();
        var inicio = css.IndexOf(".pdz-chd-trilho {", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei a regra .pdz-chd-trilho.");
        var bloco = css[inicio..css.IndexOf('}', inicio)];

        // Sem o encaixe, o arrasto para no meio de uma rodada e o quadro vira uma tira solta.
        Assert.Contains("scroll-snap-type: x mandatory", bloco);

        // ⚠️ 66%, e não 100%: é a beirada da rodada seguinte que responde "ganhei, e agora?"
        // sem ninguém tocar em nada. Uma rodada por tela cheia perde exatamente isso.
        var rodada = css[css.IndexOf(".pdz-chd-rodada {", StringComparison.Ordinal)..];
        Assert.Contains("flex: 0 0 66%", rodada[..rodada.IndexOf('}', StringComparison.Ordinal)]);
    }

    [Fact]
    public void Quem_passou_direto_aparece_so_pelo_nome()
    {
        // 🗣️ Felipe: *"só não precisa colocar que folgou na primeira rodada"*. Na árvore quem
        // conta isso é a AUSÊNCIA de linha chegando na vaga — o selo era a mesma informação
        // escrita duas vezes. O sufixo continua existindo em ChaveProjetada pra quem lê a
        // prévia em lista, onde não há linha nenhuma pra explicar.
        Assert.Contains("\" (passou direto)\", \"\"", Partial());
    }

    [Fact]
    public void A_final_fecha_o_caminho_com_destaque()
    {
        var css = Css();

        // ⚠️ DUAS CLASSES NO SELETOR: `.pdz-chave-vaga` define `border` e vem depois no arquivo,
        // então `.pdz-arv-final` sozinha perderia a disputa de especificidade e o destaque
        // sumiria sem nenhum teste vermelho. Foi assim que ele sumiu na primeira tentativa.
        Assert.Contains(".pdz-chave-vaga.pdz-arv-final {", css);
        Assert.Contains("pdz-arv-final", Partial());
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
