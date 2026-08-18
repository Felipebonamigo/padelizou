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

    // ANONIMATO (18/08/2026): quem escreve pela enquete de fim de torneio escolhe se assina.
    // Marcado, o depoimento sai da home como "um jogador do Padelizou" — sem primeiro nome e
    // sem cidade, que juntos identificam gente numa base pequena.
    //
    // ⚠️ Ele não substitui o `Exibir`: anônimo continua nascendo invisível como todo o resto.
    // São perguntas diferentes — "isto pode aparecer?" (admin) e "com o nome de quem?" (autor).
    public bool Anonimo { get; set; }

    // De onde veio, quando não veio do rodapé do site. A enquete pós-torneio manda o texto
    // sobre o sistema pra cá, e sem esta coluna o admin lê "as quadras estavam ótimas" sem
    // saber de que torneio a pessoa estava voltando.
    public int? TorneioId { get; set; }
    public virtual Torneio? Torneio { get; set; }
}
