namespace Padelizou.Services;

// O que falta pro professor existir de verdade pro aluno.
//
// A tela de marcar aula é uma escada: cidade → professor → local → tipo → horário. Cada degrau
// só aparece depois do anterior. Quem não declarou cidade não entra na lista do primeiro degrau,
// e quem não tem local trava o aluno no terceiro — nos dois casos, sem erro nenhum na tela.
//
// Isso não era hipótese: em 28/07/2026, 7 professores de 7 nos três ambientes estavam sem cidade,
// e portanto ninguém no site inteiro conseguia marcar uma aula. Avisar não bastou; agora o painel
// leva a pessoa pro que falta antes de deixar entrar.
public enum PendenciaDoProfessor
{
    Nenhuma,
    Cidade,
    Local,
    Horario
}

public static class CadastroDeProfessor
{
    // A ordem segue a escada da tela de marcar aula, não a preferência de quem programou:
    // cidade primeiro porque é por ela que o aluno começa a busca.
    //
    // O horário entrou depois, e por um motivo que só apareceu com dados reais: em produção havia
    // 1 professor com cidade, 0 locais e 0 horários. Cobrar só cidade e local deixaria o aluno
    // chegar até o passo 5 — escolher cidade, professor, local, tipo — e não achar horário nenhum.
    // Andar quatro degraus pra descobrir que não dá é pior que parar no primeiro.
    public static PendenciaDoProfessor Pendencia(bool temCidade, bool temLocal, bool temHorario)
    {
        if (!temCidade) return PendenciaDoProfessor.Cidade;
        if (!temLocal) return PendenciaDoProfessor.Local;
        if (!temHorario) return PendenciaDoProfessor.Horario;
        return PendenciaDoProfessor.Nenhuma;
    }

    public static string AcaoPara(PendenciaDoProfessor pendencia) => pendencia switch
    {
        PendenciaDoProfessor.Cidade => "MinhasCidades",
        PendenciaDoProfessor.Local => "MeusLocais",
        PendenciaDoProfessor.Horario => "MeusHorarios",
        _ => "Dashboard"
    };

    // A mensagem diz a CONSEQUÊNCIA, não a tarefa. "Cadastre sua cidade" soa burocrático e a
    // pessoa deixa pra depois; "nenhum aluno consegue te achar" ela resolve agora.
    public static string? MensagemPara(PendenciaDoProfessor pendencia) => pendencia switch
    {
        PendenciaDoProfessor.Cidade =>
            "Falta um passo pra você aparecer: o aluno busca aula pela cidade, "
            + "e sem ela você não entra na lista. Escolha abaixo onde você dá aula.",

        PendenciaDoProfessor.Local =>
            "Falta o local: depois de escolher você, o aluno precisa saber onde a aula acontece. "
            + "Sem nenhum cadastrado, ele não consegue concluir a marcação.",

        PendenciaDoProfessor.Horario =>
            "Último passo: diga os dias e horas em que você dá aula. É a lista final que o aluno "
            + "vê — sem ela, ele chega até o fim da marcação e não encontra nenhum horário livre.",

        _ => null
    };
}
