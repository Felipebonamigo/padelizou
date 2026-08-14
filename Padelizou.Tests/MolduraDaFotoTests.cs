using Padelizou.Services;
using Padelizou.ViewModels;
using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 13/08/2026 — MOLDURAS DA FOTO: cada conquista destrava uma, o jogador escolhe no perfil.
//
// Os dois conjuntos (conquistas e molduras) são gêmeos POR CONTRATO, e o teste é o cartório:
// conquista sem moldura é promessa quebrada ("cada conquista destrava uma moldura" — e a sua
// não); moldura sem conquista é enfeite órfão que ninguém consegue destravar. Os dois erros
// nascem do mesmo jeito — alguém adiciona numa lista e esquece da outra.
public class MolduraDaFotoTests
{
    // Todas as conquistas do catálogo, todas destravadas — o universo completo de códigos.
    private static List<ConquistaVM> TodasConquistadas() =>
        CatalogoConquistas.Montar(new DadosParaConquistas(
            JogouAlgumaVez: true, JogosSemanais: 99, EhOrganizador: true, TemTime: true,
            EhProfessor: true, Titulos: 10, Finais: 10, TotalTorneios: 99, Vitorias: 200,
            ElogiosRecebidos: 99, AulasComoAluno: 99, VezesMvp: 1, ClubesDiferentes: 9));

    private static List<ConquistaVM> NenhumaConquistada() =>
        CatalogoConquistas.Montar(new DadosParaConquistas(
            false, 0, false, false, false, 0, 0, 0, 0, 0, 0));

    [Fact]
    public void Toda_conquista_tem_moldura_e_toda_moldura_tem_conquista()
    {
        var conquistas = TodasConquistadas().Select(c => c.Codigo).ToHashSet();
        var molduras = CatalogoMolduras.Todas.Select(m => m.Codigo).ToHashSet();

        var conquistaSemMoldura = conquistas.Except(molduras).ToList();
        var molduraSemConquista = molduras.Except(conquistas).ToList();

        Assert.True(conquistaSemMoldura.Count == 0,
            "Conquista sem moldura (a promessa é 'cada conquista destrava uma'): "
            + string.Join(", ", conquistaSemMoldura));
        Assert.True(molduraSemConquista.Count == 0,
            "Moldura órfã — não há conquista que a destrave: " + string.Join(", ", molduraSemConquista));
    }

    // ⚠️ Moldura no catálogo sem CSS é INVISÍVEL — a pessoa escolhe e nada muda, sem erro em
    // lugar nenhum. O teste lê o site.css de verdade, como o PwaArquivosTests lê o sw.js.
    [Fact]
    public void Toda_moldura_do_catalogo_tem_o_desenho_no_site_css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei o site.css.");

        var css = File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "wwwroot", "css", "site.css"));

        var semDesenho = CatalogoMolduras.Todas
            .Where(m => !Regex.IsMatch(css, $@"\.pdz-m-{Regex.Escape(m.Codigo)}\b"))
            .Select(m => m.Codigo)
            .ToList();

        Assert.True(semDesenho.Count == 0,
            "Moldura sem CSS — quem escolher não vê nada acontecer: " + string.Join(", ", semDesenho));
    }

    // ===================== A TRAVA DE USO =====================

    [Fact]
    public void Tirar_a_moldura_pode_sempre()
    {
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar(null, NenhumaConquistada()));
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar("", NenhumaConquistada()));
    }

    [Fact]
    public void Moldura_de_conquista_destravada_pode()
    {
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar("Estreia", TodasConquistadas()));
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar("Decacampeao", TodasConquistadas()));
    }

    // ⚠️ A LISTA DA TELA NÃO É A TRAVA: a tela cinza a moldura bloqueada, mas o POST chega com
    // qualquer string. Sem esta recusa, um formulário montado à mão vestiria a moldura de
    // Decacampeão em quem nunca jogou.
    [Fact]
    public void Moldura_de_conquista_NAO_destravada_e_recusada()
    {
        var motivo = CatalogoMolduras.MotivoParaNaoUsar("Decacampeao", NenhumaConquistada());

        Assert.NotNull(motivo);
        Assert.Contains("ainda não destravou", motivo);
    }

    [Fact]
    public void Codigo_inventado_e_recusado_sem_virar_classe_css()
    {
        Assert.NotNull(CatalogoMolduras.MotivoParaNaoUsar("HackDoRecreio", TodasConquistadas()));

        // E mesmo que um valor inventado chegue ao banco, ele nunca vira classe: o CSS da
        // moldura sai do catálogo, não do texto gravado.
        Assert.Equal("", CatalogoMolduras.ClasseCss("HackDoRecreio"));
        Assert.Equal("", CatalogoMolduras.ClasseCss(null));
        Assert.Equal("pdz-m-Campeao", CatalogoMolduras.ClasseCss("Campeao"));
    }

    // O VM do parcial: tamanho zero significa "quem dimensiona é a classe" (o chip compacto) —
    // nesse caso o parcial não pode emitir o --foto inline, porque inline vence classe.
    [Fact]
    public void O_VM_carrega_moldura_e_classe_do_jogador()
    {
        var jogador = new Padelizou.Models.Jogador
        {
            Nome = "Teste", FotoPerfil = "/img/x.jpg", MolduraEscolhida = "Campeao",
        };

        var vm = FotoDoJogadorVM.De(jogador, 0, "pdz-chip-avatar");

        Assert.Equal("Campeao", vm.Moldura);
        Assert.Equal(0, vm.Tamanho);
        Assert.Equal("pdz-chip-avatar", vm.Classe);

        // Jogador nulo (dupla sem parceiro) não pode derrubar a tela.
        var vazio = FotoDoJogadorVM.De(null, 48);
        Assert.Null(vazio.FotoUrl);
        Assert.Null(vazio.Moldura);
    }
}
