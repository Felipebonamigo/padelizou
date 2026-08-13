using Padelizou.Models;

namespace Padelizou.Services;

// Duplicar torneio: o organizador que faz o mesmo torneio todo mês recriava tudo à mão —
// categorias, preços, formato, quadras. Um clique copia a ESTRUTURA e nada do que aconteceu.
//
// A decisão central mora nas duas listas abaixo: TODA propriedade do Torneio está em uma das
// duas, e um teste de reflexão recusa propriedade nova que não escolheu lado. Sem isso, o
// campo que alguém criar ano que vem entraria na cópia (ou ficaria de fora) em silêncio —
// e é assim que carimbo de aviso viaja pra edição nova e reenvia push, ou que uma
// configuração some entre edições sem ninguém decidir.
public static class DuplicacaoDeTorneio
{
    // O que É estrutura: viaja pra edição nova.
    public static readonly string[] Copiadas =
    {
        // O jeito do torneio
        nameof(Torneio.Formato),
        nameof(Torneio.FormatoUnico),
        nameof(Torneio.SetsFaseGrupos),
        nameof(Torneio.GamesFaseGrupos),
        nameof(Torneio.SetsFaseMataMata),
        nameof(Torneio.GamesFaseMataMata),
        nameof(Torneio.SetsFaseFinal),
        nameof(Torneio.GamesFaseFinal),
        nameof(Torneio.ContagemDeGames),
        nameof(Torneio.TamanhoGrupo),
        nameof(Torneio.ClassificadosPorGrupo),
        nameof(Torneio.DesempateAmericano),
        nameof(Torneio.PontuaNoRankingAmericano),

        // Onde e como acontece
        nameof(Torneio.ClubeId),
        nameof(Torneio.LocalTorneio),
        nameof(Torneio.QuantidadeQuadras),
        nameof(Torneio.TempoPrevistoPartidaMinutos),
        nameof(Torneio.SemHorarioPrevisto),
        nameof(Torneio.HoraInicioDoDia),
        nameof(Torneio.HoraInicioDiasSeguintes),
        nameof(Torneio.HoraFimDoDia),

        // Dinheiro
        nameof(Torneio.PrecoInscricao),
        nameof(Torneio.PrecoSegundaInscricao),
        nameof(Torneio.FormaPagamento),
        nameof(Torneio.ModoComissao),
        nameof(Torneio.PagamentoObrigatorioNaInscricao),
        nameof(Torneio.ExcluirSeNaoPagar),
        nameof(Torneio.ChavePixOrganizador),
        nameof(Torneio.TaxaPorImpedimento),

        // Impedimentos
        nameof(Torneio.PermiteImpedimentos),
        nameof(Torneio.PermiteImpedimentoQuintaNoite),
        nameof(Torneio.PermiteImpedimentoSextaNoite),
        nameof(Torneio.PermiteImpedimentoSabadoManha),
        nameof(Torneio.PermiteImpedimentoSabadoTarde),

        // Quem pode entrar
        nameof(Torneio.RestricaoCategoria),
        nameof(Torneio.BloquearCategoriaInferior),
        nameof(Torneio.PermiteMultiplasCategorias),
        nameof(Torneio.LimiteDuplasTotal),
        nameof(Torneio.Restrito),
        nameof(Torneio.ValidarPeloRankingRs),
        nameof(Torneio.Oculto),

        // Recursos ligados
        nameof(Torneio.UsaCheckIn),
        nameof(Torneio.UsaVotacaoDeMvp),

        // O grupo do WhatsApp costuma ser o mesmo entre edições — e é editável na gestão.
        nameof(Torneio.LinkGrupoWhatsApp),
    };

