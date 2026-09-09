using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// Financeiro do torneio numa tela só, quebrado por categoria — hoje o organizador
// precisa cruzar Pagamentos/Meus com a lista de inscritos pra saber onde está o dinheiro.
public class FinanceiroTorneioVM
{
    public Torneio Torneio { get; set; } = null!;

    public decimal Arrecadado { get; set; }
    public decimal Pendente { get; set; }
    public decimal Estornado { get; set; }
    public decimal TaxaPlataforma { get; set; }

    public int Inscritos { get; set; }
    public int Pagantes { get; set; }

    public List<FinanceiroCategoriaVM> PorCategoria { get; set; } = new();
    public List<PagamentoPendenteVM> Pendentes { get; set; } = new();

    // A caderneta do "por fora": quem já acertou com o organizador e quem ainda deve.
    //
    // A lista `Pendentes` acima é feita das cobranças que o SITE gerou — num torneio "por
    // fora" ela é sempre vazia, porque o dinheiro não passa por aqui. O organizador ficava
    // com a conta de cabeça, marcando dupla por dupla no meio da lista de gestão.
    public List<CobrancaPorForaVM> CobrancaPorFora { get; set; } = new();

    public bool CobraPorFora => !Torneio.CobraPeloSite && Torneio.PrecoInscricao > 0;

    public decimal RecebidoPorFora => CobrancaPorFora.Where(c => c.Pago).Sum(c => c.Valor);
    public decimal AReceberPorFora => CobrancaPorFora.Where(c => !c.Pago).Sum(c => c.Valor);

    // A taxa de 5% do "por fora". Zero quando já foi paga ou negociada — aí não há o que
    // descontar do que o organizador recebeu.
    public decimal TaxaExterno { get; set; }

    // ── Os quatro números do topo ─────────────────────────────────────────────────────────
    //
    // Liam SÓ de Pagamento, que é o dinheiro que passou pelo gateway. Num torneio "por fora"
    // não passa nenhum, então os quatro ficavam em R$ 0,00 pra sempre — enquanto a caderneta
    // logo abaixo, na mesma tela, mostrava R$ 4.080 recebidos. Duas verdades sobre o mesmo
    // dinheiro, e a que estava em letra garrafal era a cega.
    //
    // Agora cada número sabe de onde ler: gateway quando o site cobra, caderneta quando não.
    public decimal ArrecadadoNaTela => CobraPorFora ? RecebidoPorFora : Arrecadado;
    public decimal AguardandoNaTela => CobraPorFora ? AReceberPorFora : Pendente;
    public decimal TaxaNaTela => CobraPorFora ? TaxaExterno : TaxaPlataforma;

    // Nunca negativo. "Líquido pra você" quer dizer "do que entrou, quanto é seu" — e disso
    // não dá pra ter menos que nada. No "por fora" a taxa é devida sobre TODOS os inscritos,
    // não sobre quem já pagou, então antes de alguém acertar a conta dava "−R$ 12,00", que
    // o organizador lê como se o torneio tivesse dado prejuízo. O valor devido continua
    // escrito na linha "taxa" logo abaixo — nada some, só para de assustar.
    public decimal LiquidoNaTela => Math.Max(0m, ArrecadadoNaTela - TaxaNaTela);

    // Estorno é coisa de cobrança do site: no "por fora" quem devolve é o organizador, por
    // fora também, e o sistema não tem como saber. Card zerado ali seria só ruído.
    public bool MostraEstornado => !CobraPorFora;

    // Quantos já acertaram — a versão de "pagantes" que funciona nas duas formas.
    public int PagantesNaTela => CobraPorFora ? CobrancaPorFora.Count(c => c.Pago) : Pagantes;

    // Quanto sobra pro organizador depois da comissão da plataforma.
    public decimal Liquido => Arrecadado - TaxaPlataforma;

    public bool TemMovimento => ArrecadadoNaTela > 0 || AguardandoNaTela > 0;

    // Torneio gratuito: a tela explica em vez de mostrar zeros.
    public bool EhGratuito => Torneio.PrecoInscricao <= 0;

