using Padelizou.Models;

namespace Padelizou.Services;

// O MURAL "PROCURO DUPLA": quem se inscreveu sem parceiro já aparece na lista pública de
// inscritos ("Procurando parceiro") — o mural é dar AÇÃO a essa visibilidade: qualquer
// jogador logado manda o "quero jogar com você" num clique.
//
// O chamado é só o recado. Quem fecha a dupla é o DONO da inscrição, pelo convite por link
// que já existe — o mural não mexe em inscrição de ninguém, e é por isso que ele não
// precisa de regra de preço, de ranking nem de vaga: tudo isso mora no caminho que fecha.
public static class MuralDeParceiros
{
    // Devolve o motivo da recusa, ou null quando o chamado pode ir. TODA a validação é do
    // servidor: a tela só esconde botão, e POST montado à mão não passa por view nenhuma.
    public static string? MotivoParaNaoChamar(Dupla? dupla, string? statusDoTorneio, int candidatoId)
    {
        if (dupla == null) return "Inscrição não encontrada.";
        if (dupla.EhTime) return "Linha de time não procura parceiro.";
        if (dupla.Jogador2Id != null) return "Essa dupla já fechou.";
        if (dupla.Jogador1Id == candidatoId) return "Essa inscrição é a sua.";
        // Fechou a inscrição, fechou o mural: chamar alguém pra um torneio que não aceita
        // mais dupla é gerar conversa sem saída.
        if (statusDoTorneio != PortaDaInscricao.Aberta) return "As inscrições deste torneio já fecharam.";
        return null;
    }

    public const string TituloDoAviso = "Alguém quer fechar dupla com você";

    // ⚠️ O texto mudou junto com o destino do aviso (17/08/2026): ele apontava pro PERFIL do
    // candidato e mandava "mande o convite por link da sua inscrição" — o aviso terminava
    // numa tarefa manual. Agora o toque cai na tela de Chamados, que tem Aceitar e Recusar,
    // e o texto precisa prometer exatamente o que a tela entrega.
    public static string CorpoDoAviso(string candidato, string torneio) =>
        $"{candidato} quer jogar o {torneio} com você. Toque pra ver o perfil e responder.";

    // Por que este candidato não pode mais ser aceito. Null = pode fechar a dupla com ele.
    //
    // Vive aqui, e não no controller, porque a tela também pergunta: ela precisa saber se
    // desenha o botão Aceitar ou uma explicação no lugar dele. Duas cópias e a tela ofereceria
    // um botão que o servidor recusa — desleixo que o usuário paga.
    public static string? MotivoParaNaoAceitar(Dupla? dupla, string? statusDoTorneio, int donoLogadoId)
    {
        if (dupla == null) return "Inscrição não encontrada.";
        if (dupla.Jogador1Id != donoLogadoId) return "Essa inscrição não é sua.";
        if (dupla.EhTime) return "Linha de time não tem parceiro pra escolher.";
        if (dupla.Jogador2Id != null) return "Essa dupla já fechou.";

        // Inscrição fechada, decisão fechada: aceitar aqui colocaria alguém num torneio que
        // não aceita mais dupla — e o chaveamento já pode ter saído.
        if (statusDoTorneio != PortaDaInscricao.Aberta) return "As inscrições deste torneio já fecharam.";
        return null;
    }
}
