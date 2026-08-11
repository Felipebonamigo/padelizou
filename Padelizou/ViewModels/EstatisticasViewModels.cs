using Padelizou.Models;

namespace Padelizou.ViewModels;

// Ranking por categoria (agrupado por Categoria.Nome)
public class RankingCategoriaVM
{
    public string Categoria { get; set; } = null!;
    public List<RankingLinhaVM> Linhas { get; set; } = new();
}

public class RankingLinhaVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public int Titulos { get; set; }
    public int Torneios { get; set; }
    public int Finais { get; set; }

    // Vitórias em partidas de torneio (todas as épocas — a aba de pontos não filtra período).
    public int Vitorias { get; set; }

    // Movimento de posição vs ~1 mês atrás: >0 subiu, <0 desceu, 0 sem mudança,
    // null = novo no ranking (não existia há 1 mês, quando já havia histórico).
    public int? Movimento { get; set; }
}

// Selo histórico de um jogador numa categoria (usado nas abas do torneio)
public class HistoricoCategoriaVM
{
    public string MelhorFase { get; set; } = "Grupos";
    public int Titulos { get; set; }

    // Estilo do selo, conforme o tier da categoria (ver EstatisticasService.TierDaCategoria).
    public string Tier { get; set; } = "Geral";
    public string TierNome { get; set; } = "Geral";
    public string IconeTier { get; set; } = "bi-trophy-fill";
    public string CorFundoTier { get; set; } = "#eef2f8";
    public string CorTextoTier { get; set; } = "#6c757d";
}

// Resumo de um adversário no histórico de confrontos
public class ConfrontoResumoVM
{
    public Jogador Oponente { get; set; } = null!;
    public int Jogos { get; set; }
    public int Vitorias { get; set; }
    public int Derrotas { get; set; }
}

// Head-to-head detalhado entre dois jogadores
public class HeadToHeadVM
{
    public Jogador Eu { get; set; } = null!;
    public Jogador Oponente { get; set; } = null!;
    public int Jogos { get; set; }
    public int Vitorias { get; set; }
    public int Derrotas { get; set; }
    public List<ConfrontoPartidaVM> Partidas { get; set; } = new();
}

public class ConfrontoPartidaVM
{
    public DateTime? Data { get; set; }
    public string Torneio { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Fase { get; set; } = "";
    public string MinhaDupla { get; set; } = "";
    public string DuplaOponente { get; set; } = "";
    public string Placar { get; set; } = "";
    public bool EuVenci { get; set; }
}

// Resumo estatístico do jogador (cards do perfil)
public class ResumoJogadorVM
{
    // Pontos de ranking somados de TODAS as categorias (mesma regra do hub: pontos por fase
    // alcançada em cada torneio). Substitui o campo morto Jogador.PontuacaoGlobal, que ficava 0.
    public int Pontos { get; set; }

    public int TotalTorneios { get; set; }
    public int Titulos { get; set; }
    public int Finais { get; set; }
    public int Semis { get; set; }
    public int Quartas { get; set; }
    public int CaiuNaChave { get; set; }
    public int Vitorias { get; set; }
    public int Derrotas { get; set; }
}

// Evolução do jogador no tempo — um ponto por mês, pro gráfico do perfil.
public class EvolucaoJogadorVM
{
    public List<MesEvolucaoVM> Meses { get; set; } = new();

    // Falso quando o jogador nunca pontuou no período — a view esconde o gráfico.
    public bool TemDados => Meses.Any(m => m.Pontos > 0);

    // Quanto ganhou no mês corrente (o "+X pts neste mês" ao lado do total).
    //
    // ⚠️ Antes havia um `TemMesFuturo` aqui e um `NoFuturo` no mês, e a janela do gráfico se
    // esticava até o torneio mais distante em que a pessoa estivesse inscrita. Os três caíram
    // em 10/08/2026 junto com a causa: o ranking pagava participação já na INSCRIÇÃO, então o
    // total do perfil incluía torneio que ainda não aconteceu e a linha precisava alcançá-lo.
    // Agora ponto só nasce com o torneio começado — o último mês é sempre o mês corrente.
    public int PontosNoUltimoMes => Meses.LastOrDefault()?.Pontos ?? 0;

