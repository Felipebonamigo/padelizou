namespace Padelizou.Services;

using Padelizou.Models;

// O QUADRO do mata-mata: as fases empilhadas que a aba "Chaves e Grupos" desenha
// (Views/Torneios/_ChaveDoMataMata) — os jogos reais numerados e, abaixo, as vagas
// futuras com a procedência de cada lado ("quem ganhar o jogo 3"). O pareamento é o
// mesmo do robô: vencedores na ordem dos jogos + byes do melhor pro pior, primeiro
// contra último (Services/AvancoDaChave + ParearVencedores) — divergir daqui é o quadro
// prometer um confronto e o robô criar outro.
//
// A montagem morava solta no Razor, sem teste nenhum. Virou serviço por dois motivos:
// pra numeração/pareamento ganharem teste, e pra PINTAR O CAMINHO DO JOGADOR sem copiar
// regra — quais vagas futuras "ainda podem ser dele" é decisão de Services/MeusJogos,
// a MESMA corrente do filtro "Meus jogos" da aba Jogos. Aqui só se traduz o quadro pro
// formato que ela pede e se lê a resposta de volta.
public static class QuadroDoMataMata
{
    // Um lado de vaga futura: ou aponta pro jogo de onde vem o vencedor (`VemDoJogo`,
    // na numeração DESTE quadro), ou é a dupla que folgou a primeira rodada — que entra
    // NOMEADA no jogo futuro, senão ela some do desenho (24 na chave, 16 visíveis).
    // `JaVenceu` é o jogo de procedência que JÁ TERMINOU: a vaga passa a mostrar o nome de
    // quem ganhou em vez de "quem ganhar o jogo 12", sem esperar o resto da rodada. Quem
    // venceu quer saber na hora que passou e contra quem — e a informação já está na tela,
    // dois cartões acima. A PARTIDA da fase seguinte continua nascendo só quando a rodada
    // fecha (Services/AvancoDaChave): meia chave viraria jogo sem adversário, e o
    // agendamento precisa da rodada inteira pra distribuir hora e quadra.
    public record Lado(int? VemDoJogo, Dupla? DuplaDeBye, Dupla? JaVenceu = null)
    {
        public bool EhBye => DuplaDeBye != null;

        // O lado já tem dono — por ter folgado ou por ter vencido. Quem tem nome não fica
        // apagado no desenho: é informação firme, não promessa.
        public Dupla? ComNome => DuplaDeBye ?? JaVenceu;
    }

    // Uma vaga do quadro: jogo REAL (Jogo != null) ou vaga futura (Lado1/Lado2).
    // `EhMinha` é o caminho do jogador logado — jogo em que ele está (inclusive o que já
    // perdeu: faz parte do trajeto), ou vaga futura que ainda pode ser dele.
    public record Vaga(int Numero, Partida? Jogo, Lado? Lado1, Lado? Lado2, bool EhMinha);

    public record Fase(string Nome, List<Vaga> Vagas);

