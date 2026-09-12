using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — OS FILTROS DA ABA JOGOS FORAM PRA DENTRO DE UM BOTÃO "FILTROS".
//
// 🗣️ Felipe, num print da aba Jogos no celular: *"aqui esta muito poluito, muitos botoes.
// Acho que poe apenas um Meus jogos e os outros todos coloca minimizado dentro de um botão
// 'filtros'"*. Eram seis controles empilhados em quatro linhas antes do primeiro jogo
// aparecer — a tela do dia de jogo começava com a régua em vez de com a bola.
//
// A régua nova: "Meus jogos" fica fora (é liga/desliga de um toque), o resto — time, clube,
// quadra, fase e categorias — mora recolhido.
//
// ⚠️ O PAINEL FECHADO NÃO PODE ESCONDER QUE A LISTA ESTÁ FILTRADA. Por isso o botão leva o
// contador de filtros ativos e o "Limpar" fica FORA do recolhido: sem os dois, quem escolheu
// uma quadra recarrega a página, vê meia lista e não tem como descobrir por quê — é a mesma
// "tela mentindo sobre o filtro escolhido" que tirou o botão "Filtrar" daqui.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor, e isto é markup puro.
public class FiltrosRecolhidosNaListaDeJogosTests
{
    [Theory]
    [InlineData("name=\"timeFiltroId\"")]
    [InlineData("name=\"clubeFiltroId\"")]
    [InlineData("name=\"quadraFiltro\"")]
    [InlineData("name=\"faseFiltro\"")]
    [InlineData("name=\"categoriaFiltroIds\"")]
    public void Cada_filtro_mora_dentro_do_painel_recolhido(string campo)
    {
        var fonte = ListaDeJogos();
        var (inicio, fim) = PainelRecolhido(fonte);

        var onde = fonte.IndexOf(campo, StringComparison.Ordinal);
        Assert.True(onde >= 0, $"Não achei o filtro {campo} na lista de jogos.");
        Assert.True(onde > inicio && onde < fim,
            $"O filtro {campo} precisa estar DENTRO do painel #filtrosDosJogos (achei em {onde}, painel de {inicio} a {fim}).");
    }

    [Fact]
    public void Meus_jogos_fica_de_fora_do_recolhido()
    {
        var fonte = ListaDeJogos();
        var (inicio, fim) = PainelRecolhido(fonte);

        var meusJogos = fonte.IndexOf("Meus jogos", StringComparison.Ordinal);
        Assert.True(meusJogos >= 0, "Não achei o link 'Meus jogos'.");
        Assert.False(meusJogos > inicio && meusJogos < fim,
            "'Meus jogos' é o único que fica na linha — não pode ir pra dentro do painel.");
    }

    [Fact]
    public void O_painel_vive_dentro_do_formulario_do_filtro()
    {
        // Select fora do <form> não viaja no GET: o filtro escolhido sumiria no submit.
        var fonte = ListaDeJogos();
        var (inicio, fim) = PainelRecolhido(fonte);

        var form = fonte.IndexOf("id=\"filtroJogos\"", StringComparison.Ordinal);
        var fechaForm = fonte.IndexOf("</form>", form, StringComparison.Ordinal);
        Assert.True(form >= 0 && fechaForm > 0, "Não achei o formulário #filtroJogos.");
        Assert.True(inicio > form && fim < fechaForm,
            "O painel recolhido precisa estar dentro do <form id=\"filtroJogos\">.");
    }

    [Fact]
    public void O_botao_Filtros_so_aparece_quando_ha_o_que_recolher()
    {
        // Botão que abre painel vazio é pior que botão nenhum — a mesma régua do "Meus jogos".
        var fonte = ListaDeJogos();

        var alvo = fonte.IndexOf("data-bs-target=\"#filtrosDosJogos\"", StringComparison.Ordinal);
        Assert.True(alvo >= 0, "Não achei o botão que abre o painel #filtrosDosJogos.");

        var gate = fonte.LastIndexOf("@if (temFiltroRecolhido)", alvo, StringComparison.Ordinal);
        Assert.True(gate >= 0, "O botão 'Filtros' precisa estar atrás de um `@if (temFiltroRecolhido)`.");
        Assert.DoesNotContain("}", fonte[gate..alvo]);
    }

    [Fact]
    public void O_botao_conta_os_filtros_ativos_e_o_Limpar_fica_de_fora()
    {
        var fonte = ListaDeJogos();
        var (inicio, fim) = PainelRecolhido(fonte);

        Assert.Contains("filtrosAtivos", fonte, StringComparison.Ordinal);

        // O contador é impresso no botão que abre o painel, não escondido junto com ele.
        var alvo = fonte.IndexOf("data-bs-target=\"#filtrosDosJogos\"", StringComparison.Ordinal);
        var fechaBotao = fonte.IndexOf("</button>", alvo, StringComparison.Ordinal);
        Assert.Contains("filtrosAtivos", fonte[alvo..fechaBotao], StringComparison.Ordinal);

        var limpar = fonte.IndexOf("Limpar filtros", StringComparison.Ordinal);
        Assert.True(limpar >= 0, "Não achei o 'Limpar filtros'.");
        Assert.False(limpar > inicio && limpar < fim,
            "O 'Limpar filtros' precisa ficar FORA do painel: é a saída de quem não sabe por que a lista encolheu.");
    }

    // O painel pelo casamento das tags, e não por distância em caracteres: o `@if` de cada
    // filtro abre e fecha <div> lá dentro, e um número mágico passaria com o painel fechado
    // no meio.
    private static (int Inicio, int Fim) PainelRecolhido(string fonte)
    {
        var id = fonte.IndexOf("id=\"filtrosDosJogos\"", StringComparison.Ordinal);
        Assert.True(id >= 0, "Não achei o painel recolhido #filtrosDosJogos.");

        var abre = fonte.LastIndexOf("<div", id, StringComparison.Ordinal);
        var i = fonte.IndexOf('>', id) + 1;
        var nivel = 1;
        while (nivel > 0)
        {
            var proximaAbre = fonte.IndexOf("<div", i, StringComparison.Ordinal);
            var proximaFecha = fonte.IndexOf("</div>", i, StringComparison.Ordinal);
            Assert.True(proximaFecha >= 0, "O painel #filtrosDosJogos não fecha.");

            if (proximaAbre >= 0 && proximaAbre < proximaFecha)
            {
                nivel++;
                i = proximaAbre + 4;
            }
            else
            {
                nivel--;
                i = proximaFecha + 6;
            }
        }

        return (abre, i);
    }

    private static string ListaDeJogos() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
