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

    // ── O FIM DE UM POSTO ────────────────────────────────────────────────────────────────────
    // Não é o último jogo dele — é o último jogo do BLOCO CHEIO dele.
    //
    // 🗣️ Felipe, 09/09/2026, no print do Er em produção: *"os jogos estao terminando no sabado
    // 19:40 por que? nao deveria, é pra ir ate as 23h"*. A fase de grupos fechava 19h40 de sábado
    // e UM jogo de grupo caía em 08h de domingo (impedimento ou concentração da dupla). Com a
    // barreira no `Max`, TODAS as eliminatórias esperavam esse jogo, e a noite de sábado ficava
    // vazia — o oposto do que ele mesmo abriu ao pedir a ordem: *"a menos que fique horario
    // vazio"*. Um retardatário não pode segurar o torneio inteiro atrás dele.
    //
    // A régua: os horários em ordem; o primeiro HORÁRIO DA GRADE INTEIRO sem jogo do posto
    // encerra o bloco, e o que vem depois é retardatário. `horarioSeguinte` é o passo da grade
    // (GradeDeJogos.DepoisDe) — é ele que sabe que depois das 22h10 de sexta vem 08h de sábado,
    // e que isso NÃO é buraco.
    //
    // ⚠️ SÓ UM HORÁRIO INTEIRO CONTA COMO BURACO, e não "o próximo horário passou": jogo mexido
    // na mão pra um minuto quebrado (20h13) partiria o bloco a cada troca do organizador. Por
    // isso o teste é `seguinte(seguinte(anterior)) <= h` — cabe um horário cheio entre os dois.
    //
    // ⚠️ E RETARDATÁRIO É QUEM SOBRA EM MENOS DE UMA RODADA depois do buraco (`capacidade` =
    // quadras do torneio). O outro lado dessa moeda é o buraco NO MEIO da fase — um horário que
    // o encaixe deixou vazio porque nenhum jogo restante cabia nele (descanso, impedimento) e
    // depois do qual a fase continua com dezenas de jogos. Cortar o bloco ali mandaria as
    // eliminatórias pro MEIO dos grupos, que é exatamente a queixa de 09/09 de manhã. Uma rodada
    // inteira de jogos depois do buraco é a fase seguindo, não gente atrasada; o corte anda do
    // fim pro começo e para no primeiro buraco que não é de retardatário.
    //
    // Os três lugares que calculam barreira de posto leem daqui (LevasDaGrade,
    // ProximasFasesDaChave, AuditoriaDaGrade): grade, prévia e Conferir grade dizendo fins
    // diferentes seria pior que qualquer uma das três réguas sozinha.
    public static DateTime? FimDoBloco(IEnumerable<DateTime> horarios, Func<DateTime, DateTime> horarioSeguinte,
        int capacidade)
    {
        // Com repetição, de propósito: o que se conta depois do buraco são JOGOS, não horários.
        var lista = horarios.OrderBy(h => h).ToList();
        if (lista.Count == 0) return null;

        int umaRodada = Math.Max(1, capacidade);
        int fim = lista.Count;                      // exclusivo — lista[0..fim) é o bloco

        for (int i = lista.Count - 1; i > 0; i--)
        {
            bool buraco = horarioSeguinte(horarioSeguinte(lista[i - 1])) <= lista[i];
            if (!buraco) continue;

            if (fim - i < umaRodada) fim = i;       // retardatários: caem fora, e segue olhando
            else break;                             // uma rodada cheia depois do buraco: é a fase
        }

        return lista[fim - 1];
    }
}
