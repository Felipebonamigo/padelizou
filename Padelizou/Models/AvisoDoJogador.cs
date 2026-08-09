using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A CAIXA DE ENTRADA do jogador: cada aviso que o sistema mandou pra ele, guardado.
//
// POR QUE ESTA TABELA EXISTE: até aqui o aviso era só ENTREGA — push, e-mail e WhatsApp
// saíam e o sistema esquecia. Quem estava sem sinal, com a notificação apagada sem querer ou
// simplesmente sem o app instalado (a maioria: 4 aparelhos em 128 jogadores) não tinha
// NENHUM lugar pra descobrir o que perdeu. O aviso mais importante do dia — "seu jogo é o
// próximo" — dependia de o celular estar na mão na hora certa.
//
// ⚠️ Ela é preenchida DENTRO de `PushNotificationService.EntregarAgoraAsync`, o funil por
// onde todo aviso do sistema já passa. É de propósito: a lista nasce completa, em vez de
// depender de alguém lembrar de gravar aqui a cada tipo novo de aviso — que é como uma tela
// dessas fica pela metade em três meses.
//
// ⚠️ E é no ENTREGAR, não no enfileirar: o enfileirar roda dentro da requisição e não pode
// tocar o banco (ver o comentário do `EnviarParaJogadorAsync` — foi assim que "Publicando o
// torneio…" passou a demorar minutos).
[Table("AvisoDoJogador")]
public class AvisoDoJogador
{
    public int Id { get; set; }

    public int JogadorId { get; set; }

    public string Titulo { get; set; } = null!;
    public string Corpo { get; set; } = null!;

    // Pra onde o aviso leva quando tocado. Nulo = aviso sem destino (recado geral).
    public string? Url { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // Nulo = não lida. É o que acende o contador no menu.
    public DateTime? LidaEm { get; set; }

    [ForeignKey(nameof(JogadorId))]
    public virtual Jogador Jogador { get; set; } = null!;
}
