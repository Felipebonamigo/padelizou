using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using SkiaSharp;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ Felipe, 14/09/2026: *"crie um botão para compartilhar o ranking"* — e, perguntado, escolheu
// ARTE PNG (não link) com **um botão por aba**.
//
// ⚠️ O RANKING NÃO É CALCULADO AQUI, e essa é a régua pra ler o arquivo inteiro. A ordem, os
// pontos e o recorte regional saem do `ObterRankingHubAsync`, o mesmo que desenha a tela — o
// `RankingParaCard` só ESCOLHE QUAL LISTA e a TRADUZ em linhas. Uma segunda ordenação aqui
// publicaria uma arte dizendo que o time A é o primeiro enquanto a página diz que é o B, e é o
// tipo de divergência que só aparece depois de postada.
//
// 🔑 E ELE PÁRA NO TOP 10 (escolha do Felipe): é o que cabe legível na faixa de tabela do
// 1080×1350. Acima disso a fonte encolhe até o chão do `TamanhoQueCabe` — que devolve o mínimo
// mesmo quando ele não cabe, e aí o Skia pinta até a borda e some com o resto do nome (o buraco
// do pódio, 14/09/2026).
public class CartaoDoRankingTests
{
    private static Jogador Quem(int id, string nome, string? apelido = null) =>
        new() { Id = id, Nome = nome, Apelido = apelido, Cpf = id.ToString("D11") };

    // O ranking de times do print do Felipe, com os números que estavam na tela.
    private static RankingHubVM HubDosTimes()
    {
        var hub = new RankingHubVM();
        var nomes = new[]
        {
            ("Los Corneteiros", 1301), ("ER Padel", 1169), ("Chakra Padel", 562),
            ("Os Loberos", 535), ("Porto Padel", 364), ("Team Maia", 201),
            ("Trevo", 182), ("Sindaqua", 118), ("Los Vespeiros", 98),
            ("POA Padel", 84), ("Vibora Banguela", 77), ("Joel Padel Trainer", 72),
        };

        for (var i = 0; i < nomes.Length; i++)
        {
            hub.Times.Add(new RankingTimeVM
            {
                TimeId = i + 1,
                Time = nomes[i].Item1,
                Pontos = nomes[i].Item2,
            });
        }

        return hub;
    }

    [Fact]
    public void A_aba_de_times_vira_uma_lista_com_o_lider_em_primeiro()
    {
        var lista = RankingParaCard.Montar(HubDosTimes(), AbaDoRanking.Times);

        Assert.NotNull(lista);
        Assert.Equal("Los Corneteiros", lista!.Linhas[0].Nome);
        Assert.Equal(1, lista.Linhas[0].Posicao);
        Assert.Equal("1301 pts", lista.Linhas[0].Valor);
    }

    [Fact]
    public void A_lista_para_no_top_10_mesmo_com_doze_times()
    {
        var lista = RankingParaCard.Montar(HubDosTimes(), AbaDoRanking.Times);

        // Doze entraram; dez saem. O 11º e o 12º da tela não cabem na arte — e é melhor que a
        // arte diga "top 10" do que espremer doze linhas até ninguém conseguir ler nenhuma.
        Assert.Equal(RankingParaCard.MaximoDeLinhas, lista!.Linhas.Count);
        Assert.Equal("POA Padel", lista.Linhas[^1].Nome);
    }

    [Fact]
    public void Aba_sem_ninguem_nao_vira_arte()
    {
        // Card de ranking vazio anuncia um ranking que não existe. Quem chama devolve 404 —
        // a mesma régua do card de classificação sem jogo terminado.
        Assert.Null(RankingParaCard.Montar(new RankingHubVM(), AbaDoRanking.Times));
    }

    [Fact]
    public void O_recorte_regional_vai_escrito_na_arte()
    {
        // Sem isto, a arte do ranking de Porto Alegre é indistinguível da do Brasil todo — e
        // quem posta "sou o 1º" estaria dizendo uma coisa diferente da que a tela dizia.
        var hub = HubDosTimes();
        hub.Estado = "RS";
        hub.Cidades.Add("Porto Alegre");

        var lista = RankingParaCard.Montar(hub, AbaDoRanking.Times);

        Assert.Contains("Porto Alegre", lista!.Recorte);
    }

    [Fact]
    public void A_arte_nao_encosta_nome_nenhum_na_borda()
    {
        // O pior caso REAL: dupla com nome comprido e apelido nos dois lados, na 10ª posição
        // (o "10º" é mais largo que o "1º"). É o nome que derrubou o pódio em 14/09.
        var hub = new RankingHubVM();
        for (var i = 1; i <= 10; i++)
        {
            hub.VitoriasDuplas.Add(new DuplaContagemVM
            {
                Jogador1 = Quem(i * 2, "Anderson Matteus Schwaab", "Andersinho"),
                Jogador2 = Quem(i * 2 + 1, "Charls Gustavio Polese", "Charlinho"),
                Categoria = "4ª Masculina",
                Torneio = "2ª Etapa ER Padel Tour (EPT)",
                Vitorias = 40 - i,
                Jogos = 50,
            });
        }

        var png = CartaoDoRanking.Desenhar(
            RankingParaCard.Montar(hub, AbaDoRanking.VitoriasDuplas)!, FontesDeVerdade(), RaizWeb());

        Assert.Empty(TintaNaMargem(png));
    }

