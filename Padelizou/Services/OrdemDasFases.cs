namespace Padelizou.Services;

// EM QUE ORDEM AS FASES DO TORNEIO ACONTECEM — do torneio INTEIRO, não de uma categoria.
//
// 🗣️ Felipe, 09/09/2026, olhando a grade do Er em `dev`: *"como que aqui tem jogo de chave e
// nas outras categorias tem final? o torneio tem q seguir uma ordem, primeiro todas as chaves,
// depois todas as primeiras eliminatorias (decimas > oitavas > quartas > semi > final) a ideia
// e fazer as finais de cada categorias ser os ultimos jogos do torneio"*.
//
// 🕳️ O QUE ELE VIU. Até aqui a ordem era POR CATEGORIA: cada mata-mata era ancorado no fim dos
// grupos da PRÓPRIA categoria (TorneiosController.Chaves.AberturaDoMataMata,
// RoboDoChaveamento.AgendarNaGradeAsync, ProximasFasesDaChave.Agendar — as três diziam a mesma
// coisa). Quem tem 8 duplas fecha os grupos às 21h e joga a final às 22h10; quem tem 24 ainda
// está na fase de grupos no dia seguinte. As duas leituras são defensáveis olhando UMA
// categoria, e nenhuma delas é o que um torneio parece de fora — a final é o clímax, e clímax
// não acontece enquanto tem gente na primeira rodada.
//
// ── O POSTO ──────────────────────────────────────────────────────────────────────────────
// O posto é a DISTÂNCIA ATÉ A FINAL, lida pelo nome da fase. É por isso que ele resolve o
// problema de categorias de tamanhos diferentes sem saber o tamanho de nenhuma: a categoria de
// 8 duplas estreia no mata-mata pelas Quartas e a de 32 pela Primeira Rodada, e as duas se
// encontram no posto das Quartas — que é exatamente a lista que o Felipe escreveu.
//
// ⚠️ A FASE DE GRUPOS É UM POSTO SÓ, com TODAS as rodadas dela dentro (alerta do próprio
// Felipe: *"cuidado por que os grupos podem ter rodada 2 tambem"*). Um grupo de 3 são três
// rodadas, e nenhuma eliminatória entra no meio delas — quem ordena por dentro do posto 0 é o
// Services/OrdemDasRodadas, que intercala os grupos rodada a rodada pra dar descanso. Fatiar o
// posto por rodada de grupo quebraria esse intercalamento sem ganhar nada: a rodada 2 de um
// grupo não depende da rodada 1 de OUTRO grupo.
public static class OrdemDasFases
{
    public const int PostoDaFaseDeGrupos = 0;
    public const int PostoDaFinal = 5;

    // ⚠️ Fase desconhecida cai no posto da FINAL, e não no 0. As duas escolhas erram, e esta
    // erra pro lado barato: uma fase que este mapa não conhece indo pro fim do torneio atrasa
    // um jogo; indo pro começo, ela subiria na frente da fase de grupos e o torneio marcaria
    // uma eliminatória antes de existir quem a dispute. O Americano não passa por aqui (ele
    // tem grade própria, ver TorneiosController.Americano).
    public static int Posto(string? fase)
    {
        if (FasesTorneio.EhFaseDeGrupos(fase)) return PostoDaFaseDeGrupos;

        return fase switch
        {
            ChaveamentoMataMata.PrimeiraRodada => 1,
            "Oitavas de Final" => 2,
            "Quartas de Final" => 3,
            "Semifinal" => 4,
            _ => PostoDaFinal,
        };
    }
}