    public int Total => Meses.Count == 0 ? 0 : Meses[^1].Acumulado;
}

public class MesEvolucaoVM
{
    public DateTime Mes { get; set; }        // primeiro dia do mês
    public int Pontos { get; set; }          // ganhos NESTE mês
    public int Acumulado { get; set; }       // total até o fim deste mês
    public int Torneios { get; set; }        // torneios do mês
    public int Titulos { get; set; }

    public string Rotulo => Mes.ToString("MMM/yy");
}

// Parceiro de dupla mais frequente ("joga sempre com")
public class ParceiroResumoVM
{
    public Jogador Parceiro { get; set; } = null!;
    public int Jogos { get; set; }
    public int Vitorias { get; set; }
}

// Destaques de relacionamento do jogador (parceria/rivalidade), para os cards do perfil.
// Cada campo pode ser null se não houver dado suficiente ainda.
public class DestaquesJogadorVM
{
    public ParceiroResumoVM? MaisJogouJunto { get; set; }   // parceiro com mais jogos juntos
    public ConfrontoResumoVM? MaisEnfrentou { get; set; }   // adversário com mais confrontos
    public ConfrontoResumoVM? MaisTeVenceu { get; set; }    // algoz: mais derrotas suas pra ele
    public ConfrontoResumoVM? VoceMaisVenceu { get; set; }  // freguês: mais vitórias suas sobre ele
}

// Badge/conquista calculada on-the-fly (não persistida no banco)
public class ConquistaVM
{
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Icone { get; set; } = "";
    public bool Conquistada { get; set; }

    // Como se destrava. Conquista bloqueada é uma META — "Mensalista" cinza sem explicação
    // não diz o que fazer; "jogue 4 jogos fixos" diz.
    public string Descricao { get; set; } = "";
}

// Elogio agregado por tipo, pra exibir no perfil ("3 Smash Bom")
public class ElogioResumoVM
{
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Icone { get; set; } = "";
    public int Quantidade { get; set; }
    public bool EuDei { get; set; }
}

// ---------------------------------------------------------------------------
// Página de Ranking consolidada ("hub"). TUDO aqui é calculado apenas a partir
// de resultados de torneio (Dupla.UltimaFase, Partida.VencedorId/sets/games e
// Categoria.Nome). Nada de jogo semanal, aula ou pontuação manual entra.
// ---------------------------------------------------------------------------
public class RankingHubVM
{
    public List<RankingCategoriaVM> PorCategoria { get; set; } = new();            // 1. ranking por categoria (pontos)
    public List<RankingCategoriaVM> TrofeusPorCategoria { get; set; } = new();     // 2. troféus por categoria (títulos)
    public List<JogadorContagemVM> VitoriasJogadores { get; set; } = new();        // 3. jogador com mais vitórias (geral)
    public List<CategoriaJogadoresVM> VitoriasJogadoresPorCategoria { get; set; } = new(); // 4. jogador com mais vitórias por categoria
    public List<InvictoJogadorVM> InvictosJogadores { get; set; } = new();         // 5. jogador com mais jogos invicto
    public List<DuplaContagemVM> VitoriasDuplas { get; set; } = new();             // 6. dupla com mais vitórias (geral)
    public List<CategoriaDuplasVM> VitoriasDuplasPorCategoria { get; set; } = new(); // 7. dupla com mais vitórias por categoria
    public List<DuplaContagemVM> InvictasDuplas { get; set; } = new();             // 8. dupla com mais tempo invicta
    public List<NivelJogadorVM> NiveisComprovados { get; set; } = new();           // 9. nível comprovado (base p/ previsão de categoria)
    public List<RankingTimeVM> Times { get; set; } = new();                        // 10. ranking de times (soma dos pontos dos jogadores)