    // O que NÃO viaja: identidade, datas, carimbos e tudo que pertence à edição que já
    // aconteceu. Cada um é resolvido de propósito (novo valor, ou fica no padrão do modelo).
    public static readonly string[] NaoViajam =
    {
        nameof(Torneio.Id),                             // novo
        nameof(Torneio.Nome),                           // NomeParaEdicaoNova decide
        nameof(Torneio.Codigo),                         // sorteado de novo
        nameof(Torneio.Status),                         // nasce com inscrições FECHADAS
        nameof(Torneio.TorneioOrigemId),                // RaizDaSerie decide
        nameof(Torneio.ChaveAcesso),                    // torneio restrito ganha chave NOVA
        nameof(Torneio.ImagemCapa),                     // o ARQUIVO é copiado no controller

        // Datas são da edição — a nova ainda não tem nenhuma.
        nameof(Torneio.DataInicio),
        nameof(Torneio.DataFim),
        nameof(Torneio.PrazoPagamento),
        nameof(Torneio.PrevisaoEncerramentoInscricoes),
        nameof(Torneio.PrevisaoChaveamento),

        // Aprovação é POR torneio: a edição nova volta pra fila do admin.
        nameof(Torneio.AprovadoEm),
        nameof(Torneio.AprovadoPorId),

        // Carimbos de coisas que JÁ aconteceram — viajar reenviaria (ou calaria) avisos.
        nameof(Torneio.PerguntaDeNaoPagosEm),
        nameof(Torneio.AvisoDeMvpEnviadoEm),
        nameof(Torneio.RankingAmericanoPagoEm),
        nameof(Torneio.TaxaExternoPagaEm),
        nameof(Torneio.TaxaExternoNegociadaEm),
        nameof(Torneio.TaxaExternoNegociadaObs),

        // Da edição que passou.
        nameof(Torneio.RecadoAosInscritos),
        nameof(Torneio.LinkDasFotos),
        nameof(Torneio.MotivoCancelamento),
        nameof(Torneio.CanceladoEm),
    };

    // A raiz da série: duplicar uma duplicata herda a origem DELA, então toda edição aponta
    // pro mesmo torneio-raiz e "todas as edições" é uma consulta só.
    public static int RaizDaSerie(Torneio original) => original.TorneioOrigemId ?? original.Id;

    // O nome da edição nova. O nome original serve quando está livre (o caso comum: a edição
    // anterior já acabou, e NomeDoTorneio libera nome de torneio finalizado). Preso, ganha
    // um número — "Copa do Clube 2", "Copa do Clube 3" — até achar um livre.
    public static string NomeParaEdicaoNova(
        string nomeOriginal, IEnumerable<(string Nome, string? Status)> torneiosDoOrganizador)
    {
        var torneios = torneiosDoOrganizador.ToList();

        if (NomeDoTorneio.JaExisteEntre(torneios, nomeOriginal) == null) return nomeOriginal;

        for (int n = 2; n <= 99; n++)
        {
            var candidato = $"{nomeOriginal} {n}";
            if (NomeDoTorneio.JaExisteEntre(torneios, candidato) == null) return candidato;
        }

        // 98 torneios de pé com o mesmo nome não é um cenário — é um teste de estresse. Mas
        // devolver algo é melhor que estourar: o Create recusaria o repetido com a mensagem
        // de sempre.
        return nomeOriginal;
    }

    // A MESMA cópia, mas para PRÉ-PREENCHER O FORMULÁRIO de criação ("copiar configurações do
    // último torneio"). A diferença com o `CopiarConfiguracao` é de INTENÇÃO, e ela importa:
    //
    // · "Criar a próxima edição" (botão dentro do torneio) diz *este torneio de novo* — a
    //   edição entra na SÉRIE, e o ranking do circuito soma as duas.
    // · "Copiar configurações" (tela de criar) diz *um torneio novo, do meu jeito de sempre* —
    //   o nome vai ser outro, e somar isso num circuito juntaria dois eventos diferentes num
    //   ranking só. Por isso aqui a série NÃO é herdada.
    //
    // O nome também não vem: o campo fica em branco de propósito, porque é a primeira coisa
    // que a pessoa vai escrever — e um nome já preenchido é o jeito mais fácil de publicar
    // "Copa de Agosto" em setembro sem perceber.
    public static Torneio ParaOFormularioDeCriacao(Torneio original)
    {
        var novo = CopiarConfiguracao(original);
        novo.TorneioOrigemId = null;
        novo.Nome = "";
        return novo;
    }

    // A cópia em si, guiada pela lista — a lista É o comportamento, não uma documentação dele.
    public static Torneio CopiarConfiguracao(Torneio original)
    {
        var novo = new Torneio
        {
            Nome = original.Nome,   // provisório; quem decide é NomeParaEdicaoNova
            Codigo = "",            // provisório; quem decide é o controller
            Status = PortaDaInscricao.Fechada,
            TorneioOrigemId = RaizDaSerie(original),
        };

        foreach (var nome in Copiadas)
        {
            var propriedade = typeof(Torneio).GetProperty(nome)!;
            propriedade.SetValue(novo, propriedade.GetValue(original));
        }

        return novo;
    }
}
