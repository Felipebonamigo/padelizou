using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O DIA DA SEMANA JUNTO DA DATA DO JOGO.
//
// 🗣️ Emerson Pisoni, do 2ª Etapa ER PADEL TOUR: *"ali na data daria pra colocar o dia da
// semana, não quero procurar pra saber se é sexta ou sábado, sou vagabundo"*.
//
// "11/09" sozinho não responde a única pergunta que quem lê a lista tem: dá pra ir? Num
// torneio que atravessa o fim de semana, saber se aquilo é sexta à noite ou sábado de manhã
// custava abrir o calendário do celular.
//
// ⚠️ POR QUE TESTE DE FONTE nas views: a suíte não renderiza Razor. O comportamento de
// verdade (que dia é qual) está travado no teste do serviço aqui embaixo; o que só existe na
// VIEW — a data do jogo dizendo o dia — nenhum teste de comportamento alcança.
public class DiaDaSemanaNaDataDoJogoTests
{
    // As seis telas em que a data de um JOGO chega ao jogador. Todas passam a dizer o dia da
    // semana: uma que ficasse de fora seria a tela em que ele volta a ter que adivinhar.
    public static TheoryData<string, string> ViewsComDataDeJogo() => new()
    {
        { Path.Combine("Views", "Torneios", "_JogoEmLinha.cshtml"), "lista de Agendadas/Finalizadas" },
        { Path.Combine("Views", "Torneios", "_JogoQueVem.cshtml"), "prévia da fase que vem" },
        { Path.Combine("Views", "Torneios", "_ChaveDoMataMata.cshtml"), "vaga da chave" },
        { Path.Combine("Views", "Torneios", "Details.cshtml"), "jogos do grupo e chave projetada" },
    };

    [Theory]
    // Os dias do 2ª Etapa ER PADEL TOUR, o torneio do print: sexta à noite e o fim de semana.
    [InlineData("2026-09-10", "qui")]
    [InlineData("2026-09-11", "sex")]
    [InlineData("2026-09-12", "sáb")]
    [InlineData("2026-09-13", "dom")]
    public void Cada_dia_tem_o_nome_certo(string data, string esperado)
    {
        Assert.Equal(esperado, Padelizou.Services.DiaDaSemana.Curto(DateTime.Parse(data,
            System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Tres_letras_sem_ponto_em_todos_os_dias()
    {
        // O abreviado do ICU vem "sex." e já mudou de versão pra versão — a lista é fixa
        // justamente pra a tela não depender da imagem do Linux do VPS.
        for (int i = 0; i < 7; i++)
        {
            var dia = Padelizou.Services.DiaDaSemana.Curto(new DateTime(2026, 9, 6).AddDays(i));
            Assert.Equal(3, dia.Length);
            Assert.DoesNotContain(".", dia, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(ViewsComDataDeJogo))]
    public void A_data_do_jogo_vem_com_o_dia_da_semana(string caminhoRelativo, string ondeE)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

        Assert.True(fonte.Contains("DiaDaSemana.Curto", StringComparison.Ordinal),
            $"A data do jogo ({ondeE}) precisa dizer o dia da semana — ver Services/DiaDaSemana.");
    }

    [Fact]
    public void Nenhuma_data_de_jogo_ficou_sem_o_dia_da_semana()
    {
        // A régua é por OCORRÊNCIA, e não por arquivo: `Details.cshtml` tem duas datas de jogo
        // (o mini-jogo do grupo e a chave projetada) e o teste de cima passaria com uma só.
        foreach (var (caminho, quantas) in new[]
        {
            (Path.Combine("Views", "Torneios", "_JogoEmLinha.cshtml"), 1),
            (Path.Combine("Views", "Torneios", "_JogoQueVem.cshtml"), 1),
            (Path.Combine("Views", "Torneios", "_ChaveDoMataMata.cshtml"), 1),
            (Path.Combine("Views", "Torneios", "Details.cshtml"), 2),
        })
        {
            var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), caminho));

            var comDia = Contagem(fonte, "DiaDaSemana.Curto");
            Assert.True(comDia >= quantas,
                $"{caminho}: esperava {quantas} data(s) de jogo com o dia da semana, achei {comDia}.");
        }
    }

    private static int Contagem(string texto, string agulha)
    {
        int achados = 0, i = 0;
        while ((i = texto.IndexOf(agulha, i, StringComparison.Ordinal)) >= 0)
        {
            achados++;
            i += agulha.Length;
        }

        return achados;
    }

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
