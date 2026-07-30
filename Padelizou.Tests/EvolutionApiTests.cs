using Padelizou.Services;

namespace Padelizou.Tests;

// O canal de WhatsApp roda numa Evolution API no nosso próprio VPS, com um chip pré-pago.
// Aqui ficam as duas decisões que não podem errar: quando o canal está DESLIGADO (senão um
// teste manda mensagem pra gente de verdade) e como o número vira endereço do WhatsApp.
public class EvolutionApiTests
{
    [Fact]
    public void Sem_configuracao_o_canal_nasce_desligado()
    {
        // O padrão precisa ser desligado: no localhost e no dev não existe container nenhum,
        // e "ligado por engano" aqui significa mensagem no celular de jogador de verdade.
        Assert.False(new EvolutionSettings().Configurado);
    }

    [Theory]
    [InlineData("", "chave", "padelizou", false)]
    [InlineData("http://127.0.0.1:8081", "", "padelizou", false)]
    [InlineData("http://127.0.0.1:8081", "chave", "", false)]
    [InlineData("http://127.0.0.1:8081", "chave", "padelizou", true)]
    public void So_liga_com_endereco_chave_e_instancia(string url, string chave, string instancia, bool esperado)
    {
        var settings = new EvolutionSettings { BaseUrl = url, ApiKey = chave, Instancia = instancia };

        Assert.Equal(esperado, settings.Configurado);
    }

    [Theory]
    [InlineData("51999998888", "5551999998888")]      // como o cadastro guarda
    [InlineData("(51) 99999-8888", "5551999998888")]  // salvo com máscara
    [InlineData("5133334444", "555133334444")]        // fixo, 10 dígitos
    [InlineData("5551999998888", "5551999998888")]    // já veio com o país — não duplica
    [InlineData("+55 51 99999-8888", "5551999998888")]
    public void Numero_ganha_o_55_do_pais_sem_duplicar(string guardado, string esperado)
    {
        Assert.Equal(esperado, EvolutionApiService.NumeroInternacional(guardado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("51")]           // campo pela metade
    [InlineData("-")]
    [InlineData("123456789")]    // 9 dígitos: nem DDD nem número inteiro
    public void Numero_que_nao_existe_nao_vira_envio(string? guardado)
    {
        // Sem esse piso, um campo sujo viraria tentativa de envio — que só serve pra queimar
        // reputação do chip com número inválido.
        Assert.Null(EvolutionApiService.NumeroInternacional(guardado));
    }
}