    // Todas as categorias cadastradas no sistema (nomes distintos), para o dropdown da aba "Por categoria".
    public List<string> TodasCategorias { get; set; } = new();

    // Período aplicado às seções de vitórias/invencibilidade/troféus: "sempre" | "ano" | "mes".
    // Pontos e Times ignoram o período (são sempre o total histórico).
    public string Periodo { get; set; } = "sempre";

    // Ranking de UM torneio específico, exibido embutido nesta mesma página (não vira outra tela).
    public int? TorneioSelecionadoId { get; set; }
    public string? TorneioSelecionadoNome { get; set; }
    public List<RankingTorneioLinhaVM> RankingTorneio { get; set; } = new();

    // Filtro regional aplicado (Cidades vazio + Estado null = país todo) + opções para os selects.
    // Pode filtrar por um estado e VÁRIAS cidades ao mesmo tempo.
    public string? Estado { get; set; }
    public List<string> Cidades { get; set; } = new();
    public List<string> EstadosDisponiveis { get; set; } = new();
    public List<string> CidadesDisponiveis { get; set; } = new();
    public bool TemFiltro => Cidades.Count > 0 || !string.IsNullOrWhiteSpace(Estado);

    // Aba Padelímetro: jogadores ordenados pelo nível (a unidade exibida é PDZ). Regras
    // em RANKING.md; a lista respeita o mesmo filtro regional do resto do hub.
    public List<PadelimetroLinhaVM> Padelimetro { get; set; } = new();

    // Aba Ranking Americano (Trilha C do RANKING.md): separada do ranking oficial porque
    // rodízio e torneio de chave não se somam. Só entra Americano contratado, pago e com o
    // piso de 8 pessoas — a regra inteira mora em Services/RankingAmericanoService.
    // ⚠️ DUAS listas, e não uma com uma coluna "formato": no individual o parceiro troca a cada
    // rodada e o resultado é seu; no de duplas metade do mérito é do parceiro que você escolheu.
    // Numa lista só, os dois números pareceriam a mesma medida.
    public List<RankingAmericanoLinhaVM> AmericanoIndividual { get; set; } = new();
    public List<RankingAmericanoLinhaVM> AmericanoDuplas { get; set; } = new();

    // "Quantas posições o último torneio me fez ganhar ou perder" — o selo verde/vermelho das
    // tabelas. Nulo = nenhum torneio terminou nos últimos 7 dias, e aí a coluna some inteira
    // (ver Services/MovimentoNoRanking, inclusive a razão dos 7 dias).
    //
    // ⚠️ São DUAS janelas porque são dois rankings que não se somam: o Americano de sábado não
    // pode carimbar movimento no ranking oficial, nem o contrário.
    public Padelizou.Services.MovimentoNoRanking.Janela? JanelaDoMovimento { get; set; }
    public Padelizou.Services.MovimentoNoRanking.Janela? JanelaDoAmericano { get; set; }
}

// O selo "+2 / -1" de uma linha, pro partial `_SeloDeMovimento`.
//
// ⚠️ É um record e não um `int?` solto porque partial com modelo NULO herda o modelo do PAI
// em vez de receber nulo — e "novo no ranking" É o caso nulo. Com `int?` a estreia de um
// jogador renderizaria o hub inteiro dentro de uma célula da tabela.
public record SeloDeMovimentoVM(int? Posicoes);

// Uma linha da aba Ranking Americano.
public class RankingAmericanoLinhaVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public int Americanos { get; set; }   // quantos contaram (não quantos jogou)
    public int Vitorias { get; set; }     // 1º lugar
    public int Podios { get; set; }       // 1º, 2º ou 3º

    // O último que contou, pra linha dizer de onde veio o ponto mais recente.
    public DateTime? UltimoEm { get; set; }
    public string? UltimoNome { get; set; }

    // Posições ganhas/perdidas no último Americano que contou (>0 subiu, <0 desceu, 0 igual,
    // null = entrou agora). Ver Services/MovimentoNoRanking.
    public int? Movimento { get; set; }
}