    // Quem ajuda a organizar abre esta mesma tela pra marcar quem já acertou, mas sem
    // nenhum valor — nem total, nem por categoria, nem o da linha. Ver Services/
    // AcessoAoDinheiroDoTorneio. Nasce FALSO: a tela só mostra dinheiro quando alguém
    // afirmou que pode, nunca por esquecimento de preencher.
    public bool PodeVerDinheiro { get; set; }
}

public class FinanceiroCategoriaVM
{
    // O id, e não só o nome: nome de categoria se edita e já houve torneio com dois nomes
    // iguais — casar a linha da tabela com a categoria pelo texto era pedir pra somar
    // dinheiro na fileira errada.
    public int CategoriaId { get; set; }
    public string Categoria { get; set; } = "";
    public int Inscritos { get; set; }
    public int ListaDeEspera { get; set; }

    // Vale pras duas formas: no online é o que o gateway confirmou, no "por fora" é o que o
    // organizador marcou na caderneta. Ficava sempre em zero no "por fora" — mesmo defeito
    // dos cartões do topo, uma camada abaixo, e que sobreviveu à primeira correção.
    public decimal Arrecadado { get; set; }

    // Só no "por fora": quanto daquela categoria ainda está pra entrar. No online essa conta
    // não existe por categoria (a inscrição só nasce quando o dinheiro entra).
    public decimal AReceber { get; set; }

    public decimal Pendente { get; set; }
    public decimal Estornado { get; set; }
}

public class PagamentoPendenteVM
{
    public string Jogador { get; set; } = "";
    public string? Celular { get; set; }
    public string Categoria { get; set; } = "";
    public decimal Valor { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? ExpiraEm { get; set; }
    public string? LinkCobranca { get; set; }
}

// Uma linha da caderneta do "por fora": uma inscrição e se ela já foi acertada.
public class CobrancaPorForaVM
{
    // Id da Dupla ou da InscricaoAmericana — o botão de marcar pago precisa saber qual dos
    // dois, porque são ações diferentes.
    public int Id { get; set; }
    public bool EhDupla { get; set; }

    public string Nomes { get; set; } = "";
    public string Categoria { get; set; } = "";

    // Celular de quem responde pela inscrição (o jogador 1), pro botão de cobrar no WhatsApp.
    public string? Celular { get; set; }
    public string PrimeiroNome { get; set; } = "";

    public decimal Valor { get; set; }
    public bool Pago { get; set; }
    public DateTime? PagoEm { get; set; }
    public bool EmListaDeEspera { get; set; }
}

// Relatório de fechamento: o que o organizador manda pro patrocinador.
public class RelatorioTorneioVM
{
    public Torneio Torneio { get; set; } = null!;

    public int TotalDuplas { get; set; }
    public int TotalJogadores { get; set; }
    public int TotalPartidas { get; set; }
    public int PartidasFinalizadas { get; set; }
    public int TotalCategorias { get; set; }

    public decimal Arrecadado { get; set; }
    public decimal TaxaPlataforma { get; set; }
    public decimal Liquido => Arrecadado - TaxaPlataforma;

    // O relatório serve pra duas coisas — mostrar o pódio e prestar contas. Quem ajuda a
    // organizar leva o pódio; o bloco de dinheiro é de quem criou (ver Services/
    // AcessoAoDinheiroDoTorneio). Nasce falso pelo mesmo motivo do FinanceiroTorneioVM.
    public bool PodeVerDinheiro { get; set; }

    public List<PodioCategoriaVM> Podios { get; set; } = new();
    public List<FinanceiroCategoriaVM> PorCategoria { get; set; } = new();

