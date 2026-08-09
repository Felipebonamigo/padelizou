namespace Padelizou.ViewModels;

// Quem segue e quem é seguido. Seguir já existia desde sempre (é o que faz alguém receber
// aviso dos torneios e dos jogos de quem acompanha), mas não havia NENHUMA tela pra ver a
// lista: dava pra seguir e pra deixar de seguir, e nunca pra saber quem estava lá.
public class RedeDoJogadorVM
{
    public int JogadorId { get; set; }
    public string NomeDoJogador { get; set; } = "";

    // O dono está olhando a própria rede. Muda o texto (que fala "você") e libera o botão de
    // seguir de volta.
    public bool SouEu { get; set; }

    public List<PessoaNaRedeVM> Seguidores { get; set; } = new();
    public List<PessoaNaRedeVM> Seguindo { get; set; } = new();

    // Qual das duas listas abre. Quem chega pelo card "seguidores" quer ver seguidores.
    public bool AbrirEmSeguindo { get; set; }
}

public class PessoaNaRedeVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Apelido { get; set; }
    public string? Foto { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public DateTime Desde { get; set; }

    // Se EU (quem está olhando) já sigo esta pessoa. É o que separa "Seguir de volta" de
    // "vocês dois se seguem" — e evita oferecer um botão que não faria nada.
    public bool EuSigo { get; set; }
}

// Uma linha da lista, com o contexto que decide o botão da direita.
//
// Vai como MODELO em vez de ViewData porque as duas listas usam o mesmo partial e a única
// diferença entre elas é essa: ViewData resolveria igual, mas com string solta e sem o
// compilador conferindo nada.
public record LinhaDaRede(
    PessoaNaRedeVM Pessoa,
    // O dono da rede é quem está olhando. Na rede dos outros a lista é só pra ver: oferecer
    // "seguir de volta" ali seria oferecer uma ação sobre uma relação que não é minha.
    bool SouDonoDaRede,
    int DonoDaRede,
    bool NaListaDeSeguidores);