    [Fact]
    public void A_pilula_do_recorte_tambem_cabe_na_arte()
    {
        // ⚠️ A `Pilula` DIMENSIONA O FUNDO PELO TEXTO (largura = texto + folga de 44 por lado) e
        // NÃO encolhe a fonte — ao contrário do `TextoCentralizado`, que é o reflexo de quem lê
        // este arquivo. Com um recorte comprido ela nasceria mais larga que o canvas, o Skia
        // cortaria nas duas bordas, e o que sobraria é uma faixa lime sangrando de ponta a
        // ponta com a categoria pela metade dentro.
        //
        // ⚠️ ESTE TESTE PASSOU DE PRIMEIRA, E ISSO SOZINHO NÃO PROVA NADA (Regra 1). O que o
        // sustenta é a MEDIÇÃO, feita no dia: no corpo 40, o pior recorte real mede **605px**
        // ("Governador Celso Ramos") contra um teto de **960** — 355px de sobra. Nenhuma cidade
        // ou categoria que existe chega perto; a conta só vira ESTOURA lá pelos 50 caracteres.
        // Por isso o `CartaoDoRanking` NÃO encolhe a pílula: seria código pra um caso que não
        // existe, e a escada do CLAUDE.md manda parar no degrau que resolve.
        //
        // 🔬 E O GATE FOI FALSIFICADO, não só executado: com um recorte de 51 caracteres
        // (pílula de 1179px) ele acusou **84 linhas** com tinta na margem. Ele morde — a arte
        // real é que está longe da borda. Se um dia o recorte crescer (uma cidade de nome
        // enorme, um rótulo novo), é aqui que isso aparece VERMELHO em vez de sair num story.
        //
        // 🔎 E ele pega a pílula sem saber que ela existe: a lime (163,216,39) tem luminância
        // ~180, acima do limiar de 140 — pra ele é tinta como qualquer outra.
        var hub = new RankingHubVM { Estado = "RS" };
        hub.Cidades.Add("São José do Rio Preto");

        for (var i = 1; i <= 10; i++)
        {
            hub.TrofeusAmericanoDuplas.Add(new RankingAmericanoLinhaVM
            {
                Jogador = Quem(i, "Anderson Matteus Schwaab", "Andersinho"),
                Vitorias = 20 - i,
                Pontos = 500 - i,
            });
        }

        // O título mais longo que existe aqui, com o recorte mais longo que existe.
        var lista = RankingParaCard.Montar(hub, AbaDoRanking.TrofeusAmericanoDuplas)!;
        Assert.Equal("TROFÉUS DO AMERICANO EM DUPLAS", lista.Titulo);
        Assert.Equal("São José do Rio Preto", lista.Recorte);

        Assert.Empty(TintaNaMargem(CartaoDoRanking.Desenhar(lista, FontesDeVerdade(), RaizWeb())));
    }

    // ── O mesmo gate de margem do `ArteNaoCortaNomeDentroDoPngTests` ────────────────────────
    // Copiado de propósito e não extraído: aquele arquivo é o registro de um defeito de 14/09 e
    // os números dele (60px, luminância 140, a faixa lime de 14px) são a leitura daquela arte.
    // Compartilhar o helper faria uma mudança de margem lá calar a trava daqui.
    private const int FaixaProibida = 60;
    private const int LimiarDeTinta = 140;
    private const int AlturaDaFaixa = 14;

    private static List<(int Y, int Colunas)> TintaNaMargem(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        var achados = new List<(int, int)>();

        for (var y = AlturaDaFaixa; y < bitmap.Height; y++)
        {
            var colunas = 0;
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (x >= FaixaProibida && x < bitmap.Width - FaixaProibida) continue;
                var p = bitmap.GetPixel(x, y);
                var luminancia = (0.299 * p.Red) + (0.587 * p.Green) + (0.114 * p.Blue);
                if (luminancia > LimiarDeTinta) colunas++;
            }
            if (colunas > 0) achados.Add((y, colunas));
        }

        return achados;
    }

    private static FonteDoCartao FontesDeVerdade() => new(PastaDasFontes());

    private static string RaizWeb() => Path.GetDirectoryName(PastaDasFontes())!;

    private static string PastaDasFontes()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "wwwroot", "fonts");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }

        throw new DirectoryNotFoundException("Não achei Padelizou/wwwroot/fonts a partir de " + AppContext.BaseDirectory);
    }
}
