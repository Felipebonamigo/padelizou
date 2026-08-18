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
        var elegiveis = candidatos
            .Where(j => ufDoTorneio == null || UnidadeFederativa.Combina(j.Estado, ufDoTorneio))
            .Select(j => j.Id)
            .ToList();

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
