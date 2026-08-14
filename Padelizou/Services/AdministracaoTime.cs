using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quem pode mexer num time, e quem pode dar esse poder a outra pessoa.
//
// A regra do Felipe, em uma frase: um time pode ter VÁRIOS administradores; quem coloca o
// primeiro é um admin do sistema, e a partir daí um administrador do time coloca o próximo.
//
// Fica num serviço, e não espalhado nos controllers, porque é regra de autorização: a
// varredura de 26/07 achou dois buracos que existiam justamente por a checagem estar
// copiada de um lado e esquecida do outro.
public static class AdministracaoTime
{
    // Pode editar o time (nome, logo, clube) e mexer na lista de administradores?
    public static bool PodeGerenciar(bool ehAdminDoSistema, bool ehAdminDoTime) =>
        ehAdminDoSistema || ehAdminDoTime;

    // Motivo pra recusar a concessão, ou null se pode conceder.
    // Devolve texto porque quem chama mostra direto na tela — e um "não pode" sem motivo
    // é o tipo de tela sem saída que a Mesa de Controle já foi.
    public static string? ProblemaParaConceder(
        bool quemPedeEhAdminDoSistema, bool quemPedeEhAdminDoTime, bool alvoJaEAdmin)
    {
        if (!PodeGerenciar(quemPedeEhAdminDoSistema, quemPedeEhAdminDoTime))
            return "Só um administrador deste time (ou um administrador do Padelizou) pode incluir outro.";

        if (alvoJaEAdmin)
            return "Essa pessoa já administra o time.";

        return null;
    }

    // Motivo pra recusar a remoção, ou null se pode remover.
    public static string? ProblemaParaRemover(
        bool quemPedeEhAdminDoSistema, bool quemPedeEhAdminDoTime, bool alvoEAdmin, int quantosAdministradores)
    {
        if (!PodeGerenciar(quemPedeEhAdminDoSistema, quemPedeEhAdminDoTime))
            return "Só um administrador deste time (ou um administrador do Padelizou) pode remover outro.";

        if (!alvoEAdmin)
            return "Essa pessoa não administra o time.";

        // Deixar o time sem ninguém devolveria ele pro estado dos 44 importados: só um admin
        // do sistema conseguiria mexer de novo. Quem quer sair chama o outro administrador
        // primeiro — e um admin do sistema sempre consegue destravar.
        if (quantosAdministradores <= 1 && !quemPedeEhAdminDoSistema)
            return "Este é o único administrador do time. Inclua outro antes de remover este.";

        return null;
    }

    // Concede o cargo — e veste a camisa junto, porque as duas coisas andam juntas: sem isto
    // o administrador ficaria de fora da própria lista de membros, e a tela do time diria
    // "não tem ninguém" com ele lá dentro.
    //
    // Mora aqui, e não copiado nas duas telas que concedem (a vitrine /Times e o painel
    // /Admin/Times), porque é UMA regra. Regra duplicada é o que já produziu os piores bugs
    // deste projeto: a segunda cópia é corrigida meses depois da primeira, ou nunca.
    //
    // Não chama SaveChanges — quem chama decide quando gravar.
    public static void Conceder(DbPadelContext context, int timeId, Jogador novo, int? concedidoPorId)
    {
        context.Set<TimeAdministrador>().Add(new TimeAdministrador
        {
            TimeId = timeId,
            JogadorId = novo.Id,
            ConcedidoPorId = concedidoPorId,
            ConcedidoEm = DateTime.Now,
        });

        // Vestir a camisa é uma TRANSFERÊNCIA como qualquer outra, e passa pelo mesmo lugar:
        // quem vinha de outro time aparece na janela de transferências igual a quem trocou
        // pelo perfil. Ver Services/TransferenciasDeTime — ele já ignora quem sai e entra no
        // mesmo time, então não precisa do "if" que havia aqui.
        TransferenciasDeTime.Registrar(context, novo, timeId);
    }

    // O PRESIDENTE: o administrador mais antigo do time.
    //
    // Não é uma coluna nova, e isso é decisão, não preguiça. `Time.DonoId` já existiu e foi
    // derrubado justamente por criar uma segunda resposta pra "quem manda neste time?" —
    // ressuscitá-lo com outro nome reabriria o mesmo buraco, agora com a pergunta valendo em
    // duas tabelas ao mesmo tempo.
    //
    // O administrador mais antigo É o presidente na prática: quem cria o time vira o primeiro
    // administrador dele (AuthController), e nos 44 times importados do ranking o primeiro é
    // quem um admin do Padelizou escolheu pra comandar. Todos continuam com o MESMO poder —
    // presidência aqui é quem representa o time na vitrine, não um poder a mais.
    //
    // Null quando o time não tem administrador nenhum, que é o estado da maioria dos
    // importados: a tela diz que não há presidente em vez de eleger alguém por conta própria.
    public static T? Presidente<T>(IEnumerable<T> administradores, Func<T, DateTime> concedidoEm)
        where T : class =>
        administradores.OrderBy(concedidoEm).FirstOrDefault();

    public static Task<bool> EhAdministradorAsync(DbPadelContext context, int timeId, int jogadorId) =>
        context.Set<TimeAdministrador>().AnyAsync(a => a.TimeId == timeId && a.JogadorId == jogadorId);

    public static Task<List<int>> TimesQueAdministraAsync(DbPadelContext context, int jogadorId) =>
        context.Set<TimeAdministrador>()
            .Where(a => a.JogadorId == jogadorId)
            .Select(a => a.TimeId)
            .ToListAsync();
}