// Uma linha da aba Padelímetro do ranking.
public class PadelimetroLinhaVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pdz { get; set; }                 // o nível 0–1000 (unidade PDZ)
    public int Jogos { get; set; }               // partidas contadas (10+ = calibrado)
    public bool EmCalibracao { get; set; }
    public string? FaixaRotulo { get; set; }     // "4ª", "Open"... nulo = sem escada conhecida
    public string? FaixaEscada { get; set; }     // "masculina" | "feminina"

    // Posições ganhas/perdidas no último torneio (>0 subiu, <0 desceu, 0 igual, null = entrou
    // agora no ranking). Ver Services/MovimentoNoRanking.
    public int? Movimento { get; set; }
}

// Uma linha do ranking de UM torneio (agregado por jogador dentro daquele torneio).
public class RankingTorneioLinhaVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }     // soma dos pontos das duplas do jogador nesse torneio (por fase alcançada)
    public int Jogos { get; set; }      // partidas jogadas nesse torneio
    public int Vitorias { get; set; }   // partidas vencidas nesse torneio
    public int Titulos { get; set; }    // categorias em que foi campeão nesse torneio
}

// Pontos que um time somou DENTRO de um torneio específico.
public class PontosTimeTorneioVM
{
    public int TimeId { get; set; }
    public string Time { get; set; } = "";
    public string? Logo { get; set; }
    public int Pontos { get; set; }
    public int Jogadores { get; set; }
}

// Ranking de times: soma dos pontos de torneio de todos os jogadores do time.
public class RankingTimeVM
{
    public int TimeId { get; set; }
    public string Time { get; set; } = "";
    public string? Logo { get; set; }
    public int Pontos { get; set; }
    public int Titulos { get; set; }
    public int Jogadores { get; set; }

    // Vitórias em partidas de torneio somadas de todos os jogadores do time.
    public int Vitorias { get; set; }

    // Movimento de posição vs ~1 mês atrás (>0 subiu, <0 desceu, 0 igual, null = novo).
    public int? Movimento { get; set; }
}

// Contagem de vitórias/jogos de um jogador (usado nos leaderboards de vitórias)
public class JogadorContagemVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Vitorias { get; set; }
    public int Jogos { get; set; }
}

// Contagem de vitórias/jogos + sequência invicta de uma dupla (inscrição por torneio)
public class DuplaContagemVM
{
    public Jogador Jogador1 { get; set; } = null!;
    // Anulável porque dá pra se inscrever sem parceiro e definir depois. Quem exibe isto
    // (Ranking) já passa pelo helper NomesDaDupla, que devolve "(sem parceiro)" — o tipo é que
    // ainda prometia alguém que podia não existir.
    public Jogador? Jogador2 { get; set; }
    public string Categoria { get; set; } = "";
    public string Torneio { get; set; } = "";
    public int Vitorias { get; set; }
    public int Jogos { get; set; }
    public int SequenciaInvicta { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
}

// Maior sequência de partidas de torneio consecutivas sem perder (jogador)
public class InvictoJogadorVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Sequencia { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
}

public class CategoriaJogadoresVM
{
    public string Categoria { get; set; } = "";
    public List<JogadorContagemVM> Jogadores { get; set; } = new();
}

public class CategoriaDuplasVM
{
    public string Categoria { get; set; } = "";
    public List<DuplaContagemVM> Duplas { get; set; } = new();
}

// Nível "comprovado" de um jogador: categoria mais FORTE em que ele chegou à
// final (ou foi campeão). Base da regra anti-sandbagging de inscrição.
public class NivelComprovadoVM
{
    public string Categoria { get; set; } = "";
    public int Ordem { get; set; }          // ver EstatisticasService.OrdemCategoria (maior = mais forte)
    public string? MelhorFase { get; set; } // "Final" ou "Campeao"
}

public class NivelJogadorVM
{
    public Jogador Jogador { get; set; } = null!;
    public string Categoria { get; set; } = "";
    public int Ordem { get; set; }
    public string? MelhorFase { get; set; }
}
