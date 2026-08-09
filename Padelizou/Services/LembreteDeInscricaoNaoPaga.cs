using Padelizou.Models;

namespace Padelizou.Services;

// "VOCÊ ESTÁ INSCRITO E AINDA NÃO PAGOU" — o lembrete que olha a INSCRIÇÃO, não a fatura
// (Felipe, 09/08/2026).
//
// ⚠️ JÁ EXISTIA UM LEMBRETE E ELE NÃO SERVIA PRA ISSO. O de `PagamentoExpiradoBackgroundService`
// é de **cobrança a vencer**: parte de uma linha em `Pagamento` com status "Pendente" e avisa 6h
// antes dela expirar. Quem se inscreveu e escolheu "pago depois" **não tem cobrança nenhuma** —
// a fatura só nasce quando a pessoa diz que vai pagar (ver QuandoPagarInscricao). Não havia o
// que vencer, então não havia o que avisar: as 8 pessoas devendo no NATA PADEL TOUR ficariam em
// silêncio até o dia do fechamento.
//
// A diferença em uma frase: aquele pergunta "alguma fatura está pra vencer?", este pergunta
// "alguém está devendo?".
//
// ⚠️ E ele avisa com ANTECEDÊNCIA, não em cima da hora. Aviso que chega 6h antes do prazo, num
// prazo de 43 dias, é aviso que chega tarde: quem ia pagar já não tem o dia pra se organizar, e
// quem esqueceu não tem como resolver.
public static class LembreteDeInscricaoNaoPaga
{
    // Os marcos, em DIAS QUE FALTAM pro prazo — do mais distante pro mais perto.
    //
    // Escolha do Felipe (09/08/2026): **7 dias** e **24 horas** antes do fechamento. Um pra
    // lembrar, com tempo de resolver com calma; outro pra apertar, quando é agora ou perde.
    // Um terceiro no meio viraria cobrança chata de quem já decidiu pagar, e cada aviso a mais
    // é um motivo a mais pra pessoa desligar as notificações.
    //
    // ⚠️ O "24 horas" é o marco **1**, e ele vale o DIA INTEIRO da véspera: o varredor passa de
    // hora em hora a partir das 9h, então na prática o aviso sai na manhã do dia anterior ao
    // prazo. Contar em horas exatas faria o aviso sair de madrugada pra quem se inscreveu de
    // madrugada — ver HoraCivilizada e o comentário de MarcoDevido sobre dias de calendário.
    public static readonly int[] Marcos = { 7, 1 };

    // Ninguém é acordado às 3h da manhã pra ser cobrado. O varredor pode rodar a qualquer hora;
    // o AVISO só sai em horário de gente.
    public const int PrimeiraHora = 9;
    public const int UltimaHora = 21;

    public static bool HoraCivilizada(DateTime agora) =>
        agora.Hour >= PrimeiraHora && agora.Hour < UltimaHora;

    // Só torneio que cobra PELO SITE e aceita pagar depois. Em "só confirmo depois de pago" não
    // existe inscrição devendo (ela nasce paga), e no recebimento por fora o site não conhece o
    // combinado — cobrar ali seria falar de um acerto que não é nosso.
    public static bool VigiaEsteTorneio(Torneio torneio) =>
        FormaDePagamentoDoTorneio.EhPeloSite(torneio.FormaPagamento)
        && !PrazoParaPagar.ExigePagarNaHora(torneio)
        && torneio.PrecoInscricao > 0
        && torneio.CanceladoEm == null
        && PrazoParaPagar.Ate(torneio) != null;

    // ⚠️ ONDE MORA A INSCRIÇÃO DESTE TORNEIO — e no Americano individual NÃO é na tabela Dupla.
    //
    // Lá `Dupla` guarda o PAR DE UMA RODADA, não uma inscrição: o parceiro é sorteado de novo a
    // cada rodada e cada uma cria linhas novas. Em produção, o "Americano das Gurias do Padel"
    // tem **10 jogadoras e 27 linhas de Dupla** — varrer essa tabela mandaria a cada uma delas
    // uma cobrança POR PARTIDA JOGADA. A inscrição individual mora em `InscricaoAmericana`.
    //
    // O discriminador é o mesmo do resto do sistema: tudo que não é "Americano" (o individual)
    // se inscreve em dupla — inclusive o Americano DE DUPLAS, onde a dupla é fixa e é mesmo a
    // inscrição. Ver [[project_padelizou_ranking_americano_formatos]].
    public static bool AInscricaoEhDupla(Torneio torneio) => torneio.Formato != "Americano";

    // Qual marco cabe AGORA nesta inscrição — nulo quando não há o que avisar.
    //
    // ⚠️ Quando mais de um marco está vencido de uma vez (a pessoa se inscreveu faltando 1 dia,
    // e os marcos de 7 e de 2 valem os dois), vale o MAIS URGENTE e os outros são dados por
    // cumpridos. Sem isso, ela levaria dois avisos em minutos — um por tick do varredor.
    public static int? MarcoDevido(DateTime prazo, DateTime agora, int? ultimoMarcoEnviado)
    {
        // Passou do prazo não é lembrete, é cobrança — e essa conversa é do organizador com a
        // pessoa, não de um robô às 9 da manhã.
        if (prazo <= agora) return null;

        // Em DIAS DE CALENDÁRIO: "falta 1 dia" tem que valer o dia inteiro, senão o aviso muda
        // de marco conforme a hora em que o varredor passou.
        var faltam = (prazo.Date - agora.Date).Days;

        var devidos = Marcos.Where(m => faltam <= m && (ultimoMarcoEnviado == null || m < ultimoMarcoEnviado));

        return devidos.Any() ? devidos.Min() : null;
    }

    // O texto conta os dias DE VERDADE, nunca o número do marco: quem se inscreve faltando 1 dia
    // entra pelo marco de 7, e ouvir "faltam 7 dias" seria uma mentira com hora marcada.
    public static string Frase(string nomeDoTorneio, decimal valor, DateTime prazo, DateTime agora)
    {
        var faltam = (prazo.Date - agora.Date).Days;

        var quando = faltam <= 0 ? "Hoje é o último dia"
            : faltam == 1 ? "Falta 1 dia"
            : $"Faltam {faltam} dias";

        return $"Sua inscrição no {nomeDoTorneio} ainda não está paga — {valor:C}. "
             + $"{quando} pra pagar (até {prazo:dd/MM}) e garantir a vaga.";
    }

    public const string Titulo = "Inscrição ainda não paga";
}
