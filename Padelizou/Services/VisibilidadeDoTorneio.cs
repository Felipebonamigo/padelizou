using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// "Esta pessoa consegue ABRIR este torneio?" — a régua do torneio OCULTO.
//
// 🕳️ O buraco que ela fecha (Felipe, 18/08/2026): *"ainda vamos divulgar, e só aí permitir que
// o pessoal veja"*. O torneio nasce montado dias antes do anúncio — capa, categorias, preço —,
// e até 18/08 não havia estado nenhum pra isso: ou ele estava no ar pra todo mundo, ou o
// organizador ficava sem poder montá-lo.
//
// ⚠️ ATÉ 18/08/2026 `Oculto` QUERIA DIZER OUTRA COISA: "some da listagem, mas o link direto
// abre". Passou a querer dizer INVISÍVEL — decisão do Felipe, tomada com a pergunta na mesa. A
// diferença importa pra quem lê isto no futuro: o comentário do campo em Models/Torneio.cs foi
// reescrito junto, e não sobrou nenhum lugar dizendo a regra antiga.
//
// São TRÊS escapes, e o terceiro não é generosidade — é o que impede o estrago:
//   1. quem CUIDA do torneio (organizador, criador) — é dele o torneio;
//   2. quem OLHA TUDO (admin e assistente do sistema) — mesma régua de sempre;
//   3. quem JÁ ESTÁ INSCRITO. Esconder um torneio depois de inscrever gente não pode
//      trancar do lado de fora quem já pagou: ele perderia a chave, o horário do jogo e a
//      página inteira, sem nunca entender por quê. E é isto que protege os torneios
//      restritos+ocultos que já existiam quando a regra mudou.
//
// A recusa é 404, nunca 403: o 403 confirma que o torneio existe (e o nome dele sai na URL
// compartilhada), o 404 não conta nada. É também o que o buscador precisa ler pra tirar do
// índice o que o organizador escondeu.
public static class VisibilidadeDoTorneio
{
    // A pergunta pura, sem banco — pra quem já tem as duas respostas na mão (o Details, que
    // calcula as duas de qualquer jeito, e os testes).
    public static bool PodeAbrir(Torneio torneio, bool cuidaDoTorneio, bool estaInscrito) =>
        !torneio.Oculto || cuidaDoTorneio || estaInscrito;

    // A mesma pergunta pra quem só tem o id de quem está olhando.
    //
    // ⚠️ Curto-circuito no primeiro `if`: torneio visível — que é o caso de 99% das aberturas
    // de página do site — não custa uma consulta sequer. Só o oculto paga pra ser conferido.
    public static async Task<bool> PodeAbrirAsync(DbPadelContext ctx, Torneio torneio, int? jogadorId)
    {
        if (!torneio.Oculto) return true;
        if (jogadorId is not int quem || quem <= 0) return false;

        if (await ctx.TorneioOrganizadores.AnyAsync(o => o.TorneioId == torneio.Id && o.JogadorId == quem))
            return true;

        if (PoderesNoSistema.PodeOlharTudo(await ctx.Jogadores.FindAsync(quem)))
            return true;

        return await EstaInscritoAsync(ctx, torneio.Id, quem);
    }

    // Inscrito nas duas famílias de inscrição: dupla (torneio de duplas) e individual
    // (Americano). Conta quem está na lista de espera e quem ainda não pagou — a pergunta
    // aqui é "esta pessoa já entrou no torneio?", não "esta vaga está garantida".
    private static async Task<bool> EstaInscritoAsync(DbPadelContext ctx, int torneioId, int jogadorId) =>
        await ctx.Duplas.AnyAsync(d => d.Categoria.TorneioId == torneioId
                                       && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
        || await ctx.InscricoesAmericanas.AnyAsync(i => i.Categoria.TorneioId == torneioId
                                                        && i.JogadorId == jogadorId);

    // O que o organizador lê no topo da própria página enquanto o torneio está escondido.
    // Fica aqui, e não na view, porque a mesma frase aparece na página do torneio e no
    // recado do botão — e as duas têm que dizer a mesma coisa.
    public const string AvisoDeOculto =
        "Este torneio está OCULTO: ele não aparece na lista de torneios, na página inicial "
        + "nem no Google, e quem receber o link não consegue abrir. Só você, os outros "
        + "organizadores e quem já está inscrito estão vendo esta página.";

    public const string ConviteParaPublicar =
        "Quando estiver pronto pra divulgar, é só publicar — o torneio aparece na hora pra todo mundo.";
}
