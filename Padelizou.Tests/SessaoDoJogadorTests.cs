using Padelizou.Services;

namespace Padelizou.Tests;

// "Tem que ficar logando toda hora" (jogadores, 09/08/2026). O cookie de login nascia sem
// propriedades nenhuma, e sem IsPersistent ele é um cookie de SESSÃO — morre quando o
// navegador fecha, e no celular quando o sistema recicla a aba.
public class SessaoDoJogadorTests
{
    [Fact]
    public void O_login_e_persistente_e_nao_de_sessao()
        => Assert.True(SessaoDoJogador.Propriedades().IsPersistent);

    // ⚠️ ExpiresUtc em branco de propósito: com data fixa a renovação deslizante não adianta
    // o relógio, e quem usa o app todo dia seria deslogado no nonagésimo assim mesmo. Quem
    // manda é o ExpireTimeSpan + SlidingExpiration do Program.cs.
    [Fact]
    public void Sem_data_fixa_de_validade_pra_nao_furar_a_renovacao()
        => Assert.Null(SessaoDoJogador.Propriedades().ExpiresUtc);
}
