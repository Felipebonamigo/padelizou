using System.Globalization;

namespace Padelizou.Services;

// A GEOMETRIA da prévia do mata-mata: onde cada jogo fica no quadro, e quanto ele ocupa.
//
// A tela desenha a mesma chave de dois jeitos — no computador de cima pra baixo, no celular
// deitada e arrastável da esquerda pra direita. Os dois saem DESTE cálculo: desenho é CSS,
// posição é conta, e conta repetida em duas views diverge no dia em que a regra mudar.
//
// A régua é uma só e cabe em duas linhas:
//
//   LARGURA de um jogo = a soma das larguras dos jogos que o alimentam (1 se nenhum alimenta).
//   COLUNA  de um jogo = a coluna onde começa a faixa dele, andando da final pra trás.
//
// É isso que centraliza cada cartão sobre os dois que o produzem sem cálculo de pixel nenhum:
// a grade do CSS recebe `grid-column: coluna+1 / span largura` e faz o resto, igual numa vaga
// de 2 colunas e numa de 16.
//
// ⚠️ A ORDEM DENTRO DA RODADA NÃO É A NUMÉRICA. Numa chave de 4, a primeira rodada sai
// 1, 4, 2, 3 — porque o jogo 5 nasce do 1 e do 4, e o 6 do 2 e do 3. Desenhar em ordem
// numérica faria as linhas de ligação cruzarem no meio do quadro.
//
// ⚠️ QUEM PASSOU DIRETO NÃO OCUPA COLUNA. O lado de bye não vem de jogo nenhum, então não tem
// largura pra somar: com 3 grupos a chave tem 2 colunas, e não 4. Na tela isso aparece como
// uma vaga sem linha chegando de cima — que é exatamente o que "passou direto" significa.
public static class ArvoreDaChave
{
    // Um jogo já posicionado. `Coluna` é base zero; o Razor soma 1 pro CSS.
    public record Vaga(ChaveProjetada.JogoProjetado Jogo, int Coluna, int Largura);

    public record Rodada(string Fase, List<Vaga> Vagas);

    // `Colunas` é a largura da chave inteira — o número de colunas da grade.
    public record Quadro(int Colunas, List<Rodada> Rodadas)
    {
        // O índice é montado uma vez só: a view chama `Ligacao` por vaga, e remontar o
        // dicionário a cada chamada faria uma chave de 32 percorrer 31 jogos 31 vezes.
        private Dictionary<int, Vaga>? _porNumero;
        private Dictionary<int, Vaga> PorNumero =>
            _porNumero ??= Rodadas.SelectMany(r => r.Vagas).ToDictionary(v => v.Jogo.Numero);

        // De qual vaga vem cada lado desta — o que a linha de ligação desenha.
        public List<Vaga> Alimentadores(Vaga vaga)
        {
            var porNumero = PorNumero;
            return new[] { vaga.Jogo.VemDoJogo1, vaga.Jogo.VemDoJogo2 }
                .Where(n => n.HasValue && porNumero.ContainsKey(n!.Value))
                .Select(n => porNumero[n!.Value])
                .ToList();
        }

        // As medidas da ligação, em % da PRÓPRIA vaga, prontas pro atributo `style`: onde entra
        // o primeiro talo (`--a`) e quanto anda a linha até o segundo (`--hw`). Tudo relativo,
        // então serve igual a uma vaga de 2 colunas e a uma de 16 — e aos DOIS eixos, porque no
        // computador a árvore desce e no celular ela deita.
        //
        // ⚠️ O CENTRO DA VAGA SEMPRE CAI ENTRE OS DOIS TALOS, porque a vaga cobre exatamente a
        // faixa dos jogos que a alimentam. É isso que deixa o desenho ser um retângulo de borda
        // em vez de quatro pedaços posicionados na unha.
        //
        // ⚠️ CULTURA INVARIANTE, SEMPRE. Em pt-BR um `double` sai "25,5" e o navegador descarta
        // a declaração inteira sem avisar — o quadro perderia as linhas em produção e passaria
        // por qualquer teste que só olhasse a marcação. Travado em ArvoreDaChaveTests.
        //
        // Nulo = ninguém alimenta esta vaga (primeira rodada). `Reta` = um alimentador só,
        // porque o outro lado passou direto: aí o talo desce no meio e não há cotovelo.
        public (bool Reta, string Estilo)? Ligacao(Vaga vaga)
        {
            var alimentadores = Alimentadores(vaga);
            if (alimentadores.Count == 0) return null;

            var centros = alimentadores
                .Select(a => ((a.Coluna - vaga.Coluna) + a.Largura / 2.0) / vaga.Largura * 100)
                .ToList();
            double a = centros.Min(), b = centros.Max();
            if (b - a < 0.01) return (true, "");

            return (false, $"--a:{Pct(a)};--hw:{Pct(b - a)}");
        }

        private static string Pct(double valor) =>
            valor.ToString("0.###", CultureInfo.InvariantCulture) + "%";
    }

    private static readonly Quadro Vazio = new(0, new List<Rodada>());

    public static Quadro Montar(IReadOnlyList<ChaveProjetada.RodadaProjetada> rodadas)
    {
        var jogos = rodadas.SelectMany(r => r.Jogos).ToList();
        if (jogos.Count == 0) return Vazio;

        var porNumero = jogos.ToDictionary(j => j.Numero);

        // A raiz é o jogo que ninguém consome — a final. Procurar assim, e não pegar o último
        // da lista, é o que faz a árvore continuar certa se um dia a ordem das rodadas mudar.
        var consumidos = jogos
            .SelectMany(j => new[] { j.VemDoJogo1, j.VemDoJogo2 })
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToHashSet();
        var raiz = jogos.FirstOrDefault(j => !consumidos.Contains(j.Numero));
        if (raiz == null) return Vazio;

        List<ChaveProjetada.JogoProjetado> Alimentadores(ChaveProjetada.JogoProjetado jogo) =>
            new[] { jogo.VemDoJogo1, jogo.VemDoJogo2 }
                .Where(n => n.HasValue && porNumero.ContainsKey(n!.Value))
                .Select(n => porNumero[n!.Value])
                .ToList();

        var largura = new Dictionary<int, int>();
        int Medir(ChaveProjetada.JogoProjetado jogo)
        {
            var alimentadores = Alimentadores(jogo);
            int soma = alimentadores.Count == 0 ? 1 : alimentadores.Sum(Medir);
            largura[jogo.Numero] = soma;
            return soma;
        }
        int colunas = Medir(raiz);

        var vagas = new Dictionary<int, Vaga>();
        void Posicionar(ChaveProjetada.JogoProjetado jogo, int coluna)
        {
            vagas[jogo.Numero] = new Vaga(jogo, coluna, largura[jogo.Numero]);
            int cursor = coluna;
            foreach (var alimentador in Alimentadores(jogo))
            {
                Posicionar(alimentador, cursor);
                cursor += largura[alimentador.Numero];
            }
        }
        Posicionar(raiz, 0);

        // As fases ficam na ordem em que a projeção as entregou (primeira rodada → final), e
        // dentro de cada uma as vagas saem da esquerda pra direita.
        var montadas = rodadas
            .Select(r => new Rodada(
                r.Fase,
                r.Jogos.Where(j => vagas.ContainsKey(j.Numero))
                       .Select(j => vagas[j.Numero])
                       .OrderBy(v => v.Coluna)
                       .ToList()))
            .Where(r => r.Vagas.Count > 0)
            .ToList();

        return new Quadro(colunas, montadas);
    }
}
