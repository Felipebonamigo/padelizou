using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — O CHECK-IN CABE NA ABA JOGOS, UMA BOLINHA POR JOGADOR.
//
// 🗣️ Felipe, num print da aba Jogos do 2ª Etapa ER aberta no celular: *"Temos q por uma forma de
// fazer o checkin nessa tela por jogo"* — e, no mesmo dia, sobre a bolinha por dupla que subiu
// primeiro: *"Mude para um check por jogador, por que é assim que controla check in"*.
//
// 🕳️ O BURACO É DE CAMINHO, não de dado: a presença já existia (a tela de Check-in virou fila de
// jogos em 10/09), mas quem está na aba Jogos — chamando quadra, dando a largada, marcando placar
// — precisava SAIR dela, marcar na outra tela e voltar. No sábado de manhã "quem joga agora?" e
// "essa dupla chegou?" são a mesma linha, no mesmo minuto.
//
// ⚠️ ESTE ARQUIVO GUARDA A TELA. Quem a presença é, onde é gravada e o que "dupla completa"
// significa está em `PresencaPorJogadorTests` — aqui se cobra o que a LINHA DO JOGO mostra: quem
// vê a bolinha, quando ela existe, e que ela é visível nos dois temas.
public class CheckInNaListaDeJogosTests
{
    private static (Torneio torneio, Jogador organizador) Montar(DbPadelContext ctx, bool usaCheckIn)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        torneio.UsaCheckIn = usaCheckIn;
        ctx.SaveChanges();
        return (torneio, organizador);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_lista_de_jogos_sabe_se_o_torneio_usa_check_in(bool ligado)
    {
        // Sem isto a bolinha apareceria nos torneios que desligaram a chamada — e o servidor
        // recusaria o clique depois, que é fazer o organizador descobrir a regra pelo erro.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = Montar(ctx, ligado);

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).Jogos(torneio.Id, null, null));

        Assert.Equal(ligado, view.ViewData["UsaCheckIn"]);
    }

    [Fact]
    public void A_bolinha_so_nasce_pra_quem_opera_o_dia_com_a_chamada_ligada_e_jogo_por_vir()
    {
        // As três condições juntas, numa guarda só: quem NÃO opera o dia não vê (🗣️ *"apenas o
        // organizador ve"*), torneio sem chamada não ganha bolinha nenhuma, e jogo que já começou
        // ou acabou já respondeu a pergunta por outra via.
        var fonte = Ler("_JogoEmLinha.cshtml");
        int guarda = fonte.IndexOf("bool bolinhaDoCheckIn", StringComparison.Ordinal);
        Assert.True(guarda >= 0, "Não achei a guarda da bolinha do check-in.");

        var expressao = fonte[guarda..fonte.IndexOf(';', guarda)];
        Assert.Contains("Model.EhOrganizador", expressao);
        Assert.Contains("UsaCheckIn", expressao);
        Assert.Contains("ehAgendado", expressao);
    }

    [Fact]
    public void A_bolinha_de_cada_jogador_vem_ANTES_do_nome_dele()
    {
        // 🗣️ *"uma bolinha do lado de cada nome"*: ela nasce antes do rosto, na ponta esquerda do
        // chip do jogador — é assim que as quatro viram uma coluna que se varre de cima a baixo.
        // Empurrada pro fim, brigaria com a porcentagem do palpitômetro, que já mora na direita.
        var fonte = Ler("_JogoEmLinha.cshtml");

        foreach (var lado in new[] { "jogo.Dupla1", "jogo.Dupla2" })
        {
            int par = fonte.IndexOf($"PresencaNoDia.JogadoresDa({lado})", StringComparison.Ordinal);
            Assert.True(par >= 0, $"O lado {lado} não abre o par por jogador.");

            int botao = fonte.IndexOf("<partial name=\"_BotaoDoCheckIn\"", par, StringComparison.Ordinal);
            int rosto = fonte.IndexOf("pdz-jl-rosto", par, StringComparison.Ordinal);
            int nome = fonte.IndexOf("pdz-jl-nome", par, StringComparison.Ordinal);

            Assert.True(botao >= 0 && botao < rosto && rosto < nome,
                $"No lado {lado} a ordem tem que ser bolinha → rosto → nome.");
        }
    }

    [Fact]
    public void Sem_chamada_ligada_a_linha_continua_sendo_a_de_sempre()
    {
        // O caminho antigo (`LadoDaDupla`) segue vivo e é o que 99% de quem abre a página recebe:
        // dois rostos sobrepostos e os dois nomes numa linha. Quem não organiza não paga nada
        // pela chamada — nem um `if` a mais no HTML entregue.
        var fonte = Ler("_JogoEmLinha.cshtml");
        Assert.Contains("@LadoDaDupla(jogo.Dupla1)", fonte);
        Assert.Contains("@LadoDaDupla(jogo.Dupla2)", fonte);
    }

    // ── A BOLINHA PRECISA SER VISTA ──────────────────────────────────────────────────────

    [Fact]
    public void A_bolinha_de_quem_ainda_nao_chegou_e_visivel_no_tema_ESCURO()
    {
        // 🕳️ DEFEITO DE 12/09/2026, achado pelo Felipe no ar: *"Mas eu nao achei aonde q marca o
        // checkin"*. Ela estava lá — desenhada com `var(--pdz-border)`, que no tema escuro é
        // `rgba(231, 236, 247, .10)`: DEZ POR CENTO de alpha. Borda de 10% separa caixa de fundo;
        // ícone de 10% em cima de card escuro não existe pra quem olha.
        //
        // ⚠️ A ARMADILHA É O NOME DO TOKEN, e é por isso que ela vale um teste: `--pdz-border`
        // parece "o cinza discreto do tema" e é o cinza de UMA LINHA de 1px. Cor de coisa que
        // precisa ser vista sai de `--pdz-muted` (#6a7891 no claro, #9aa7c4 no escuro), que é o
        // token contrastado dos dois lados — o mesmo raciocínio do `--pdz-linha-chave`, que
        // nasceu porque a borda de 9% sumia ao atravessar o quadro da chave.
        var regra = Regex.Match(Css(), @"\.pdz-jl-checkin\s*\{[^}]*\}", RegexOptions.Singleline);

        Assert.True(regra.Success, "A regra .pdz-jl-checkin sumiu do site.css.");
        Assert.DoesNotMatch(new Regex(@"color:\s*var\(--pdz-border\)"), regra.Value);
        Assert.Matches(new Regex(@"color:\s*var\(--pdz-muted\)"), regra.Value);
    }

    [Fact]
    public void A_bolinha_de_quem_chegou_se_distingue_de_quem_nao_chegou()
    {
        // Verde cheia contra cinza vazada: se as duas fossem da mesma cor, a chamada viraria um
        // teste de memória.
        Assert.Matches(
            new Regex(@"\.pdz-jl-checkin-chegou\s*\{[^}]*color:\s*var\(--padel-green\)", RegexOptions.Singleline),
            Css());
    }

    [Fact]
    public void Marcar_da_lista_nao_joga_a_tela_de_volta_pro_topo()
    {
        // Numa lista de 97 jogos, cada POST redesenha a página do começo — a mesma queixa que
        // criou o js/manter-posicao-na-lista.js. O opt-in vive no formulário, que é um só.
        Assert.Contains("data-manter-posicao", Ler("_BotaoDoCheckIn.cshtml"));
    }

    private static string Ler(string view) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", view));

    private static string Css() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "css", "site.css"));

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
