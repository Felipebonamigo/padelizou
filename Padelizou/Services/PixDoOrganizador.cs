using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Services;

// O "por fora": o torneio não cobra pelo site, o organizador põe a chave Pix dele na tela e o
// dinheiro passa longe do Padelizou — a gente não recebe e não confere nada.
//
// Aqui mora quem responde DUAS perguntas que a tela sozinha respondia pela metade: quando o
// bloco aparece, e PRA QUEM o jogador manda o comprovante depois de pagar.
public static class PixDoOrganizador
{
    // A chave só existe no "por fora". Nas formas pelo site o jogador paga no checkout, e uma
    // chave na tela seria um segundo caminho pro mesmo dinheiro.
    //
    // ⚠️ Esta condição também guarda a consulta do contato no controller. Ela mora aqui pra
    // não existir uma segunda cópia: a view mostrando o bloco sem o controller ter buscado o
    // contato daria exatamente o que esta mudança veio consertar — o pedido de comprovante
    // sem ninguém pra quem mandar.
    public static bool Aparece(Torneio torneio) =>
        !torneio.CobraPeloSite
        && !string.IsNullOrWhiteSpace(torneio.ChavePixOrganizador)
        && torneio.PrecoInscricao > 0;

    // ── QUANTO MANDAR ────────────────────────────────────────────────────────────────────
    // O card dava a chave e pedia o comprovante, mas não dizia o número que a pessoa tem que
    // digitar no app do banco. Ela ia buscar no cabeçalho da página — e o do cabeçalho é POR
    // PESSOA. Numa inscrição de dupla, quem lê "R$ 150,00" e manda 150 pagou metade, e quem
    // descobre isso é o organizador, conferindo comprovante na mão.
    //
    // ⚠️ NENHUMA CONTA NOVA DE DINHEIRO NASCE AQUI, de propósito: `ValorPorPessoa` devolve o
    // mesmo `PrecoInscricao` que a tela já mostra neste cenário (no "por fora" o
    // `ViewBag.PrecoTotal` é nulo e o cabeçalho cai no campo), e o total só multiplica pelo
    // `PessoasPorInscricao` que o próprio Torneio expõe. Dois lugares calculando o preço é
    // como a página passaria a anunciar dois valores diferentes pro mesmo torneio.
    public static decimal ValorPorPessoa(Torneio torneio) => torneio.PrecoInscricao;

    public static decimal ValorDaInscricao(Torneio torneio) =>
        torneio.PrecoInscricao * torneio.PessoasPorInscricao;

    // No Americano os dois números são iguais — a inscrição é de uma pessoa só. Imprimir
    // "R$ 150,00 por pessoa · R$ 150,00 a inscrição" só faz procurar a diferença que não existe.
    public static bool ValePorPessoaEPorInscricao(Torneio torneio) =>
        torneio.PessoasPorInscricao > 1;

    // ⚠️ O número do card é o preço BASE. Impedimento marcado SOMA, e a segunda categoria do
    // mesmo jogador pode custar MENOS (ver Services/PrecoDaInscricao) — e o card não sabe
    // quantas categorias esta pessoa vai pegar nem que impedimentos vai marcar. Prometer um
    // total exato nesses torneios seria mandar pagar o valor errado no Pix, que é dinheiro
    // que o Padelizou não vê e não conserta. Quando pode variar, o card avisa em vez de mentir.
    public static bool OTotalPodeVariar(Torneio torneio) =>
        torneio.TaxaPorImpedimento > 0 || torneio.PermiteMultiplasCategorias;

    // Quem recebe o comprovante é quem tem o caixa: o CRIADOR do torneio (ver
    // AcessoAoDinheiroDoTorneio). Ajudante organiza, mas não é pra quem o dinheiro vai.
    //
    // ⚠️ UM TORNEIO PODE TER MAIS DE UM "Criador" — nada no cadastro impede, e o painel admin
    // já registra isso. Sem uma ordem TOTAL, qual deles aparece mudaria de um carregamento
    // pro outro, e o jogador mandaria o comprovante pra pessoas diferentes a cada vez.
    // Ordena por Id: é estável e é o mais antigo, isto é, quem criou de fato.
    public static async Task<Jogador?> QuemRecebeOComprovanteAsync(DbPadelContext db, int torneioId) =>
        await db.TorneioOrganizadores
            .Where(o => o.TorneioId == torneioId && o.NivelAcesso == "Criador")
            .OrderBy(o => o.JogadorId)
            .Select(o => o.Jogador)
            .FirstOrDefaultAsync();

    // O texto que abre no WhatsApp já escrito. O comprovante em si é uma IMAGEM, que o
    // jogador anexa na mão — o wa.me não carrega arquivo. Por isso a frase avisa que ele vem
    // em seguida: sem isso o organizador recebe um "oi" solto e não sabe o que esperar.
    public static string Mensagem(string nomeDoTorneio, string? nomeDoJogador)
    {
        var quem = string.IsNullOrWhiteSpace(nomeDoJogador) ? "" : $" Sou {nomeDoJogador.Trim()}.";
        return $"Olá! Acabei de pagar a inscrição do {nomeDoTorneio} pelo Pix.{quem} "
             + "Estou mandando o comprovante aqui.";
    }

