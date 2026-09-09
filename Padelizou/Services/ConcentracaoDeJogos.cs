using Padelizou.Models;

namespace Padelizou.Services;

// O turno em que o organizador CONCENTRA os jogos de grupo de uma dupla.
//
// Não é o `TurnoDoImpedimento` com outro nome: lá `SextaNoite` quer dizer "NÃO pode na sexta",
// aqui quer dizer "SÓ na sexta". São escolhas opostas, e um enum só faria o mesmo valor
// significar as duas coisas dependendo de onde estivesse gravado.
//
// Sem `QuintaNoite`: o Felipe pediu três opções ("os 2 jogos na sexta / no sábado de manhã /
// no sábado à tarde"), e a quinta só existe em torneio que começa nela. Se fizer falta, entra
// aqui e em `ConcentracaoDeJogos.JanelaDoTurno` — o resto do cálculo não muda.
public enum TurnoDeConcentracao
{
    Nenhuma,
    SextaNoite,
    SabadoManha,
    SabadoTarde,
}

// "COLOCAR OS 2 JOGOS NA SEXTA" — o favor que o organizador (e o adm do sistema) concede.
//
// 🗣️ Pedido do Felipe, 08/09/2026: "permita também criar uma opção, lá nos impedimentos, de
// 'colocar os 2 jogos na sexta', colocar os 2 jogos no sábado a tarde, os 2 jogos no sábado de
// manha, apenas para os organizadores e adm do sistema, para que nós possamos auxiliar
// algumas pessoas".
//
// ⚠️ É O AVESSO DO IMPEDIMENTO, e é por isso que não coube nos quatro booleanos que já existem:
// o impedimento diz "não posso em X" e Services/ImpedimentoUnico garante no máximo UM ligado;
// a concentração diz "só posso em X", que é o complemento — precisaria de três ligados de uma
// vez, quebrando essa invariante e fazendo o preço contar três taxas por um favor que é de
// graça. Daí a coluna própria (`Dupla.ConcentrarJogosEm`).
//
// ⚠️ VALE SÓ NA FASE DE GRUPOS, decisão do Felipe: "os 2 jogos" são os 2 do grupo. A
// eliminatória sai DEPOIS dos grupos por definição, e prendê-la ao mesmo turno seria pedir o
// impossível. Quem aplica esse recorte é GradeDeJogos.Encaixar (parâmetro
// `janelasSoNosGruposPorDupla`) — este serviço só produz as janelas.
//
// 💰 NÃO CUSTA NADA (decisão do Felipe, 08/09/2026): é favor do organizador, não flexibilidade
// comprada. Quem tinha impedimento pago e vira concentração tem o valor ABAIXADO, igual a
// tirar o impedimento — ver AlteracaoDeImpedimento.QuantoMudaOValorAoConcentrar.
public static class ConcentracaoDeJogos
{
    // Quanto tempo antes e depois do início do torneio a proibição se estende. O bloqueio é o
    // COMPLEMENTO do turno escolhido, e complemento precisa de borda: sem ela a janela iria a
    // DateTime.MinValue/MaxValue e qualquer aritmética em cima estouraria.
    //
    // 30 dias é folga absurda de propósito — nenhum torneio dura isso, então na prática é
    // "sempre", e um "Refazer grade" que empurre jogo pro quarto dia continua coberto.
    private static readonly TimeSpan Horizonte = TimeSpan.FromDays(30);

    public static string Rotulo(TurnoDeConcentracao turno) => turno switch
    {
        TurnoDeConcentracao.SextaNoite => "Os 2 jogos na sexta à noite",
        TurnoDeConcentracao.SabadoManha => "Os 2 jogos no sábado de manhã",
        TurnoDeConcentracao.SabadoTarde => "Os 2 jogos no sábado à tarde",
        _ => "Sem concentração",
    };

