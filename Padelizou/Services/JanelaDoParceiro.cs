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
    // Recebe o STATUS e não o Torneio, pra falar a mesma língua dos dois serviços que já
    // decidiam isto (ConviteDeParceiro e MuralDeParceiros recebem `torneio?.Status`).
    public static string? MotivoParaNaoDefinir(Dupla? dupla, string? statusDoTorneio, bool jaComecouAJogar)
    {
        if (dupla == null || statusDoTorneio == null) return "Não encontrei essa inscrição.";

        // Time não tem parceiro: Jogador2Id nulo é a construção normal dele, e a lista de
        // times se altera na tela de times, pelo organizador.
        if (dupla.EhTime) return "Time não tem parceiro — a lista de times se altera em \"Gerenciar times e estrutura\".";

        if (dupla.Completa) return "Essa dupla já está completa.";

        if (CancelamentoDoTorneio.EstaCancelado(statusDoTorneio))
            return "Esse torneio foi cancelado.";

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