    // `byes` na ordem do pareamento (melhor → pior) — o desenho conta com ela.
    public static List<Fase> Montar(
        IReadOnlyList<Partida> jogos, IReadOnlyList<Dupla> byes, int? meuJogadorId)
    {
        var ordemDasFases = new[]
        {
            ChaveamentoMataMata.PrimeiraRodada,
            "Oitavas de Final", "Quartas de Final", "Semifinal", "Final"
        };

        var fases = new List<Fase>();
        int primeira = Array.FindIndex(ordemDasFases, f => jogos.Any(j => j.Fase == f));
        if (primeira < 0) return fases;

        static Dupla? VencedoraDe(Partida p) => p.VencedorId == null ? null
            : p.VencedorId == p.Dupla1Id ? p.Dupla1 : p.Dupla2;

        bool MinhaDupla(Dupla? d) => meuJogadorId != null && d != null &&
            (d.Jogador1Id == meuJogadorId || d.Jogador2Id == meuJogadorId);
        bool MeuJogo(Partida p) => MinhaDupla(p.Dupla1) || MinhaDupla(p.Dupla2);
        // Só jogo FINALIZADO conta como perdido — em quadra ou agendado ainda pode ser meu.
        bool Perdido(Partida p) => p.Status == "Finalizada" && p.VencedorId != null &&
            !MinhaDupla(p.VencedorId == p.Dupla1Id ? p.Dupla1 : p.Dupla2);

        // A tradução pro formato de MeusJogos.Filtrar: a regra fala em (fase, número na
        // fase), o quadro numera globalmente — `ondeEsta` é a ponte entre os dois.
        var reaisTraduzidos = new List<MeusJogos.JogoReal>();
        var projetadosTraduzidos = new List<ProximasFasesDaChave.JogoQueVem>();
        var ondeEsta = new Dictionary<int, (string Fase, int Ordem)>();

        // O quadro é de UMA categoria só, então ela não precisa aparecer na chave da
        // procedência — mas o campo existe na regra e tem que ser o mesmo dos dois lados.
        const string categoria = "";

        ProximasFasesDaChave.Lado Traduzir(Lado lado) => lado.VemDoJogo is int n
            ? new("", ondeEsta[n].Fase, ondeEsta[n].Ordem)
            : new(lado.DuplaDeBye!.NomeDeExibicao);

        // Quem entra na fase seguinte, na ordem do pareamento do robô. Numeração GLOBAL
        // na ordem de criação — a mesma que o robô lê, então "quem ganhar o jogo 3" aqui
        // e na chave de verdade são o mesmo jogo.
        var entrantes = new List<Lado>();
        int numero = 1;

        for (int i = primeira; i < ordemDasFases.Length; i++)
        {
            var faseAtual = ordemDasFases[i];
            var reais = jogos.Where(j => j.Fase == faseAtual).OrderBy(j => j.Id).ToList();
            var vagas = new List<Vaga>();

            if (reais.Count == 0 && entrantes.Count < 2) break;

            // Fase com jogo REAL fica com o nome dos próprios jogos — com bye ela tem MENOS
            // jogos que o quadro promete, e batizá-la pela contagem rebaixaria uma Primeira
            // Rodada de 2 jogos a "Semifinal". Fase só futura é batizada pelo tamanho (vagas ×
            // 2 lados), senão uma chave curta termina com a final rotulada "Quartas".
            string nomeDaFase = reais.Count > 0 ? faseAtual : ChaveamentoMataMata.NomeFase(entrantes.Count);

            // ⚠️ QUANTOS JOGOS ESTA FASE TEM QUANDO CHEIA — e não quantos já existem. Desde o
            // avanço parcial (11/09/2026, Services/AvancoDaChave) uma fase fica um tempo PELA
            // METADE: a Semifinal 1 nasce assim que a Quartas 1 termina, com a 2 ainda em
            // quadra. Lendo só `reais.Count`, o quadro perderia a Semifinal 2 e a Final — e a
            // chave pararia de mostrar o caminho justamente pra quem acabou de classificar.
            // `Max` com os reais é rede: dado torto que traga mais jogo do que a rodada
            // anterior comporta aparece no quadro em vez de sumir dele.
            int esperados = Math.Max(reais.Count, entrantes.Count / 2);

            var proximos = new List<Lado>();

            for (int k = 0; k < esperados; k++)
            {
                Vaga vaga;
                if (k < reais.Count)
                {
                    var jogo = reais[k];
                    vaga = new Vaga(numero, jogo, null, null, MeuJogo(jogo));
                    reaisTraduzidos.Add(new MeusJogos.JogoReal(
                        categoria, nomeDaFase, k + 1, MeuJogo(jogo), Perdido(jogo)));
                    proximos.Add(new Lado(numero, null, VencedoraDe(jogo)));
                }
                else
                {
                    vaga = new Vaga(numero, null,
                        entrantes[k], entrantes[entrantes.Count - 1 - k], EhMinha: false);
                    projetadosTraduzidos.Add(new ProximasFasesDaChave.JogoQueVem(
                        categoria, nomeDaFase, k + 1, null,
                        Traduzir(vaga.Lado1!), Traduzir(vaga.Lado2!)));
                    proximos.Add(new Lado(numero, null));
                }

                vagas.Add(vaga);
                ondeEsta[numero] = (nomeDaFase, k + 1);
                numero++;
            }

            // ⚠️ O BYE ENTRA UMA VEZ SÓ, na fase seguinte à que ele folgou. Antes o `AddRange`
            // se repetia em toda fase real e era inócuo porque a lista de byes se esvaziava
            // sozinha assim que a segunda fase nascia. Ela não se esvazia mais — é estável de
            // propósito, pra sobreviver ao avanço parcial (ver AvancoDaChave) —, então quem
            // filtra é aqui: sem isto, a Final sairia com quatro entrantes.
            if (i == primeira) proximos.AddRange(byes.Select(b => new Lado(null, b)));
            entrantes = proximos;

            fases.Add(new Fase(nomeDaFase, vagas));

            if (vagas.Count == 1 && reais.Count > 0 && faseAtual == "Final") break;
            if (entrantes.Count < 2) break;
        }

        // A resposta da regra, lida de volta pelo quadro: quais vagas futuras ainda
        // podem ser do jogador. Categoria em grupos não existe aqui — quadro desenhado
        // é chave que já começou.
        if (meuJogadorId != null)
        {
            var byesComMeuNome = byes.Where(MinhaDupla)
                .Select(b => b.NomeDeExibicao).ToHashSet();
            var minhas = MeusJogos
                .Filtrar(projetadosTraduzidos, reaisTraduzidos, byesComMeuNome, [])
                .Select(j => (j.Fase, j.Numero))
                .ToHashSet();

            foreach (var fase in fases)
                for (int v = 0; v < fase.Vagas.Count; v++)
                    if (fase.Vagas[v].Jogo == null && minhas.Contains((fase.Nome, v + 1)))
                        fase.Vagas[v] = fase.Vagas[v] with { EhMinha = true };
        }

        return fases;
    }
}
