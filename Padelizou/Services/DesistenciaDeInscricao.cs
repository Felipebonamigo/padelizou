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

    // Estava sozinho, ou os dois saem juntos: a inscrição acaba e a vaga volta pra fila.
    AInscricaoAcaba,
}

// O que a pessoa respondeu ao sair de uma dupla COMPLETA.
//
// Existe porque o padrão anterior — sai só quem clicou — estava certo pela metade. Ele protege
// o parceiro que continua querendo jogar, e essa continua sendo a resposta mais comum; mas a
// dupla que se inscreveu junto e desistiu junto era obrigada a clicar duas vezes, em duas
// contas. Na prática a segunda metade não clicava, e meia inscrição segurava a vaga até o
// encerramento — com a lista de espera parada atrás dela.
public enum EscolhaDeQuemSai
{
    // Saio eu, o parceiro fica inscrito e procura outro. A vaga NÃO abre.
    SoEu,

    // Saímos os dois: a inscrição acaba e a vaga volta pra fila.
    ADuplaInteira,
}

public static class DesistenciaDeInscricao
{
    // Devolve o motivo da recusa, ou null quando pode desistir.
    public static string? MotivoParaNaoDesistir(Dupla? dupla, Torneio? torneio, int quemPede)
    {
        if (dupla == null || torneio == null) return "Não encontrei essa inscrição.";

        // Time não desiste por aqui: o Jogador1Id dele é o organizador que o cadastrou, e
        // sem esta linha o organizador "desistiria" de um time pela porta do jogador.
        if (dupla.EhTime) return "Times são gerenciados pelo organizador na tela de times.";

        return TravasQueValemPraQualquerSaida(torneio, dupla.Jogador1Id == quemPede || dupla.Jogador2Id == quemPede);
    }

    // A mesma porta, pro Torneio Americano. A inscrição de Americano é individual e vive em
    // outra tabela (um jogador por linha, porque os parceiros mudam a cada rodada), então ela
    // não passa pelo caminho da Dupla — e sem esta função o jogador de Americano continuava
    // sem conseguir sair sozinho.
    public static string? MotivoParaNaoDesistirDoAmericano(InscricaoAmericana? inscricao, Torneio? torneio, int quemPede)
    {
        if (inscricao == null || torneio == null) return "Não encontrei essa inscrição.";

        return TravasQueValemPraQualquerSaida(torneio, inscricao.JogadorId == quemPede);
    }

    // As duas travas que valem igual pros dois tipos de inscrição. Ficam num lugar só de
    // propósito: escritas duas vezes, uma das cópias acabaria deixando sair depois do sorteio
    // — e aí a dupla some de uma chave já montada, com adversários contando com ela.
    private static string? TravasQueValemPraQualquerSaida(Torneio torneio, bool ehMinha)
    {
        // Só quem está NA inscrição. O organizador tem o caminho dele (RemoverDupla), com as
        // permissões dele — misturar os dois deixaria qualquer um tirando qualquer um.
        if (!ehMinha) return "Essa inscrição não é sua.";

        // Depois do sorteio a inscrição já está numa chave, com jogos marcados e adversários
        // contando com ela. Sair aí é assunto do organizador, que precisa remontar a grade.
        if (torneio.Status != "Inscrições Abertas")
            return "As inscrições já foram encerradas — fale com o organizador pra sair do torneio.";

        return null;
    }

    // Só a dupla COMPLETA tem duas saídas possíveis — e portanto só ela tem o que perguntar.
    // Quem está sozinho recebe um botão, não uma pergunta.
    public static bool TemEscolha(Dupla dupla) => dupla.Completa;

    public static EfeitoDaDesistencia Efeito(Dupla dupla, EscolhaDeQuemSai escolha) =>
        dupla.Completa && escolha == EscolhaDeQuemSai.SoEu
            ? EfeitoDaDesistencia.SoSaiQuemDesistiu
            : EfeitoDaDesistencia.AInscricaoAcaba;

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
