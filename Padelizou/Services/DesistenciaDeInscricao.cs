using Padelizou.Models;

namespace Padelizou.Services;

// O que acontece quando o próprio inscrito desiste do torneio.
//
// Antes só o organizador tirava alguém (RemoverDupla), então desistir era mandar mensagem pra
// ele — que mandava mensagem pro Felipe. Trabalho manual pra uma coisa que o jogador resolve
// sozinho em dois toques.
//
// A regra que importa: quem desiste sai, o PARCEIRO não é arrastado junto. Ele estava inscrito
// também, muitas vezes já pagou, e perder a vaga porque o outro desistiu seria punir o errado.
// Por isso a dupla completa não some — ela fica com uma cadeira vazia (igualzinho a quem se
// inscreveu "ainda não tenho parceiro") e o que ficou tem até o encerramento pra achar outro.
//
// Já quem estava sozinho leva a inscrição embora: sem ninguém pra segurar a vaga, ela volta
// pro torneio e a lista de espera anda.
public enum EfeitoDaDesistencia
{
    // Dupla completa: sai um, o outro continua inscrito e sem parceiro. A vaga NÃO abre.
    SoSaiQuemDesistiu,

    // Estava sozinho: a inscrição acaba e a vaga volta pra fila.
    AInscricaoAcaba,
}

public static class DesistenciaDeInscricao
{
    // Devolve o motivo da recusa, ou null quando pode desistir.
    public static string? MotivoParaNaoDesistir(Dupla? dupla, Torneio? torneio, int quemPede)
    {
        if (dupla == null || torneio == null) return "Não encontrei essa inscrição.";

        // Só quem está NA dupla. O organizador tem o caminho dele (RemoverDupla), com as
        // permissões dele — misturar os dois deixaria qualquer um tirando qualquer um.
        if (dupla.Jogador1Id != quemPede && dupla.Jogador2Id != quemPede)
            return "Essa inscrição não é sua.";

        // Depois do sorteio a dupla já está numa chave, com jogos marcados e adversários
        // contando com ela. Sair aí é assunto do organizador, que precisa remontar a grade.
        if (torneio.Status != "Inscrições Abertas")
            return "As inscrições já foram encerradas — fale com o organizador pra sair do torneio.";

        return null;
    }

    public static EfeitoDaDesistencia Efeito(Dupla dupla) =>
        dupla.Completa ? EfeitoDaDesistencia.SoSaiQuemDesistiu : EfeitoDaDesistencia.AInscricaoAcaba;

    // Quem CONTINUA inscrito depois que `quemDesistiu` sai — null quando a inscrição acaba.
    //
    // Devolve o id porque o chamador precisa promover esse jogador pro lugar de Jogador1:
    // a coluna não é anulável, então quando o titular desiste é o parceiro que assume a
    // cadeira, e a de parceiro fica livre.
    public static int? QuemFica(Dupla dupla, int quemDesistiu)
    {
        if (!dupla.Completa) return null;
        return dupla.Jogador1Id == quemDesistiu ? dupla.Jogador2Id : dupla.Jogador1Id;
    }
}
