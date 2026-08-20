using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O "NOVO TORNEIO ABERTO" — o único aviso do sistema proporcional ao TAMANHO DA BASE.
//
// Ele morava inteiro dentro de AdminController.AprovarTorneio e saiu de lá em 18/08/2026,
// quando ganhou um SEGUNDO gatilho: publicar um torneio que estava oculto. Copiá-lo pro
// segundo lugar teria criado exatamente o tipo de cópia que o Padelizou já pagou caro pra
// aprender a não fazer — e aqui a cópia divergente não seria uma tela errada, seria um push
// pra base inteira saindo duas vezes, ou não saindo nunca.
//
// ⚠️ QUEM DECIDE É ESTE ARQUIVO, e não quem chama. Os dois gatilhos entregam o torneio e
// perguntam "dá pra avisar?"; as três recusas (não aprovado, oculto, já avisado) moram aqui.
// O texto de quem JÁ JOGOU a série, puro. É o anúncio de maior conversão que existe: a pessoa
// não está sendo apresentada a um torneio — está sendo chamada de volta pro dela.
public static class TextoDaVoltaDaSerie
{
    public const string Titulo = "Ele voltou! 🎾";

    public static string Corpo(string torneio) =>
        $"Abriu a nova edição do {torneio} — o torneio que você jogou. Garanta sua vaga.";
}

public static class AvisoDeTorneioNovo
{
    // O que aconteceu, pra tela poder dizer a verdade em vez de prometer um push que não saiu.
    public sealed record Resultado(bool Enviou, int Quantos);

    public static async Task<Resultado> EnviarSePuderAsync(
        DbPadelContext ctx, IPushNotificationService push, Torneio torneio, string? url)
    {
        // 1. NÃO APROVADO: quem ainda não passou pelo olhar do Padelizou não anuncia nada — é
        //    a trava que existe desde 07/08/2026 contra alguém lotar a base de torneio
        //    inventado.
        // 2. OCULTO: anunciar o que ninguém consegue abrir é mandar todo mundo pra um 404.
        //    ⚠️ E aqui NÃO se carimba: é justamente este torneio que vai querer o aviso mais
        //    tarde, quando o organizador publicar. Carimbar aqui mataria o aviso pra sempre.
        // 3. JÁ AVISADO: o carimbo é o que garante UMA vez só agora que há dois gatilhos.
        //    Sem ele, esconder e publicar de novo mandaria o mesmo anúncio à base a cada
        //    volta — a mesma lição do `AvisoDeMvpEnviadoEm` e do `PerguntaDeNaoPagosEm`.
        if (torneio.AprovadoEm == null || torneio.Oculto || torneio.AvisoDeTorneioNovoEm != null)
            return new Resultado(false, 0);

        // MIRA POR ESTADO (decisão do Felipe, 10/08/2026): anunciar em Porto Alegre um torneio
        // de São Paulo não serve pra ninguém dos dois lados.
        //
        // ⚠️ DUAS PORTAS DE ESCAPE, e as duas existem pra NÃO encolher alcance por falta de
        // dado — que seria um defeito mudo, do tipo que ninguém reclama porque ninguém sabe do
        // que deixou de saber:
        //   • torneio sem UF conhecida (ver UfDoTorneio) → vai pra base inteira;
        //   • jogador com o estado EM BRANCO → continua recebendo. São 44 das 172 contas
        //     ativas hoje, e o campo nunca foi obrigatório.
        var ufDoTorneio = await UfDoTorneio.DescobrirAsync(ctx, torneio.Id);

        var candidatos = await ctx.Jogadores
            .Where(j => j.NotificarTorneiosAbertos && j.ExcluidoEm == null)
            .Select(j => new { j.Id, j.Estado })
            .ToListAsync();

        // ⚠️ O filtro roda EM MEMÓRIA de propósito: `Jogador.Estado` é texto livre ("RS", "Rs",
        // "rs", "Rio Grande do Sul (RS)") e quem casa isso é o UnidadeFederativa, que o banco
        // não sabe executar. Comparar direto no SQL deixaria 39 pessoas do RS de fora —
        // conferido em produção.
        //
        // A VOLTA DA SÉRIE (20/08/2026): quem jogou uma edição anterior deste torneio sai do
        // anúncio genérico e recebe o pessoal ("o torneio que você jogou está de volta") —
        // e recebe MESMO morando em outro estado: o vínculo com a série vale mais que a UF.
        // A preferência NotificarTorneiosAbertos vale pros dois: os `candidatos` já vêm
        // filtrados por ela, e veterano só é avisado se estiver entre eles.
        var veteranos = (await VeteranosDaSerieAsync(ctx, torneio))
            .Intersect(candidatos.Select(j => j.Id))
            .ToHashSet();

        var elegiveis = candidatos
            .Where(j => ufDoTorneio == null || UnidadeFederativa.Combina(j.Estado, ufDoTorneio))
            .Select(j => j.Id)
            .Where(id => !veteranos.Contains(id))
            .ToList();

        // ⚠️ O CARIMBO VEM ANTES DO ENVIO, e é gravado MESMO quando não há ninguém elegível.
        // Antes: dois cliques rápidos no mesmo botão mandariam o anúncio duas vezes. Depois:
        // um torneio sem elegível nenhum voltaria a tentar a cada publicação, pra descobrir de
        // novo que não há quem avisar.
        torneio.AvisoDeTorneioNovoEm = DateTime.Now;
        await ctx.SaveChangesAsync();

        foreach (var jogadorId in veteranos)
        {
            await push.EnviarParaJogadorAsync(jogadorId, TextoDaVoltaDaSerie.Titulo,
                TextoDaVoltaDaSerie.Corpo(torneio.Nome), url, AlcanceDoAviso.AppSemEmail);
        }

        foreach (var jogadorId in elegiveis)
        {
            // ⚠️ SEM E-MAIL desde 09/08/2026, e este é o corte mais pesado daquele dia: eram 87
            // e-mails numa tacada só — o disparo que queimou a cota do Gmail e levou junto 130
            // e-mails, duas recuperações de senha entre eles.
            //
            // ⚠️ O preço disto, de olhos abertos (decisão do Felipe): o anúncio alcança só quem
            // tem o app instalado mais quem abrir a caixa de avisos no site.
            //
            // Só ENFILEIRA — a entrega sai pela FilaDeAvisos, por fora da requisição.
            await push.EnviarParaJogadorAsync(
                jogadorId, "Novo torneio aberto", torneio.Nome, url, AlcanceDoAviso.AppSemEmail);
        }

        return new Resultado(true, elegiveis.Count + veteranos.Count);
    }