    // Alcance: quantas pessoas diferentes abriram a página do torneio não é medido,
    // então "público" aqui é o que dá pra provar — inscritos e seguidores alcançados.
    public int JogadoresAlcancados { get; set; }
    public DateTime GeradoEm { get; set; } = DateTime.Now;
}

public class PodioCategoriaVM
{
    public string Categoria { get; set; } = "";
    public string? Campea { get; set; }
    public string? Vice { get; set; }
    public List<string> Semifinalistas { get; set; } = new();
    public int Duplas { get; set; }
}

// "Cabe?" — a conta que o organizador precisa ver ANTES de sortear as chaves, quando ainda
// dá pra mudar quadras, duração ou horário. Depois do sorteio a grade já está marcada e
// remarcar significa avisar todo mundo de novo.
public class PrevisaoGradeVM
{
    public int Duplas { get; set; }
    public int Grupos { get; set; }
    public int JogosDeGrupo { get; set; }
    public int JogosDeMataMata { get; set; }
    public int TotalDeJogos { get; set; }

    public DateTime Inicio { get; set; }

    // Quando o último jogo TERMINA (o começo mais a duração) — é o que o organizador precisa
    // pra saber a que horas devolve a quadra.
    public DateTime FimPrevisto { get; set; }

    public int Dias { get; set; }
    public bool EstouraOPrazo { get; set; }

    // Torneio por ordem de liberação: a contagem de jogos continua valendo (é o tamanho do
    // dia), mas relógio nenhum vale — não há horário a prever nem prazo a estourar.
    public bool SemHorarioPrevisto { get; set; }
}

// A ABA DE PLANEJAMENTO DE QUADRAS (09/09/2026) — ver Services/PlanejamentoDeQuadras.
//
// Os campos vêm em DOIS grupos, e a diferença entre eles é o assunto da tela:
//   • o PLANO é a resposta (quantos jogos por dia, quanto falta, quantas quadras);
//   • os BOTÕES são a pergunta, ecoada de volta pra que o formulário continue mostrando o que
//     o organizador girou — inclusive quando ele girou pra um valor que o torneio não tem.
public class PlanejamentoDeQuadrasVM
{
    public Torneio Torneio { get; set; } = null!;
    public PlanejamentoDeQuadras.Plano Plano { get; set; } = null!;

    // O número REAL de jogos deste torneio: projeção das duplas inscritas antes do sorteio,
    // partidas de verdade depois dele. É o padrão do campo — e o valor pro qual o botão
    // "voltar ao real" leva.
    public int JogosDoTorneio { get; set; }
    public bool JogosJaSorteados { get; set; }

    // O total do plano veio da URL, e não do torneio: a tela precisa dizer isso em voz alta,
    // senão o organizador leva pra reunião um número que ele mesmo inventou achando que era o
    // do sistema.
    public bool Simulando { get; set; }

    // Assistente do sistema entra em LEITURA (mesma régua da aba de gestão): vê a conta,
    // não aperta o "Aplicar". Ver TorneiosController.PodeOlharAGestaoAsync.
    public bool SoLeitura { get; set; }

    // Algum botão diverge do que está gravado — só então "Aplicar" tem o que fazer.
    public bool MudouAlgo { get; set; }

    // ── os botões, como a tela os recebeu ──
    public DateTime DataInicio { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraSeguintes { get; set; }
    public TimeSpan HoraFim { get; set; }
    public int Duracao { get; set; }
    public int Jogos { get; set; }
    public DateTime? Ate { get; set; }
    public string? Limites { get; set; }

    // ── AS QUADRAS DE VERDADE (09/09/2026) — a tabela que o Felipe pediu ──
    //
    // Não são botão de simulação: são as linhas de `Quadra` deste torneio, e mexer nelas GRAVA.
    // A tela separa as duas coisas com todas as letras. `Quadras.Count` é também a quantidade
    // do torneio — `Torneio.QuantidadeQuadras` é reescrita a cada salvamento pra nunca mais
    // divergir da lista (o jogo sem quadra de 05/08/2026 nasceu dessa divergência).
    public List<Quadra> Quadras { get; set; } = new();

    // O catálogo pro seletor de local, e o nome do clube do torneio (a opção "aqui mesmo").
    public List<Clube> Clubes { get; set; } = new();
    public string NomeDoClubeDoTorneio { get; set; } = "Clube do torneio";
    public int MaximoDeQuadras { get; set; }
}