    // ── JÁ PAGUEI? O CARD RECOLHE ────────────────────────────────────────────────────────
    // 🗣️ Emerson Pisoni, 10/09/2026: *"Se o cara já pagou, daria pra tirar info do pagamento,
    // ocupa muito espaço"*. No celular, o bloco inteiro — valor, chave, aviso e botão do
    // WhatsApp — empurrava as abas do torneio (Inscritos, Jogos, Chaves e Grupos) pra fora
    // da tela de quem já tinha resolvido a parte dele.
    //
    // ⚠️ RECOLHE, NÃO SOME (a view usa <details>): a última palavra sobre quem pagou é do
    // organizador virando o `Pago` na mão — muita inscrição é paga em dinheiro na quadra —, e
    // uma marcação errada dele não pode deixar o jogador sem caminho pra pagar.
    //
    // ⚠️ "JÁ PAGUEI" É TODAS AS MINHAS INSCRIÇÕES DESTE TORNEIO. Com duas categorias, uma paga
    // e outra não, o card continua aberto: é justamente quem ainda deve. E quem não tem
    // inscrição nenhuma também vê aberto — essa pessoa é a que ainda vai pagar.
    //
    // ⚠️ RECEBE O TORNEIO **JÁ CARREGADO COM `Categorias → Duplas`** (é o que a consulta que
    // abre o Details faz, no topo da ação) e lê as duplas de lá, sem ir ao banco. É a mesma
    // decisão, e o mesmo motivo, do `MinhasInscricoesNoTorneio` no mesmo método: esta é a
    // página mais pesada do site, e uma consulta a mais aqui é uma consulta em toda abertura
    // dela. Com as categorias não carregadas o resultado é "não pagou" — o lado seguro, que
    // deixa o card aberto.
    public static async Task<bool> JaPagouTudoAsync(DbPadelContext db, Torneio torneio, int jogadorId)
    {
        var minhas = new List<bool>();

        // ⚠️ NO AMERICANO INDIVIDUAL, `Dupla` NÃO É INSCRIÇÃO — e isso não é detalhe: cada
        // rodada sorteada grava um par por confronto (TorneiosController.Americano.
        // GerarRodadasAmericano) e o desempate grava mais dois (CriarDesempateAmericano), todos
        // com `Pago` false, porque ninguém paga um par de rodada. Lá a inscrição é a
        // `InscricaoAmericana`; contar as duplas deixaria o card aberto pra sempre pra quem já
        // pagou, no formato que mais gera essas linhas.
        //
        // O `AmericanoDuplas` fica FORA desta exceção de propósito: lá o par é FIXO, ele É a
        // inscrição, e o sorteio não cria dupla nenhuma.
        //
        // ⚠️ E O ESTRAGO DE EXCLUIR A FAMÍLIA INTEIRA É O OPOSTO DO QUE PARECE — vale escrever
        // porque a primeira versão deste comentário errou: sem o bloco, `minhas` fica VAZIA no
        // AmericanoDuplas, o `Count > 0` lá embaixo dá falso, e o método devolve `false` pra
        // TODO MUNDO, pago ou não. O card então **nunca recolhe** naquele formato — ninguém
        // perde o caminho de pagar (é o lado seguro), mas o pedido do Emerson morre calado. É
        // exatamente por isso que a contraprova usa dupla PAGA e cobra `True`: com `False` ela
        // passaria pelo motivo errado.
        if (torneio.Formato != FormatoDoTorneio.Americano)
        {
            minhas.AddRange(torneio.Categorias
                .SelectMany(c => c.Duplas)
                // Time fica de fora (a mesma exclusão do "Pagar agora" no controller): todo time
                // é uma `Dupla` com o organizador no `Jogador1Id`, e sem isto um time sem
                // pagamento manteria o card aberto pra sempre na tela dele.
                //
                // ⚠️ LISTA DE ESPERA CONTA, e é escolha: quem está na fila entra como qualquer
                // inscrição, então uma vaga de espera não paga mantém o card aberto pra quem já
                // pagou a outra. É o lado SEGURO (card aberto nunca tira o caminho de pagar), e
                // é a MESMA régua do "Pagar agora" — filtrar aqui e não lá faria as duas telas
                // discordarem sobre o que é uma inscrição minha.
                .Where(d => d.NomeTime == null && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
                .Select(d => d.Pago));
        }

        // As americanas NÃO são pré-carregadas pela tela, então estas vêm do banco — uma
        // consulta, e só em torneio "por fora" com alguém logado (ver o `if` do controller).
        var americanas = await db.InscricoesAmericanas
            .Where(i => i.Categoria.TorneioId == torneio.Id && i.JogadorId == jogadorId)
            .Select(i => i.Pago)
            .ToListAsync();

        minhas.AddRange(americanas);

        return minhas.Count > 0 && minhas.All(pago => pago);
    }
}
