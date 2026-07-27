namespace Padelizou.Models;

// O que o jogador achou do Padelizou: opinião, sugestão ou crítica, mais uma nota de 0 a 10.
//
// Nasce INVISÍVEL. Nada do que é escrito aqui aparece em tela até um admin marcar Exibir —
// é canal de conversa com quem toca o sistema, não mural público, e crítica escrita de peito
// aberto não pode virar exposição de quem escreveu.
public class FeedbackSite
{
    public int Id { get; set; }

    // Exige login: sem autor não dá pra responder a pessoa nem separar o que é real do que
    // é robô passando na rua.
    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    // 0 a 10. Nulo quando a pessoa só quis escrever, sem dar nota.
    public int? Nota { get; set; }

    public string Texto { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // Moderação: só o admin liga, e só depois de ler.
    public bool Exibir { get; set; }
    public DateTime? ExibidoEm { get; set; }

    // Marca que a equipe já leu — pra saber o que ainda falta olhar, sem publicar nada.
    public bool Lido { get; set; }
}
