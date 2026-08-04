using Padelizou.Services;

namespace Padelizou.Tests;

// A seta de voltar da barra de cima. No navegador dava pra usar o botão do próprio
// navegador; no app instalado NÃO EXISTE botão nenhum — em PWA no iPhone, entrar no perfil de
// alguém e não ter saída deixa a pessoa presa de verdade.
public class NavegacaoDeVoltaTests
{
    [Theory]
    [InlineData("Jogadores", "Perfil")]        // o caso que começou tudo
    [InlineData("Torneios", "Details")]
    [InlineData("Professores", "Perfil")]
    [InlineData("Pagamentos", "Configurar")]
    [InlineData("Agenda", "Index")]
    [InlineData("Times", "Detalhes")]
    [InlineData("Grupos", "Detalhes")]
    [InlineData("Admin", "Metricas")]
    public void Tela_interna_mostra_a_seta(string controller, string action)
    {
        Assert.True(NavegacaoDeVolta.Mostrar(controller, action));
    }

    [Theory]
    [InlineData("Home", "Index")]
    [InlineData("Torneios", "Index")]
    [InlineData("Jogadores", "Ranking")]
    [InlineData("Jogadores", "Buscar")]
    [InlineData("Aulas", "Solicitar")]
    [InlineData("Aulas", "Dashboard")]
    [InlineData("Auth", "Perfil")]
    [InlineData("Admin", "Index")]
    public void Destino_que_esta_a_um_toque_na_barra_nao_mostra(string controller, string action)
    {
        // Ali a seta seria ruído — e, no app instalado, voltar podia jogar a pessoa pra fora.
        Assert.False(NavegacaoDeVolta.Mostrar(controller, action));
    }

    [Theory]
    [InlineData("Auth", "Login")]
    [InlineData("Auth", "Cadastro")]
    [InlineData("AcessoAntecipado", "Entrar")]
    public void Porta_de_entrada_nao_mostra_seta(string controller, string action)
    {
        // Voltar daqui é sair do site.
        Assert.False(NavegacaoDeVolta.Mostrar(controller, action));
    }

    [Fact]
    public void Rota_sem_controller_ou_action_nao_quebra()
    {
        Assert.False(NavegacaoDeVolta.Mostrar(null, "Index"));
        Assert.False(NavegacaoDeVolta.Mostrar("Home", null));
        Assert.False(NavegacaoDeVolta.Mostrar("", ""));
    }

    [Fact]
    public void Comparacao_ignora_maiuscula()
    {
        // A rota pode chegar com outra caixa dependendo de como o link foi escrito.
        Assert.False(NavegacaoDeVolta.Mostrar("home", "index"));
        Assert.False(NavegacaoDeVolta.Mostrar("HOME", "INDEX"));
    }

    [Theory]
    [InlineData("Torneios", "/Torneios")]
    [InlineData("Jogadores", "/Jogadores/Buscar")]
    [InlineData("Professores", "/Jogadores/Buscar")]
    [InlineData("Agenda", "/Auth/Perfil")]
    [InlineData("Admin", "/Admin")]
    public void Destino_de_reserva_e_o_pai_do_assunto(string controller, string esperado)
    {
        // Quem chegou por link do WhatsApp ou por notificação não tem histórico: o
        // history.back() sairia do Padelizou. Aí a seta leva pro "pai" — quem estava vendo um
        // torneio quer a lista de torneios, não a home.
        Assert.Equal(esperado, NavegacaoDeVolta.DestinoDeReserva(controller));
    }

    [Fact]
    public void Assunto_sem_pai_obvio_cai_na_home()
    {
        Assert.Equal("/", NavegacaoDeVolta.DestinoDeReserva("ControllerQueAindaNaoExiste"));
        Assert.Equal("/", NavegacaoDeVolta.DestinoDeReserva(null));
    }

    [Fact]
    public void Todo_destino_de_reserva_e_um_caminho_de_dentro_do_site()
    {
        // Um destino relativo garante que a seta NUNCA leva pra fora do Padelizou — que é o
        // desfecho que o app instalado não perdoa.
        foreach (var c in new[] { "Torneios", "Jogadores", "Aulas", "Agenda", "Pagamentos",
                                  "Grupos", "Times", "Admin", "Qualquer" })
        {
            var destino = NavegacaoDeVolta.DestinoDeReserva(c);
            Assert.StartsWith("/", destino);
            Assert.DoesNotContain("//", destino);
        }
    }
}
