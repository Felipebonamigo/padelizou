using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O CARTAZ DO TORNEIO — o que ele diz, e quando ele existe.
//
// O organizador vai divulgar de qualquer jeito. Hoje ele monta a arte no Canva e posta uma
// imagem que não tem link nenhum: quem vê o cartaz no story do amigo fica sabendo do torneio e
// não tem como chegar até ele — pergunta no WhatsApp, ou desiste. Cada divulgação é uma porta
// de entrada que a gente joga fora, e são muitas por torneio, o ano inteiro.
//
// O cartaz fecha esse buraco pelo mesmo caminho do card de campeão: a arte nasce pronta com o
// que o jogador precisa pra decidir (quando, onde, quais categorias, quanto) e com o QR que
// leva direto à inscrição. Quem divulga continua sendo o organizador — a diferença é que agora
// a divulgação dele traz gente pra dentro em vez de parar no papel.
//
// ⚠️ ESTE ARQUIVO É SÓ REGRA E CONSULTA. O desenho está em `CartazDoTorneio`, do mesmo jeito
// que `CampeoesDoTorneio` (quem ganhou) é separado de `CartaoDeCampeao` (a arte).
public static class DivulgacaoDoTorneio
{
    // ⚠️ A string "Finalizado" vive espalhada em 16 lugares do projeto (EncerramentoDaPartida,
    // RoboDoChaveamento, MvpDoTorneio, RankingAmericanoService…). Esta é mais uma cópia, e é
    // dívida conhecida — o certo seria um `StatusDoTorneio` central, que é limpeza maior do que
    // cabe aqui. Enquanto isso, quem mexer no nome do status tem que grepar.
    public const string StatusFinalizado = "Finalizado";

    // Este torneio pode virar cartaz?
    //
    // ⚠️ Duas recusas, e as duas por honestidade da peça — não por limitação técnica:
    //
    // CANCELADO não divulga, mesma régua do `CampeoesDoTorneio.PodeAnunciar`. Cartaz é a coisa
    // que mais sobrevive ao próprio contexto: ele é salvo na galeria, reencaminhado e reposto
    // dias depois. Um cartaz de torneio cancelado circulando com a nossa marca embaixo leva
    // gente ao clube num dia em que não há torneio.
    //
    // FINALIZADO também não: o cartaz chama pra um evento, e evento que já aconteceu não tem
    // pra onde chamar. Quem quer contar o que rolou tem o card de campeão, que é a peça do
    // depois. Torneio EM ANDAMENTO continua tendo cartaz — ele só troca de conversa (ver
    // `Selo`), porque assistir também é convite.
    public static bool PodeDivulgar(string? status) =>
        !CancelamentoDoTorneio.EstaCancelado(status) && status != StatusFinalizado;

    // A frase de destaque, que muda com a porta da inscrição. É a linha que decide o que a
    // pessoa faz depois de ver o cartaz — e por isso ela nunca pode prometer o que o sistema
    // não vai entregar: um "INSCRIÇÕES ABERTAS" num torneio de inscrição fechada leva alguém a
    // apontar a câmera pra encontrar um botão que não existe.
    public static string Selo(string? status) =>
        status == PortaDaInscricao.Aberta ? "INSCRIÇÕES ABERTAS" : "ACOMPANHE AO VIVO";

    // O verbo que fica ao lado do QR. Curto de propósito: ele divide a linha com o desenho do
    // código, e frase comprida ali encolheria a ponto de não ser lida — que é o mesmo que não
    // estar escrita. Quem explica o gesto é o "Aponte a câmera" fixo do cartaz.
    public static string Acao(string? status) =>
        status == PortaDaInscricao.Aberta ? "INSCREVA-SE" : "VEJA AS CHAVES";

    // Até quantas categorias cabem escritas antes de a lista virar um borrão. Acima disto o
    // cartaz diz o NÚMERO: "12 categorias" é informação; doze nomes espremidos em corpo 20 não
    // são lidos por ninguém, e ainda empurram o resto da arte pra fora.
    public const int MaximoDeCategoriasNoCartaz = 8;

    public static string LinhaDeCategorias(IReadOnlyList<string> nomes)
    {
        if (nomes.Count == 0) return "";
        if (nomes.Count > MaximoDeCategoriasNoCartaz) return $"{nomes.Count} categorias";

        // ⚠️ ESPAÇO INSEPARÁVEL DENTRO DO NOME DA CATEGORIA. A linha pode ocupar duas linhas no
        // cartaz, e quem quebra procura espaços — sem isto, "4ª Feminina" virava "4ª" no fim de
        // uma linha e "Feminina" sozinha no começo da outra, que é como o cartaz saiu na
        // primeira conferência. O ponto separador continua com espaço COMUM de cada lado, então
        // a quebra acontece exatamente onde faz sentido: entre uma categoria e a seguinte.
        //
        // 🕳️ O SEGUNDO ARGUMENTO DO `Replace` ABAIXO É UM NBSP (U+00A0), NÃO UM ESPAÇO. Os dois
        // são idênticos na tela do editor, e um "isso aqui está repetido" bem-intencionado
        // desfaz a correção sem deixar rastro — o cartaz volta a quebrar "4ª Feminina" no meio
        // e ninguém liga uma coisa à outra. Conferir com `od -c` antes de mexer nesta linha.
        var inteiros = nomes.Select(n => n.Replace(' ', ' '));
        return string.Join("   ·   ", inteiros);
    }

