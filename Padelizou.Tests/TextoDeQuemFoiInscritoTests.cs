using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O TEXTO DO AVISO DE "ALGUÉM TE INSCREVEU", puro, longe do banco e da rede — mesmo padrão do
// TextoDoApito e do TextoDoTeste: é texto que precisa estar certo, e texto certo se testa.
//
// 🗣️ Felipe, 16/09/2026, com a frase pronta: *"Você foi inscrito para um torneio por Maickel"*.
// A frase é dele e está aqui LETRA POR LETRA de propósito — é ela que a pessoa lê no celular
// quando descobre que entrou num torneio sem ter clicado em nada, e é o que sustenta o direito
// de recusar. Aviso que não chega, ou que chega sem dizer QUEM inscreveu, transforma a recusa
// num botão que ninguém acha (a mesma lição escrita no DESAFIOS.md §2: "o que sustenta essa
// regra é o AVISO, não o botão").
public class TextoDeQuemFoiInscritoTests
{
    [Fact]
    public void O_titulo_e_a_frase_que_o_Felipe_pediu()
    {
        Assert.Equal("Você foi inscrito para um torneio por Maickel",
            TextoDeQuemFoiInscrito.Titulo("Maickel"));
    }

    [Fact]
    public void Sem_o_nome_de_quem_inscreveu_o_titulo_nao_fica_pela_metade()
    {
        // O nome vem do cadastro e pode faltar (pré-cadastro sem nome tratado, conta excluída).
        // "Você foi inscrito para um torneio por " com o fim cortado é pior do que não nomear.
        var titulo = TextoDeQuemFoiInscrito.Titulo("");

        Assert.Equal("Você foi inscrito para um torneio", titulo);
        Assert.DoesNotContain(" por", titulo);
    }

    [Fact]
    public void O_corpo_diz_o_torneio_a_categoria_e_que_da_pra_recusar()
    {
        var corpo = TextoDeQuemFoiInscrito.Corpo("Copa de Verão", "5ª Masculina", emListaDeEspera: false);

        Assert.Contains("Copa de Verão", corpo);
        Assert.Contains("5ª Masculina", corpo);
        // Sem esta palavra o aviso é só um comunicado: a pessoa não fica sabendo que tem saída.
        Assert.Contains("recusar", corpo);
    }

    [Fact]
    public void Na_lista_de_espera_o_corpo_nao_promete_vaga()
    {
        // Dizer "você está no torneio" pra quem entrou na fila é a mentira que faz alguém
        // viajar pro clube — o mesmo cuidado que o aviso de inscrição já toma hoje.
        var corpo = TextoDeQuemFoiInscrito.Corpo("Copa de Verão", "5ª Masculina", emListaDeEspera: true);

        Assert.Contains("lista de espera", corpo);
        Assert.Contains("recusar", corpo);
    }

    [Fact]
    public void Uma_linha_so_nos_dois_casos()
    {
        // A caixa de entrada guarda o corpo como texto e a notificação do celular corta o que
        // passa de duas linhas (mesma razão escrita no TextoDoApito).
        Assert.DoesNotContain("\n", TextoDeQuemFoiInscrito.Corpo("Copa", "5ª", emListaDeEspera: false));
        Assert.DoesNotContain("\n", TextoDeQuemFoiInscrito.Corpo("Copa", "5ª", emListaDeEspera: true));
    }

    // ── O AVISO DE QUEM LEVOU A RECUSA ───────────────────────────────────────────────────

    [Fact]
    public void Quem_fica_sozinho_le_que_a_vaga_continua_dele()
    {
        // É o desfecho que o Felipe pediu: "ao recusar o primeiro fica sozinho no torneio e o
        // avisa". Quem fica não perdeu nada — e o texto precisa dizer isso, senão ele lê
        // "recusou" e acha que a inscrição dos dois caiu.
        var (titulo, corpo) = TextoDeQuemFoiInscrito.ParaQuemFicou("Ana Prass", "Copa de Verão",
            ficouSozinho: true);

        Assert.Contains("Ana Prass", titulo);
        Assert.Contains("recusou", titulo);
        Assert.Contains("Copa de Verão", corpo);
        Assert.Contains("continua sua", corpo);
        Assert.Contains("parceiro", corpo);
    }

    [Fact]
    public void Quando_a_inscricao_acaba_o_texto_nao_manda_procurar_parceiro()
    {
        // Quem recusou era o último da inscrição: não sobrou vaga pra ninguém defender, e
        // mandar "escolha outro parceiro" seria mandar a pessoa mexer no que não existe mais.
        var (_, corpo) = TextoDeQuemFoiInscrito.ParaQuemFicou("Ana Prass", "Copa de Verão",
            ficouSozinho: false);

        Assert.DoesNotContain("continua sua", corpo);
        Assert.Contains("cancelada", corpo);
    }

    [Fact]
    public void Quem_inscreveu_de_fora_tambem_e_avisado()
    {
        // O organizador que inscreveu a dupla inteira não está nela: sem este aviso ele
        // descobriria a recusa na hora de montar a chave.
        var (titulo, corpo) = TextoDeQuemFoiInscrito.ParaQuemInscreveu("Ana Prass", "Copa de Verão");

        Assert.Contains("Ana Prass", titulo);
        Assert.Contains("recusou", titulo);
        Assert.Contains("Copa de Verão", corpo);
        Assert.Contains("inscreveu", corpo);
    }
}
