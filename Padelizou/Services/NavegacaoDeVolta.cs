namespace Padelizou.Services;

// Quem decide se a seta de voltar aparece na barra de cima.
//
// Existe porque faltava saída em tela nenhuma: entrar no perfil de alguém e não ter como
// voltar pra lista é o caso que o Felipe apontou, mas vale pra dezenas de telas. No navegador
// dava pra usar o botão do próprio navegador; **no app instalado não existe botão nenhum** —
// em PWA no iPhone a pessoa fica presa de verdade.
//
// A seta NÃO aparece nos destinos que já estão a um toque na barra: ali ela seria ruído, e no
// app instalado poderia jogar a pessoa pra fora do Padelizou.
public static class NavegacaoDeVolta
{
    private static readonly HashSet<string> Topo = new(StringComparer.OrdinalIgnoreCase)
    {
        // Itens do menu principal
        "Home/Index",
        "Torneios/Index",
        "Aulas/Solicitar",
        "Aulas/Dashboard",
        "Avisos/Index",
        "MarcarJogo/Index",
        "Grupos/Index",
        "Jogadores/Ranking",
        "Jogadores/Buscar",

        // O perfil está sempre no avatar, no canto da barra.
        "Auth/Perfil",

        // Porta de entrada: voltar daqui é sair do site.
        "Auth/Login",
        "Auth/Cadastro",
        "AcessoAntecipado/Entrar",

        // Raiz do painel admin — lá a barra já tem "Voltar ao site".
        "Admin/Index",
    };

    public static bool Mostrar(string? controller, string? action)
    {
        if (string.IsNullOrWhiteSpace(controller) || string.IsNullOrWhiteSpace(action)) return false;
        return !Topo.Contains($"{controller}/{action}");
    }

    // Pra onde a seta leva quando NÃO há histórico dentro do site — quem chegou por link do
    // WhatsApp, por notificação ou abrindo o app instalado direto nesta tela. Sem isto o
    // `history.back()` levaria pra fora do Padelizou, ou pra lugar nenhum.
    //
    // O destino é o "pai" do assunto, não a home: quem estava vendo um torneio quer a lista de
    // torneios. Só cai na home quando não há pai óbvio.
    public static string DestinoDeReserva(string? controller) => controller switch
    {
        "Torneios" => "/Torneios",
        "Jogadores" => "/Jogadores/Buscar",
        "Professores" => "/Jogadores/Buscar",
        "Aulas" => "/Aulas/Solicitar",
        "JogoAula" => "/Aulas/Solicitar",
        "Agenda" => "/Auth/Perfil",
        "Pagamentos" => "/Auth/Perfil",
        "Avisos" => "/Avisos",
        "MarcarJogo" => "/MarcarJogo",
        "RaqueteLivre" => "/Avisos",
        "Grupos" => "/Grupos",
        "Times" => "/Jogadores/Buscar",
        "Parceiros" => "/Auth/Perfil",
        "Admin" => "/Admin",
        _ => "/",
    };
}
