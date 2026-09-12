using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Avisa quem está SEGUINDO um jogo ao vivo que o placar mudou (16/08/2026, pedido do Felipe:
// "algo parecido com o placar que o Google mostra na tela de bloqueio"). Chamado depois de
// QUALQUER gravação de placar que pegou — Mesa de Controle (SincronizarPlacar) e a lista em
// lote (SalvarPlacaresAoVivo) escrevem o mesmo jogo por dois caminhos diferentes, e os dois
// precisam concordar; ficar num lugar só é o que garante isso (ver feedback sobre regra
// duplicada: a segunda cópia é sempre a que ninguém lembra de atualizar).
public class AvisoDePlacarAoVivo
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AvisoDePlacarAoVivo(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    private static string TagDe(int partidaId) => $"partida-{partidaId}";

    // O ENDEREÇO DO CARD DENTRO DA NOTIFICAÇÃO (a `image` do showNotification).
    //
    // Montar rota é trabalho do controller em todo o resto daqui — e este é a exceção pelo
    // mesmo motivo da `tag` logo acima: ele depende do PLACAR, que só existe depois da consulta
    // que este serviço faz. Quem chama tem o id da partida e mais nada.
    //
    // ⚠️ O `?p=` NÃO É ENTRADA DE NADA: o desenho sai do banco. Ele existe pra separar uma
    // versão do card da outra no cache — a resposta é `Cache-Control: public, max-age=3600`
    // (EntregaDeCard), e sem trocar de endereço a cada game o celular mostraria o card do
    // placar anterior embaixo de um título já atualizado. O estado entra junto porque o placar
    // final e o último game AO VIVO têm os mesmos números e desenhos diferentes.
    private static string EnderecoDoCard(Partida partida) =>
        $"/Torneios/CartaoDoPlacarAoVivo/{partida.Id}"
        + $"?p={partida.GamesDupla1 ?? 0}-{partida.GamesDupla2 ?? 0}"
        + (partida.Status == "Finalizada" ? "-f" : "");

    // `url` é montado por quem chama: o serviço não carrega IUrlHelper só pra isso (mesmo
    // motivo do LinksDoAviso, em TorneiosController.Placar).
    public async Task AvisarSeguidoresAsync(int partidaId, string? url)
    {
        var seguidores = await _context.Set<SeguidorDePartida>()
            .Where(s => s.PartidaId == partidaId)
            .Select(s => s.JogadorId)
            .ToListAsync();

        // A consulta mais cara (a partida com as duas duplas carregadas) só roda quando existe
        // alguém pra avisar — a maioria dos jogos não tem seguidor nenhum, e cada game marcado
        // não pode custar um JOIN de quatro tabelas à toa.
        if (seguidores.Count == 0) return;

        var partida = await _context.Partidas
            .Include(p => p.Categoria)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .FirstOrDefaultAsync(p => p.Id == partidaId);

        if (partida == null) return;

        var (titulo, corpo) = Mensagem(partida);
        var tag = TagDe(partidaId);

        var imagem = EnderecoDoCard(partida);

        foreach (var jogadorId in seguidores)
            await _push.EnviarPlacarAoVivoAsync(jogadorId, titulo, corpo, url ?? "/", tag, imagem);
    }

    // O jogo acabou: manda o placar FINAL (a última atualização da notificação que vinha
    // trocando de conteúdo) e para de seguir — não há mais "ao vivo" pra acompanhar, e manter
    // a linha só acumularia lixo que nunca mais dispara nada.
    public async Task AvisarFimEPararDeSeguirAsync(int partidaId, string? url)
    {
        await AvisarSeguidoresAsync(partidaId, url);

        var seguidores = await _context.Set<SeguidorDePartida>()
            .Where(s => s.PartidaId == partidaId)
            .ToListAsync();

        if (seguidores.Count > 0)
        {
            _context.RemoveRange(seguidores);
            await _context.SaveChangesAsync();
        }
    }

    // COMO UMA DUPLA SE CHAMA NO PLACAR AO VIVO — uma régua só, usada pelo TEXTO da notificação
    // (aqui embaixo) e pelo DESENHO do card (Services/CartaoDoPlacarAoVivo, montado no
    // controller). Duas cópias divergiriam no primeiro ajuste, e o estrago apareceria dentro da
    // mesma notificação: um nome no título, outro na imagem logo abaixo.
    //
    // ⚠️ VAGA DE PARCEIRO EM ABERTO (09/09/2026): a inscrição sozinha passou a entrar na chave,
    // então isto virou alcançável com metade dos nomes — e saía "Paulo/? 6 x 0 João/Maria", com
    // a barra pendurada num "?" que não diz nada. Quando falta o segundo, sai só quem existe.
    //
    // ⚠️ E É A MESMA régua de nome curto do resto do sistema (chip do jogo, tabela de
    // classificação): cortar no primeiro espaço aqui seria a segunda cópia, e no Americano ela
    // erra (duas Carolines, duas Natálias — ver Views/Torneios/_JogoEmLinha.cshtml).
    public static string NomeDaDupla(Padelizou.Models.Dupla? d)
    {
        string Nome(Jogador? j) => j == null ? "?" : NomeBonito.Curto(j.Nome);

        return d == null ? "A definir"
            : d.EhTime ? (d.NomeTime ?? "Time")
            : d.Jogador2 == null ? Nome(d.Jogador1)
            : $"{Nome(d.Jogador1)}/{Nome(d.Jogador2)}";
    }

    // O TEXTO DA NOTIFICAÇÃO: o título é a linha que o Android mostra com ela recolhida.
    private static (string titulo, string corpo) Mensagem(Partida partida)
    {
        // ⚠️ O PLACAR VAI NO TÍTULO, e o estado do jogo no corpo (12/09/2026). 🗣️ Felipe, com a
        // notificação chegando de verdade: *"as notificações estao acontecendo, mas eu queria
        // algo tipo esses prints"* — a bolha do app do Google e o placar na Dynamic Island.
        // Nenhum dos dois é alcançável por um site; o que a notificação da web tem é a LINHA
        // RECOLHIDA, e ela mostra o título antes de tudo. Com "Placar ao vivo" ali, quem olhava
        // o celular de longe lia um rótulo e precisava abrir pra saber o jogo.
        var titulo = $"{NomeDaDupla(partida.Dupla1)} {partida.GamesDupla1 ?? 0} × "
            + $"{partida.GamesDupla2 ?? 0} {NomeDaDupla(partida.Dupla2)}";

        // O corpo é o que o título não cabe: em que pé o jogo está, e de qual categoria e fase
        // ele é. Repetir o placar aqui seria a segunda linha dizendo a primeira.
        var estado = partida.Status == "Finalizada" ? "Jogo encerrado" : "Ao vivo";
        var contexto = new[] { CategoriaNaTela.Curto(partida.Categoria?.Nome), partida.Fase }
            .Where(t => !string.IsNullOrWhiteSpace(t));
        var corpo = string.Join(" · ", new[] { estado }.Concat(contexto));

        return (titulo, corpo);
    }
}
