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
// perguntam "dá pra avisar?"; as quatro recusas (não aprovado, oculto, já avisado, restrito)
// moram aqui — e a MIRA também, que num torneio de time deixa de ser o estado e passa a ser a
// camisa.
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
        // 4. RESTRITO (20/09/2026): quem não tem a CHAVE não se inscreve, então anunciar pra
        //    base é convidar todo mundo pra uma festa de convidados. 🗣️ Felipe, sobre o push do
        //    "Los Corneteiros | Seletiva QTimes": *"esse torneio é restrito, ai nao deveria
        //    aparecer"*.
        //    ⚠️ E aqui, como no OCULTO, NÃO se carimba: um torneio que deixar de ser restrito
        //    ainda vai querer o anúncio, e o carimbo é pra sempre.
        //    ⚠️ ISTO NÃO ESCONDE NADA: o torneio segue na listagem e na página dele. Quem some
        //    da vista é o `Oculto`, logo acima, que é outra coisa.
        if (torneio.AprovadoEm == null || torneio.Oculto || torneio.AvisoDeTorneioNovoEm != null
            || torneio.Restrito)
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
        var candidatos = await ctx.Jogadores
            .Where(j => j.NotificarTorneiosAbertos && j.ExcluidoEm == null)
            .Select(j => new { j.Id, j.Estado, j.TimeId })
            .ToListAsync();

        // TORNEIO DE UM TIME SÓ: a camisa SUBSTITUI a mira por estado (20/09/2026). Silenciar
        // por completo tiraria o aviso de quem PODE jogar; e somar os dois filtros cortaria o
        // jogador do time que mora em outro estado — caladinho, justamente quem se desloca pra
        // jogar pelo time. Num torneio de time a camisa é o sinal mais forte que existe.
        if (torneio.TimeExclusivoId is int timeDaCamisa)
        {
            var doTime = candidatos.Where(j => j.TimeId == timeDaCamisa).Select(j => j.Id).ToList();
            return await CarimbarEEnviarAsync(ctx, push, torneio, url, doTime);
        }

        var ufDoTorneio = await UfDoTorneio.DescobrirAsync(ctx, torneio.Id);

        // ⚠️ O filtro roda EM MEMÓRIA de propósito: `Jogador.Estado` é texto livre ("RS", "Rs",
        // "rs", "Rio Grande do Sul (RS)") e quem casa isso é o UnidadeFederativa, que o banco
        // não sabe executar. Comparar direto no SQL deixaria 39 pessoas do RS de fora —
        // conferido em produção.
        var elegiveis = candidatos
            .Where(j => ufDoTorneio == null || UnidadeFederativa.Combina(j.Estado, ufDoTorneio))
            .Select(j => j.Id)
            .ToList();

        return await CarimbarEEnviarAsync(ctx, push, torneio, url, elegiveis);
    }

    // O CARIMBO E O ENVIO, num lugar só — chamado pelas DUAS miras (estado e camisa).
    //
    // ⚠️ Existe pelo mesmo motivo que este arquivo existe: copiar isto pro caminho do time
    // exclusivo seria repetir a cópia divergente que tirou o aviso de dentro do controller em
    // 18/08/2026. Aqui a cópia errada não é tela torta, é push saindo duas vezes ou nenhuma.
    private static async Task<Resultado> CarimbarEEnviarAsync(
        DbPadelContext ctx, IPushNotificationService push, Torneio torneio, string? url,
        List<int> elegiveis)
    {
        // ⚠️ O CARIMBO VEM ANTES DO ENVIO, e é gravado MESMO quando não há ninguém elegível.
        // Antes: dois cliques rápidos no mesmo botão mandariam o anúncio duas vezes. Depois:
        // um torneio sem elegível nenhum voltaria a tentar a cada publicação, pra descobrir de
        // novo que não há quem avisar.
        torneio.AvisoDeTorneioNovoEm = DateTime.Now;
        await ctx.SaveChangesAsync();

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

        return new Resultado(true, elegiveis.Count);
    }

    // A frase que o admin/organizador lê depois de aprovar ou publicar. Fica aqui porque as
    // duas telas contam a MESMA história e ela mudou de dono: prometer "o aviso saiu" numa
    // tela onde ele não saiu é o jeito mais rápido de o organizador achar que divulgou.
    public static string Frase(Resultado resultado) => resultado.Enviou
        ? $" O aviso de torneio novo saiu pra {resultado.Quantos} pessoa{(resultado.Quantos == 1 ? "" : "s")}."
        : "";
}