    // A janela EM QUE A DUPLA PODE JOGAR — o turno escolhido, no calendário deste torneio.
    // Null quando o turno não existe no torneio (pedir "só sexta" num torneio de sábado a
    // domingo), e é essa nulidade que impede o caso catastrófico logo abaixo.
    //
    // As formas são as MESMAS de JanelasDeImpedimento: sexta é o dia inteiro (nela só existe o
    // turno da noite), e o sábado se parte no corte do meio-dia.
    public static (DateTime Inicio, DateTime Fim)? JanelaDoTurno(Torneio torneio, TurnoDeConcentracao turno)
    {
        if (turno == TurnoDeConcentracao.SextaNoite)
        {
            return JanelasDeImpedimento.DiaDoTorneio(torneio, DayOfWeek.Friday) is DateTime sexta
                ? (sexta, sexta.AddDays(1))
                : null;
        }

        if (JanelasDeImpedimento.DiaDoTorneio(torneio, DayOfWeek.Saturday) is not DateTime sabado) return null;

        var corte = sabado.Add(JanelasDeImpedimento.CorteSabadoManhaTarde);

        return turno switch
        {
            TurnoDeConcentracao.SabadoManha => (sabado, corte),
            TurnoDeConcentracao.SabadoTarde => (corte, sabado.AddDays(1)),
            _ => null,
        };
    }

    // As janelas em que a dupla NÃO pode jogar a fase de grupos: tudo MENOS o turno escolhido.
    //
    // ⚠️ TURNO QUE O TORNEIO NÃO TEM NÃO BLOQUEIA NADA. É a trava mais importante daqui:
    // "só na sexta" num torneio que começa no sábado não pode virar "proibido em toda parte" —
    // isso deixaria a dupla sem horário possível, o oposto exato do favor que se pediu.
    public static IEnumerable<(DateTime Inicio, DateTime Fim)> Da(Torneio torneio, Dupla dupla)
    {
        if (dupla.ConcentrarJogosEm is not TurnoDeConcentracao turno
            || turno == TurnoDeConcentracao.Nenhuma) yield break;

        if (torneio.DataInicio is not DateTime inicio) yield break;
        if (JanelaDoTurno(torneio, turno) is not { } podeJogar) yield break;

        var antes = inicio.Date - Horizonte;
        var depois = inicio.Date + Horizonte;

        // Janela vazia não é devolvida: quando o turno escolhido é o próprio começo do
        // horizonte, o pedaço "antes" tem largura zero e não proíbe nada. Devolvê-lo só
        // encheria o mapa de entradas que o Encaixar checaria à toa.
        if (antes < podeJogar.Inicio) yield return (antes, podeJogar.Inicio);
        if (podeJogar.Fim < depois) yield return (podeJogar.Fim, depois);
    }

    // AS DUAS SE CONTRADIZEM? "Não posso sábado de manhã" + "os 2 jogos no sábado de manhã" não
    // deixa horário nenhum de pé.
    //
    // ⚠️ A GRADE CEDE NESSE CASO (jogo sem hora é pior que jogo fora do turno), e cede CALADA —
    // por isso a pergunta existe: quem avisa é a TELA. Sem ela o organizador acha que mandou e
    // não mandou, e só descobre no dia do jogo.
    //
    // Conflito é o turno PROIBIDO cobrir o turno ESCOLHIDO. Compara janela com janela, e não
    // enum com enum, porque as duas formas não se correspondem uma a uma: o impedimento de
    // sexta bloqueia a sexta INTEIRA, e o de sábado se parte no meio-dia.
    public static bool Conflita(Torneio torneio, Dupla dupla)
    {
        if (JanelaDoTurnoDaDupla(torneio, dupla) is not { } podeJogar) return false;

        return JanelasDeImpedimento.Da(torneio, dupla)
            .Any(proibida => proibida.Inicio < podeJogar.Fim && podeJogar.Inicio < proibida.Fim);
    }

    // A janela em que ESTA dupla pode jogar, ou null quando ela não tem concentração (ou o turno
    // não existe no calendário do torneio).
    public static (DateTime Inicio, DateTime Fim)? JanelaDoTurnoDaDupla(Torneio torneio, Dupla dupla) =>
        dupla.ConcentrarJogosEm is TurnoDeConcentracao turno && turno != TurnoDeConcentracao.Nenhuma
            ? JanelaDoTurno(torneio, turno)
            : null;

