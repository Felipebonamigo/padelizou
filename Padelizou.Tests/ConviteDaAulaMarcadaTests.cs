using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O RECADO DE "AULA MARCADA", PRO PROFESSOR MANDAR NO WHATSAPP DELE.
//
// Pedido de um professor (28/08/2026, pelo Felipe): "queria que mandasse uma mensagem para os
// alunos para confirmar quando eu marcar a aula no Padelizou, até pra eles fazer cadastro".
//
// ⚠️ QUEM MANDA É O PROFESSOR, num toque no botão — não é envio automático pelo chip do
// Padelizou, e isso é decisão, não preguiça:
//   • o aluno responde "sim" pra quem ele conhece, não pra um número estranho;
//   • mensagem automática pra número que nunca consentiu é como um chip pré-pago é denunciado
//     e morre — e o chip é o ponto frágil da infra de WhatsApp deste projeto;
//   • o "sim" volta na conversa DELE, onde ele já combina tudo. Ninguém precisa escrever um
//     processador de resposta que não existe.
//
// O envio automático continua existindo e continua valendo pra quem TEM conta e consentiu
// (Services/AvisoPorWhatsApp) — este recado é justamente pro aluno que ainda não tem nada.
public class ConviteDaAulaMarcadaTests
{
    private static Aula Aula(string nome, DateTime quando, string local, string? endereco = null) =>
        new()
        {
            NomeAlunoAvulso = nome,
            DataHora = quando,
            LocalAula = new LocalAula { Nome = local, Endereco = endereco },
        };

    private static readonly DateTime Sabado = new(2026, 9, 5, 14, 0, 0);

    [Fact]
    public void O_recado_diz_quando_onde_com_quem_e_pede_confirmacao()
    {
        var texto = ConviteDaAulaMarcada.Texto(
            Aula("Marina", Sabado, "Batata Padel"), professor: "Jonatas", linkDeCadastro: "https://padelizou.com.br/Auth/Cadastro");

        Assert.Contains("Marina", texto);
        Assert.Contains("05/09", texto);
        Assert.Contains("14:00", texto);
        Assert.Contains("Batata Padel", texto);
        Assert.Contains("Jonatas", texto);
        // A pergunta é o ponto da mensagem: sem ela é aviso, e o professor queria confirmação.
        Assert.Contains("Confirma", texto);
    }

    // A segunda metade do pedido — "até pra eles fazer cadastro". O link fecha a mensagem, e
    // sem ele o aluno não tem caminho nenhum pra dentro do sistema.
    [Fact]
    public void O_recado_leva_o_convite_pra_criar_conta()
    {
        var texto = ConviteDaAulaMarcada.Texto(
            Aula("Marina", Sabado, "Batata Padel"), "Jonatas", "https://padelizou.com.br/Auth/Cadastro");

        Assert.Contains("https://padelizou.com.br/Auth/Cadastro", texto);
    }

    // O endereço entra quando existe: "Batata Padel" leva quem já conhece, e mais nada leva
    // quem nunca foi. Quando não existe, a linha não vira "Batata Padel, " com vírgula solta.
    [Fact]
    public void O_endereco_entra_quando_existe_e_nao_deixa_sobra_quando_nao()
    {
        var com = ConviteDaAulaMarcada.Texto(
            Aula("Marina", Sabado, "Batata Padel", "Av. Ipiranga, 100"), "Jonatas", "x");
        var sem = ConviteDaAulaMarcada.Texto(
            Aula("Marina", Sabado, "Batata Padel"), "Jonatas", "x");

        Assert.Contains("em Batata Padel, Av. Ipiranga, 100, com", com);
        // Sem endereço, o local sai limpo — nada de separador duplo nem espaço sobrando onde
        // o endereço teria entrado. (A vírgula depois do nome é da frase: "em X, com Y".)
        Assert.Contains("em Batata Padel, com", sem);
        Assert.DoesNotContain(", ,", sem);
        Assert.DoesNotContain("  ", sem);
    }

    // ── Quem ganha o botão ────────────────────────────────────────────────────────────────

    // ⚠️ ALUNO COM CONTA NÃO GANHA O BOTÃO, e isso não é esquecimento: ele já recebe o aviso
    // automático (push + e-mail + WhatsApp, conforme o que aceitou). Oferecer o botão pra ele
    // faria o professor mandar a MESMA mensagem duas vezes — e ensinaria o aluno a ignorar as
    // duas. O botão é exatamente pro aluno que hoje não recebe nada.
    [Fact]
    public void So_o_aluno_SEM_conta_e_com_telefone_ganha_o_botao()
    {
        Assert.True(ConviteDaAulaMarcada.CabeConvite(alunoId: null, celular: "51999998888"));

        Assert.False(ConviteDaAulaMarcada.CabeConvite(alunoId: 7, celular: "51999998888"));
        Assert.False(ConviteDaAulaMarcada.CabeConvite(alunoId: null, celular: null));
        Assert.False(ConviteDaAulaMarcada.CabeConvite(alunoId: null, celular: "   "));
        // Número curto demais pra existir: a régua é a MESMA do link que a pessoa clica
        // (WhatsAppLinkHelper.NumeroValido) — duas cópias divergiriam.
        Assert.False(ConviteDaAulaMarcada.CabeConvite(alunoId: null, celular: "51"));
    }

    // Turma: o professor manda pra UM aluno por toque, e o botão é do primeiro sem conta.
    // Mandar pra três de uma vez não existe no WhatsApp sem lista de transmissão, e fingir
    // que existe seria pior que dizer a verdade na tela.
    [Fact]
    public void Numa_turma_o_convite_e_de_quem_nao_tem_conta()
    {
        var comConta = ConviteDaAulaMarcada.CabeConvite(alunoId: 7, celular: "51999998888");
        var semConta = ConviteDaAulaMarcada.CabeConvite(alunoId: null, celular: "51999997777");

        Assert.False(comConta);
        Assert.True(semConta);
    }
}