    // Quem JOGOU (se inscreveu em) qualquer OUTRA edição da série deste torneio. A série é a
    // do DuplicacaoDeTorneio: toda edição aponta pro torneio-raiz, então "as outras edições"
    // é uma consulta só. Torneio fora de série devolve vazio sem tocar nas inscrições.
    private static async Task<HashSet<int>> VeteranosDaSerieAsync(DbPadelContext ctx, Torneio torneio)
    {
        int raiz = DuplicacaoDeTorneio.RaizDaSerie(torneio);

        var edicoesAnteriores = await ctx.Torneios
            .Where(t => t.Id != torneio.Id && (t.Id == raiz || t.TorneioOrigemId == raiz))
            .Select(t => t.Id)
            .ToListAsync();
        if (edicoesAnteriores.Count == 0) return new HashSet<int>();

        // As DUAS portas de inscrição, como sempre. Linha de TIME fica fora — o Jogador1
        // dela é o organizador que cadastrou o time, não quem jogou.
        var deDupla = (await ctx.Duplas
                .Where(d => d.NomeTime == null && edicoesAnteriores.Contains(d.Categoria.TorneioId))
                .Select(d => new { d.Jogador1Id, d.Jogador2Id })
                .ToListAsync())
            .SelectMany(d => new[] { (int?)d.Jogador1Id, d.Jogador2Id })
            .Where(id => id != null)
            .Select(id => id!.Value);

        var deAmericano = await ctx.InscricoesAmericanas
            .Where(i => edicoesAnteriores.Contains(i.Categoria.TorneioId))
            .Select(i => i.JogadorId)
            .ToListAsync();

        return deDupla.Concat(deAmericano).ToHashSet();
    }

    // A frase que o admin/organizador lê depois de aprovar ou publicar. Fica aqui porque as
    // duas telas contam a MESMA história e ela mudou de dono: prometer "o aviso saiu" numa
    // tela onde ele não saiu é o jeito mais rápido de o organizador achar que divulgou.
    public static string Frase(Resultado resultado) => resultado.Enviou
        ? $" O aviso de torneio novo saiu pra {resultado.Quantos} pessoa{(resultado.Quantos == 1 ? "" : "s")}."
        : "";
}
