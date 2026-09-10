using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// ATÉ QUANDO DÁ PRA DEFINIR O SEGUNDO NOME DE UMA INSCRIÇÃO SOZINHA.
//
// 🗣️ Felipe, 09/09/2026: "tem q manter o Paulo, ele vai colocar o parceiro dele depois". A
// dupla sem parceiro passou a ENTRAR na chave (ver ForaDoSorteio) — e isso só serve pra alguma
// coisa com esta janela, porque até aqui os SEIS caminhos de fechar dupla (definir por CPF,
// gerar convite, aceitar convite, chamar no mural, aceitar chamado) exigiam
// `Status == "Inscrições Abertas"`, que já passou muito antes de a chave sair.
//
// 🕳️ E a tela já prometia esta janela ANTES de ela existir: o alerta amarelo do sorteio dizia
// "quem está sem parceiro ainda pode fechar a dupla" sendo renderizado em "Chaves em Sorteio",
// quando nenhum dos seis caminhos aceitava mais nada — e os botões nem eram desenhados. A tela
// mentia; esta régua é o que a torna verdade.
//
// ⚠️ É SÓ PRA DEFINIR O QUE FALTA, NUNCA PRA TROCAR QUEM JÁ ESTÁ. Trocar A por B numa chave
// sorteada bagunçaria jogos que os inscritos já estão vendo — isso continua preso em
// "Inscrições Abertas", onde sempre esteve (ver DuplasController.TrocarParceiro).
public static class JanelaDoParceiro
{
    // ⚠️ RECEBE O TORNEIO, e não (formato, status) soltos: a régua precisa dos DOIS campos, e
    // dois `string?` vizinhos são troca silenciosa esperando acontecer — passar status no lugar
    // do formato compila e abre a janela toda. Todo chamador tem o torneio na mão, inclusive a
    // view (lá ele é o próprio Model).
    public static string? MotivoParaNaoDefinir(Dupla? dupla, Torneio? torneio, bool jaComecouAJogar)
    {
        if (dupla == null || torneio == null) return "Não encontrei essa inscrição.";

        // Time não tem parceiro: Jogador2Id nulo é a construção normal dele, e a lista de
        // times se altera na tela de times, pelo organizador.
        if (dupla.EhTime) return "Time não tem parceiro — a lista de times se altera em \"Gerenciar times e estrutura\".";

        if (dupla.Completa) return "Essa dupla já está completa.";

        // ⚠️ NO AMERICANO INDIVIDUAL, `Jogador2Id` NULO NÃO É VAGA DE PARCEIRO — e o aviso já
        // estava escrito no Models/Dupla.cs. Lá a inscrição mora em InscricoesAmericanas: a
        // linha de Dupla é pareamento de rodada, ou o CARIMBO DE CAMPEÃO que a coroação grava
        // (RoboDoChaveamento.CoroarNoAmericanoAsync — sem NomeTime, sem Partida apontando pra
        // ela). Sem esta recusa, aquela linha parecia "inscrição sozinha com a janela aberta",
        // e como o campeão é o `Jogador1Id` dela ele passava no `ehDaDupla` do TrocarParceiro:
        // um POST em GerarConvite gerava link público pra pendurar um segundo nome no título.
        //
        // O Americano de DUPLAS não entra aqui: lá a inscrição É a dupla, e a vaga é real.
        if (torneio.Formato == FormatoDoTorneio.Americano)
            return "Nesse formato a inscrição é individual — não há parceiro pra definir.";

        if (CancelamentoDoTorneio.EstaCancelado(torneio.Status))
            return "Esse torneio foi cancelado.";

        // ⚠️ O TETO DA JANELA, E ELE NÃO PODE DEPENDER DO CLIQUE DO ORGANIZADOR. O `jaComecouAJogar`
        // logo abaixo é fato de operação: alguém carimbou o jogo na Mesa. Como o W.O. é lançado
        // À MÃO (decisão registrada no STATUS.md), o jogo da dupla em que ninguém apareceu fica
        // "Agendada" pra sempre — e sem esta linha o convite continuaria valendo DEPOIS do
        // torneio acabado. Fechar a dupla ali cobraria a diferença da inscrição num torneio
        // encerrado e pagaria ponto de participação RETROATIVO aos dois jogadores (a dupla sai
        // de "incompleta, não conta" pra "completa, conta", com UltimaFase nascida "Grupos"),
        // inflando de tabela o peso da categoria pra todo mundo dela. É a lista de estragos do
        // cabeçalho de InscricaoQueConta entrando pela porta de trás.
        //
        // Também é o teto de quem NUNCA teve jogo: categoria com uma inscrição só não gera
        // Partida nenhuma (GerarChaves pula categoria com menos de 2), então ali o
        // `jaComecouAJogar` seria falso pra sempre.
        if (torneio.Status == "Finalizado")
            return "Esse torneio já terminou — não dá mais pra definir o parceiro.";

        // A bola já rolou pra ESTA dupla: o jogo aconteceu (ou está acontecendo) com a vaga
        // vazia, e pendurar um nome nele depois seria reescrever o que já foi jogado.
        if (jaComecouAJogar)
            return "Essa dupla já entrou em quadra — não dá mais pra definir o parceiro.";

        return null;
    }

    // O FATO, apurado no banco e passado de fora pra régua acima — mesmo padrão do `jaSorteou`
    // de AlteracaoDeImpedimento: a régua fica pura e testável, a consulta fica num lugar só.
    //
    // ⚠️ NÃO OLHA PLACAR, E ISSO É DE PROPÓSITO. `Partida.GamesDupla1/2` e `SetsDupla1/2` são
    // `int?` com `HasDefaultValue(0)` no Postgres: nascem 0 em produção e NULOS no EF InMemory
    // da suíte. Uma régua escrita sobre "games == null" fecharia a janela em produção no
    // segundo em que a chave saísse — e passaria verde nos ~5.700 testes daqui. O que diz que
    // a bola rolou é o `Status` (Finalizada) ou o carimbo de início real.
    //
    // Partida CANCELADA não conta: ela não foi jogada, e o jogo pode ser remarcado.
    public static Task<bool> JaComecouAJogarAsync(DbPadelContext ctx, int duplaId) =>
        ctx.Partidas.AnyAsync(p => (p.Dupla1Id == duplaId || p.Dupla2Id == duplaId)
                                && (p.Status == "Finalizada" || p.HorarioInicioReal != null));
}
