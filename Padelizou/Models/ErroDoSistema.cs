using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Um erro não tratado que estourou em produção. Antes disto, o UseExceptionHandler mostrava a
// tela amiga e o erro só existia no journalctl do VPS — ninguém ficava sabendo até alguém
// reclamar. Agora todo 500 vira uma linha aqui e um aviso pros admins (ver
// Services/RegistroDeErros e a janela de silêncio em Services/VigiaDeErros).
[Table("ErroDoSistema")]
public class ErroDoSistema
{
    public int Id { get; set; }

    public DateTime QuandoEm { get; set; } = DateTime.Now;

    // Caminho da requisição que estourou (ex: "/Torneios/FinalizarPartida").
    [MaxLength(300)]
    public string Caminho { get; set; } = null!;

    [MaxLength(10)]
    public string Metodo { get; set; } = null!;

    // Nome do tipo da exceção (ex: "NullReferenceException"). Junto com o Caminho, é a
    // identidade do erro pra janela de silêncio: mesmo tipo no mesmo lugar não reavisa.
    [MaxLength(200)]
    public string Tipo { get; set; } = null!;

    [MaxLength(1000)]
    public string Mensagem { get; set; } = null!;

    // ex.ToString() truncado: tipo + mensagem + stack trace, incluindo as InnerExceptions.
    public string Detalhe { get; set; } = null!;

    // Quem estava logado quando estourou, se havia alguém. Sem FK de propósito: apagar a
    // conta do jogador (LGPD) não pode ser barrado nem arrastar o registro do erro.
    public int? JogadorId { get; set; }

    // Se este registro em particular gerou aviso pros admins (a maioria não gera — a janela
    // de silêncio segura as repetições).
    public bool AvisoEnviado { get; set; }
}
