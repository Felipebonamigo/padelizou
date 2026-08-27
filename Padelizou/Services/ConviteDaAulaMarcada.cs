using padelizou.Models;

namespace Padelizou.Services;

// "AULA MARCADA — CONFIRMA?", o recado que o PROFESSOR manda no WhatsApp dele.
//
// Pedido de um professor (28/08/2026, pelo Felipe): "queria que mandasse uma mensagem para os
// alunos para confirmar quando eu marcar a aula no Padelizou, até pra eles fazer cadastro".
//
// ⚠️ QUEM MANDA É O PROFESSOR, num toque no botão — não é envio automático pelo chip do
// Padelizou, e isso é decisão, não preguiça:
//   • o aluno responde "sim" pra quem ele conhece, não pra um número desconhecido;
//   • disparo automático pra número que nunca consentiu é como chip pré-pago é denunciado e
//     morre — e o chip é o ponto frágil da infra de WhatsApp deste projeto;
//   • o "sim" volta na conversa DELE, onde ele já combina tudo. Ninguém precisa escrever um
//     processador de resposta, que não existe e não vale a pena existir por isto.
//
// O envio AUTOMÁTICO continua valendo pra quem tem conta e consentiu (Services/AvisoPorWhatsApp,
// disparado pelo PushNotificationService). Este recado é pro aluno que ainda não tem nada — o
// mesmo que hoje não recebe aviso nenhum quando a aula é marcada.
public static class ConviteDaAulaMarcada
{
    // Este aluno ganha o botão?
    //
    // ⚠️ ALUNO COM CONTA FICA DE FORA, e é o ponto todo: ele já recebe o aviso automático.
    // Oferecer o botão pra ele faria o professor mandar a mesma coisa duas vezes — e é assim
    // que se ensina alguém a ignorar os dois avisos.
    //
    // A régua do número é a MESMA do link que a pessoa clica (WhatsAppLinkHelper.NumeroValido):
    // duas cópias divergiriam no dia em que um formato novo aparecesse.
    public static bool CabeConvite(int? alunoId, string? celular) =>
        alunoId == null && WhatsAppLinkHelper.NumeroValido(celular);

    // O texto pronto. Curto de propósito: é mensagem de WhatsApp, não e-mail — o professor
    // ainda pode editar antes de mandar, porque o `wa.me` abre a conversa com o texto no campo.
    //
    // A PERGUNTA é o motivo de a mensagem existir ("confirmar horário?"). Sem ela vira aviso,
    // e o professor pediu confirmação. O LINK DE CADASTRO fecha a segunda metade do pedido
    // ("até pra eles fazer cadastro"): sem ele, o aluno não tem caminho nenhum pra dentro.
    public static string Texto(Aula aula, string professor, string linkDeCadastro)
    {
        var nome = string.IsNullOrWhiteSpace(aula.NomeAlunoAvulso) ? "tudo bem" : aula.NomeAlunoAvulso.Trim();

        // Endereço entra quando existe: o nome do clube leva quem já conhece, e não leva quem
        // nunca foi. Sem ele, o local sai limpo — nada de vírgula solta pendurada no nome.
        var local = string.IsNullOrWhiteSpace(aula.LocalAula?.Endereco)
            ? aula.LocalAula?.Nome ?? "a definir"
            : $"{aula.LocalAula.Nome}, {aula.LocalAula.Endereco.Trim()}";

        return $"Olá, {nome}! Sua aula foi agendada pelo Padelizou: "
             + $"{aula.DataHora:dd/MM} às {aula.DataHora:HH:mm}, em {local}, com {professor}. "
             + "Confirma o horário? Responda sim ou não 🙂\n\n"
             + $"Crie sua conta pra acompanhar suas aulas: {linkDeCadastro}";
    }
}