    // A recusa do lado do organizador. É a MESMA régua do impedimento — sem checagem de dono (ele
    // mexe no de outra pessoa, de propósito) e janela até o sorteio —, e por isso é delegada em
    // vez de copiada: duas cópias divergem, e aí a tela aceita o que o servidor recusa.
    public static string? MotivoParaOrganizadorNaoConcentrar(Dupla? dupla, Torneio? torneio, bool jaSorteou) =>
        AlteracaoDeImpedimento.MotivoParaOrganizadorNaoAlterar(dupla, torneio, jaSorteou);

    // ⚠️ ATÉ QUANDO A GRADE PRECISA IR pra que a concentração aconteça de verdade. Null quando
    // ninguém está concentrado — e aí nada muda pra torneio nenhum.
    //
    // Por que isto existe: o impedimento tira UMA janela de muitas, então sempre sobra grade
    // adiante. A concentração faz o CONTRÁRIO — tira todas menos uma. Se a lista de vagas
    // acabar antes do turno escolhido, o `Encaixar` cai no último recurso ("jogo sem horário é
    // pior que jogo fora do turno") e marca a dupla onde der: o favor não acontece, e ninguém
    // fica sabendo.
    //
    // E a lista acaba mesmo: ela é `jogos + margem` (Services/VagasDaGrade), o que num fim de
    // semana mal passa da manhã de sábado — a sexta abre às 18h e come as primeiras rodadas.
    // "Os 2 jogos no sábado à TARDE", uma das três opções que o Felipe pediu, era a que mais
    // tinha chance de sair calada. Ver VagasAlcancamAConcentracaoTests.
    public static DateTime? AteQuandoAGradePrecisaIr(Torneio torneio, IEnumerable<Dupla> duplas)
    {
        DateTime? maisTarde = null;

        foreach (var dupla in duplas)
        {
            if (dupla.ConcentrarJogosEm is not TurnoDeConcentracao turno
                || turno == TurnoDeConcentracao.Nenhuma) continue;

            if (JanelaDoTurno(torneio, turno) is not { } podeJogar) continue;

            if (maisTarde == null || podeJogar.Fim > maisTarde) maisTarde = podeJogar.Fim;
        }

        return maisTarde;
    }

    // Mesmo padrão de JanelasDeImpedimento.PorDupla: dupla sem concentração fica FORA do mapa.
    public static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> PorDupla(
        Torneio torneio, IEnumerable<Dupla> duplas)
    {
        var mapa = new Dictionary<int, (DateTime Inicio, DateTime Fim)[]>();
        foreach (var dupla in duplas)
        {
            var janelas = Da(torneio, dupla).ToArray();
            if (janelas.Length > 0) mapa[dupla.Id] = janelas;
        }
        return mapa;
    }

    public static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> PorDupla(Torneio torneio) =>
        PorDupla(torneio, torneio.Categorias.SelectMany(c => c.Duplas));

    // AS DUAS COISAS QUE ANDAM JUNTAS, num pacote só — as janelas proibidas E até onde a grade
    // precisa ir pra que elas deixem algo de pé.
    //
    // ⚠️ Não são dois parâmetros soltos de propósito: passar o mapa e esquecer o alcance é um
    // erro que COMPILA e que não quebra teste nenhum dos pedaços — só faz o favor sair calado
    // no torneio de verdade. Juntos, não há como passar um sem o outro.
    public sealed record Concentracoes(
        IReadOnlyDictionary<int, (DateTime Inicio, DateTime Fim)[]> Janelas,
        DateTime? AteQuando)
    {
        public static readonly Concentracoes Nenhuma =
            new(new Dictionary<int, (DateTime Inicio, DateTime Fim)[]>(), null);
    }

    public static Concentracoes De(Torneio torneio, IEnumerable<Dupla> duplas)
    {
        var lista = duplas as IReadOnlyCollection<Dupla> ?? duplas.ToList();
        return new Concentracoes(PorDupla(torneio, lista), AteQuandoAGradePrecisaIr(torneio, lista));
    }

    public static Concentracoes De(Torneio torneio) =>
        De(torneio, torneio.Categorias.SelectMany(c => c.Duplas).ToList());
}
