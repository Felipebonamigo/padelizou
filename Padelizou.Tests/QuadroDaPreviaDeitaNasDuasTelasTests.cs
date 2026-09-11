using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — A PRÉVIA DO MATA-MATA DEITA NAS DUAS TELAS, E O CARTÃO TEM TAMANHO.
//
// 🗣️ Felipe, com o print da prévia de 2 grupos ocupando a largura inteira do monitor:
// *"ficou muito esticado, nao tem pq e fica feio, deixe em um tamanho normal que fique bonito"*
// e, logo depois, *"Talvez seja melhor fazer tambem da esquerda pra direita, ao inves de cima
// para baixo, ficará mais alinhado"*.
//
// ⚠️ ISTO REVOGA A ESCOLHA DE HORAS ANTES ("no pc é de cima pra baixo, no mobile deitado"), que
// este mesmo arquivo travava. O desenho de cima pra baixo era uma grade de N COLUNAS, N = jogos
// da rodada mais larga — com 2 grupos são 2 colunas, cada uma metade do monitor: cartão de
// ~870px pra caber "1º do Grupo A". Era daí que vinha o esticado, e não de um `width` esquecido.
//
// ✅ AGORA É UM DESENHO SÓ, deitado da esquerda pra direita nos dois tamanhos: a rodada é uma
// COLUNA de largura fixa, e o número de rodadas (3, 4, 5) não depende do tamanho da tela. O que
// muda por largura é só o comportamento do trilho — encaixe e arrasto no celular, nada disso no
// computador.
//
// 📏 17rem POR RODADA, MEDIDO NO CHROMIUM COM O `site.css` REAL a 1440px, e não escolhido no
// olho — a linha `.pdz-chave-quando` é `nowrap; overflow: hidden` SEM reticências, então cartão
// apertado come o nome do clube calado (ver DiaDaSemanaNaoCabeNaArvoreDaChaveTests):
//
//   13rem → cartão 200px, "Er Padel · Arena Loja 7" perde 51px      ❌
//   15rem → cartão 232px, perde 19px                                 ❌
//   16rem → cartão 248px, perde 3px                                  ❌
//   17rem → cartão 264px, perde 0 — os 15 cartões da chave de 16     ✅
//
// ⚠️ POR QUE ISTO É UM TESTE, e não um comentário na view: nada na suíte renderiza Razor nem
// sabe de largura de tela. Sem estas travas, "limpar" o `@media` devolve o cartão de 870px e
// NENHUM outro teste fica vermelho.
//
// 📌 FALSIFICADOS ANTES DE ENTRAR:
//   · devolvendo o `d-md-none` ao bloco `pdz-chd` → "a prévia sumiu do computador";
//   · tirando o `@media` do computador → "a rodada voltou a esticar";
//   · tirando `scroll-snap-type` do trilho → "o trilho do celular perdeu o encaixe";
//   · devolvendo o sufixo "(passou direto)" ao rótulo → o teste do selo reprova.
public class QuadroDaPreviaDeitaNasDuasTelasTests
{
    private static string Partial() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios",
                                      "_ChaveProjetadaArvore.cshtml"));

    private static string Css() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

    [Fact]
    public void A_previa_e_um_desenho_so_deitado_da_esquerda_pra_direita()
    {
        var fonte = Partial();

        // O quadro vale pras duas telas: sem `d-md-none` no bloco, e sem um segundo bloco
        // escondido por largura pra divergir dele.
        Assert.Contains("class=\"pdz-chd\"", fonte);
        Assert.DoesNotContain("pdz-chd d-md-none", fonte);
        Assert.DoesNotContain("pdz-arv-rolagem", fonte);

        // A rodada é COLUNA e a vaga ocupa LINHAS — é a conta de ArvoreDaChave com o eixo
        // deitado. `grid-column:` era a marca do desenho de cima pra baixo.
        Assert.Contains("--pdz-chd-linhas:", fonte);
        Assert.Contains("grid-row:", fonte);
        Assert.DoesNotContain("grid-column:", fonte);
    }

    [Fact]
    public void No_computador_a_rodada_tem_largura_fixa_e_o_trilho_nao_encaixa()
    {
        var css = Css();
        var inicio = css.IndexOf("@media (min-width: 768px) { /* prévia do mata-mata no computador */",
                                 StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o @media da prévia no computador.");
        var bloco = css[inicio..css.IndexOf("\n}", inicio, StringComparison.Ordinal)];

        // O ESTICADO MORA AQUI: sem largura fixa a rodada vira fração do monitor.
        Assert.Contains("flex: 0 0 17rem", bloco);

        // Encaixe e espaço de arrasto são do celular: no computador a chave inteira está à
        // vista, e o snap só atrapalharia a rolagem de quem tem chave de 16.
        Assert.Contains("scroll-snap-type: none", bloco);

        // Centralizado sem quebrar a rolagem da chave grande: `fit-content` + margem automática
        // encolhe até o conteúdo e vira 100% quando ele não cabe. `justify-content: center` num
        // container que rola esconde o começo, que é o defeito clássico.
        Assert.Contains("width: fit-content", bloco);
        Assert.Contains("margin-inline: auto", bloco);
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
    public void As_fichas_e_a_dica_de_arrastar_ficam_so_no_celular()
    {
        var fonte = Partial();

        // No computador não há o que arrastar — a chave inteira está na tela. Atalho de rodada
        // e "arraste de lado" ali viram instrução pra um gesto que não existe.
        Assert.Contains("pdz-chd-fichas d-md-none", fonte);
        Assert.Contains("pdz-chd-dica d-md-none", fonte);
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
