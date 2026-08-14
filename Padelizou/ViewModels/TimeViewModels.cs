using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// Linha da vitrine de times.
public class TimeResumoVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Logo { get; set; }

    // Onde o time joga em casa. Lista, e não um nome só, desde que a sede deixou de ser uma
    // coluna no Time — ver Models/TimeSede.
    public List<string> Sedes { get; set; } = new();

    public int Membros { get; set; }
    public int Pontos { get; set; }
}

public class TimeDetalheVM
{
    public Time Time { get; set; } = null!;
    public List<MembroTimeVM> Membros { get; set; } = new();

    public int TotalPontos => Membros.Sum(m => m.Pontos);

    // Onde o time joga em casa — pode ser mais de um clube, e pode ser nenhum.
    public List<Clube> Sedes { get; set; } = new();

    // A estante: as taças que o TIME levantou em categoria de times. Vazia = a seção some.
    public List<TitulosDoTime.Titulo> Titulos { get; set; } = new();

    // Quem do elenco mais ganhou taça na carreira. Lista porque empate no topo mostra os dois
    // — e vazia quando ninguém tem título, que é o caso em que a seção inteira não aparece.
    public List<TitulosDoTime.Vencedor> MaioresVencedores { get; set; } = new();

    // O elenco repartido pela categoria que cada um aceita jogar. Ver Services/ElencoPorCategoria
    // sobre por que a mesma pessoa aparece em mais de um grupo.
    public List<ElencoPorCategoria.Grupo<MembroTimeVM>> Elenco { get; set; } = new();

    // Quem administra o time — pode ser mais de um, e pode não ser membro (um admin do
    // Padelizou consegue designar alguém antes de a pessoa escolher a camisa).
    public List<AdministradorTimeVM> Administradores { get; set; } = new();

    // O presidente: o administrador mais antigo. Null = o time ainda não tem nenhum (o estado
    // da maioria dos 44 importados do ranking). Ver Services/AdministracaoTime.Presidente.
    public AdministradorTimeVM? Presidente { get; set; }

    // As últimas idas e vindas deste time — a mesma janela da aba, recortada num time só.
    public List<TransferenciaDeTime> Transferencias { get; set; } = new();

    // Quem está vendo pode mexer na lista? (administrador do time, ou admin do Padelizou)
    public bool PossoGerenciar { get; set; }
    public bool SouAdminDoSistema { get; set; }

    // Candidatos ao cargo. Fica no ViewModel, e não num ViewBag, porque ViewBag só falha
    // em tela: o cast erra em runtime, no meio da página, pra quem tem permissão de ver.
    public List<Jogador> CandidatosAAdministrador { get; set; } = new();
}

// A aba "Transferências recentes": os movimentos e o estado dos filtros, pra tela conseguir
// redesenhar os campos com o que a pessoa escolheu.
public class TransferenciasVM
{
    public List<TransferenciaDeTime> Movimentos { get; set; } = new();

    // Os times do filtro, em ordem alfabética.
    public List<Time> Times { get; set; } = new();

    public int? TimeId { get; set; }
    public TransferenciasDeTime.Sentido Sentido { get; set; }
    public string? Busca { get; set; }

    public bool TemFiltro => TimeId != null
                          || Sentido != TransferenciasDeTime.Sentido.Todas
                          || !string.IsNullOrWhiteSpace(Busca);
}

// Linha da tela /Admin/Times — a de GESTÃO, não a vitrine. O que ela mostra é o que o admin
// precisa saber antes de apertar "Apagar": quantas pessoas perdem a camisa e quantas duplas
// de torneio perdem o escudo. Apagar um time é irreversível pela tela, e um número na frente
// do botão é a diferença entre uma decisão e um susto.
public class TimeNoAdminVM
{
    public Time Time { get; set; } = null!;

    // Só contas vivas: quem excluiu a conta pela LGPD continua com o TimeId gravado, mas não
    // é uma pessoa que "perde a camisa" — contá-la inflaria o aviso do apagar.
    public int Membros { get; set; }

    // Duplas de categoria de times que apontam pra este cadastro. Elas NÃO somem junto (o
    // vínculo é SetNull no banco): o que se perde é o escudo na tela do torneio.
    public int DuplasEmTorneios { get; set; }

    // Os nomes dos clubes que são sede deste time.
    public List<string> Sedes { get; set; } = new();

    public List<AdministradorTimeVM> Administradores { get; set; } = new();
}

public class AdministradorTimeVM
{
    public Jogador Jogador { get; set; } = null!;
    public DateTime ConcedidoEm { get; set; }
    public string? ConcedidoPor { get; set; }
}

public class MembroTimeVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public bool EhAdministrador { get; set; }

    // As categorias que esta pessoa aceita jogar (JogadorCategoria). Vazia = não marcou
    // nenhuma, e o cadastro lê isso como "aceita qualquer uma".
    public List<string> Categorias { get; set; } = new();

    // Quantas taças na carreira. Serve pro selo ao lado do nome no elenco — o destaque de
    // "quem mais ganhou" é outra coisa, e sai de TimeDetalheVM.MaioresVencedores.
    public int Trofeus { get; set; }
}
