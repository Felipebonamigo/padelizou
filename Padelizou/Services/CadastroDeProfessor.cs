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
    Local
}

public static class CadastroDeProfessor
{
    // A ordem segue a escada da tela de marcar aula, não a preferência de quem programou:
    // cidade primeiro porque é por ela que o aluno começa a busca.
    public static PendenciaDoProfessor Pendencia(bool temCidade, bool temLocal)
    {
        if (!temCidade) return PendenciaDoProfessor.Cidade;
        if (!temLocal) return PendenciaDoProfessor.Local;
        return PendenciaDoProfessor.Nenhuma;
    }

    public static string AcaoPara(PendenciaDoProfessor pendencia) => pendencia switch
    {
        PendenciaDoProfessor.Cidade => "MinhasCidades",
        PendenciaDoProfessor.Local => "MeusLocais",
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

        _ => null
    };
}