    // "R$ 120" ou "R$ 79,90" — o centavo só aparece quando existe.
    //
    // ⚠️ Cultura FIXA em pt-BR, e não a do processo. O `Program.cs` fixa pt-BR na thread, mas
    // quem desenha o cartaz pode ser um teste (que não passa pelo Program) ou um serviço de
    // fundo, e no Linux a cultura padrão é a invariante — que escreve "¤120.00". A moeda no
    // cartaz é dado que o jogador usa pra decidir; ela não pode depender de por onde o código
    // entrou.
    public static string PrecoNaTela(decimal valor)
    {
        var cultura = new System.Globalization.CultureInfo("pt-BR");
        return valor == decimal.Truncate(valor)
            ? valor.ToString("C0", cultura)
            : valor.ToString("C2", cultura);
    }

    // O torneio pronto pra virar cartaz. Null quando ele não pode ser divulgado.
    public static async Task<TorneioParaDivulgar?> DoTorneioAsync(DbPadelContext contexto, int torneioId)
    {
        // ⚠️ SEM `Include(t => t.Clube)`. `Torneio.ClubeId` é obrigatório, então o EF traduz a
        // navegação como INNER JOIN — e um torneio cujo clube sumiu do banco não voltaria linha
        // nenhuma, fazendo o método responder "este torneio não pode ser divulgado" em vez de
        // desenhar um cartaz sem o nome do clube. Foi exatamente assim que o card de campeão
        // quebrou, e o teste que pegou está lá até hoje.
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == torneioId);

        if (torneio == null || !PodeDivulgar(torneio.Status)) return null;

        // Clube e cidade numa consulta à parte, justamente pra eles NÃO poderem derrubar o
        // cartaz: clube ausente vira uma linha a menos na arte, nunca um torneio sem cartaz.
        var local = await contexto.Clubes
            .AsNoTracking()
            .Where(c => c.Id == torneio.ClubeId)
            .Select(c => new { c.Nome, Cidade = c.Cidade == null ? null : c.Cidade.Nome })
            .FirstOrDefaultAsync();

        var categorias = await contexto.Categorias
            .AsNoTracking()
            .Where(c => c.TorneioId == torneioId)
            .Select(c => c.Nome)
            .ToListAsync();

        // A MESMA ordem das outras telas do torneio (masculinas da mais forte pra mais fraca,
        // depois femininas, depois mista e casais) — a tupla de `Ordem` já desempata pelo nome.
        var nomesCurtos = categorias
            .OrderBy(CategoriaNaTela.Ordem)
            .Select(CategoriaNaTela.Curto)
            .ToList();

        return new TorneioParaDivulgar(
            TorneioId: torneio.Id,
            Nome: torneio.Nome,
            Capa: torneio.ImagemCapa,
            // A frase de data sai do serviço que as outras telas já usam. Formatar aqui seria a
            // terceira cópia da regra que decide como o intervalo se escreve — e a segunda já
            // custou uma tela que mostrava data diferente da listagem.
            Data: DataDoTorneioNaTela.Frase(torneio),
            TemData: DataDoTorneioNaTela.TemData(torneio),
            Clube: local?.Nome,
            Cidade: local?.Cidade,
            Categorias: LinhaDeCategorias(nomesCurtos),
            Preco: torneio.PrecoInscricao,
            Selo: Selo(torneio.Status),
            Acao: Acao(torneio.Status));
    }
}

// O que o cartaz estampa.
public sealed record TorneioParaDivulgar(
    int TorneioId,
    string Nome,
    string? Capa,
    string Data,
    bool TemData,
    string? Clube,
    string? Cidade,
    string Categorias,
    decimal Preco,
    string Selo,
    string Acao)
{
    // "Golden Point · Gravataí" — qualquer um dos dois pode faltar sem deixar separador solto.
    public string Local =>
        string.Join("  ·  ", new[] { Clube, Cidade }.Where(p => !string.IsNullOrWhiteSpace(p)));

    // ⚠️ O preço do padel se anuncia POR DUPLA na maioria dos cartazes, e aqui ele é POR PESSOA
    // (ver `Torneio.PrecoInscricao`). Estampar "R$ 120" sem dizer de quem é o R$ 120 faria a
    // dupla chegar ao checkout esperando pagar metade — e a discussão sobraria pro organizador,
    // com o nosso cartaz na mão dela como prova.
    public string PrecoComUnidade => $"{DivulgacaoDoTorneio.PrecoNaTela(Preco)} por atleta";

    // Preço zerado não vira "GRATUITO" — vira linha nenhuma. Torneio de graça existe, mas
    // campo não preenchido existe muito mais, e anunciar de graça o que vai ser cobrado é o
    // erro caro dos dois.
    public bool MostraPreco => Preco > 0;
}
