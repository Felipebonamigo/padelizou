using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 13/08/2026 — a SOLICITAÇÃO DE AULA voltou pro WhatsApp, revertendo a decisão de 09/08.
//
// O argumento de então: a solicitação fica esperando no painel e nada se perde se o professor
// vir uma hora depois. Na prática o horário fica travado pro aluno enquanto isso, e a tela de
// "solicitação enviada" empurrava o aluno pra um botão de "avisar por WhatsApp também" — ou
// seja, o recado ia pelo WhatsApp de qualquer jeito, só que pela mão do aluno.
//
// O que este arquivo protege é a ARMADILHA do caminho: o jeito antigo de evitar o e-mail
// duplicado era `AppSemEmail`, que calava o WhatsApp junto. As duas coisas vinham amarradas.
// Trocar por `AppEWhatsApp` ligaria o WhatsApp e faria voltar a duplicata de e-mail que a
// decisão de 09/08 tinha cortado — o e-mail bom, com Aceitar e Recusar, sai no controller.
public class AulaPedidaAvisaNoWhatsAppTests
{
    [Fact]
    public void O_alcance_da_solicitacao_de_aula_vai_no_WhatsApp_e_NAO_manda_email()
    {
        var alcance = AlcanceDoAviso.AppEWhatsAppSemEmail;

        Assert.True(alcance.VaiNoWhatsApp());
        Assert.False(alcance.VaiNoEmail());
    }

    [Theory]
    // O padrão: e-mail sim, WhatsApp não. É o que faz aviso novo nascer calado no canal caro.
    [InlineData(AlcanceDoAviso.SoApp, false, true)]
    // O bilhete social: nem e-mail nem WhatsApp.
    [InlineData(AlcanceDoAviso.AppSemEmail, false, false)]
    // O urgente de sempre: os dois canais.
    [InlineData(AlcanceDoAviso.AppEWhatsApp, true, true)]
    // O caso novo: WhatsApp sim, e-mail não.
    [InlineData(AlcanceDoAviso.AppEWhatsAppSemEmail, true, false)]
    public void Cada_alcance_responde_as_duas_perguntas_do_entregador(
        AlcanceDoAviso alcance, bool esperaWhatsApp, bool esperaEmail)
    {
        Assert.Equal(esperaWhatsApp, alcance.VaiNoWhatsApp());
        Assert.Equal(esperaEmail, alcance.VaiNoEmail());
    }

    // ⚠️ O valor novo entrou NO FIM de propósito: `AlcanceDoAviso` não vai pro banco, mas há
    // ~50 pontos que o usam, e um valor enfiado no meio trocaria o número dos anteriores em
    // qualquer lugar que um dia venha a persistir isso. Se alguém reordenar, quebra aqui.
    [Fact]
    public void O_alcance_novo_e_o_ultimo_e_os_antigos_mantem_o_numero()
    {
        Assert.Equal(0, (int)AlcanceDoAviso.SoApp);
        Assert.Equal(1, (int)AlcanceDoAviso.AppEWhatsApp);
        Assert.Equal(2, (int)AlcanceDoAviso.AppSemEmail);
        Assert.Equal(3, (int)AlcanceDoAviso.AppEWhatsAppSemEmail);
    }

    // A régua de quem de fato recebe não mudou: o alcance só diz que o aviso PODE ir no
    // WhatsApp. Professor que desmarcou o canal, ou sem número válido, continua fora — e é
    // por isso que a tela de "solicitação enviada" pergunta isto antes de prometer WhatsApp.
    [Theory]
    [InlineData(true, "51999998888", true)]
    [InlineData(false, "51999998888", false)]
    [InlineData(true, "51", false)]
    [InlineData(true, null, false)]
    public void Prometer_WhatsApp_na_tela_segue_a_mesma_regua_do_entregador(
        bool aceita, string? celular, bool esperado)
    {
        Assert.Equal(esperado, AvisoPorWhatsApp.PodeReceber(aceita, celular));
    }
}
