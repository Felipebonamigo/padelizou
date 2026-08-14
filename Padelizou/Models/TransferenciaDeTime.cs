namespace Padelizou.Models;

// A janela de transferências: quem trocou de camisa, e quando.
//
// O `Jogador.TimeId` responde "onde ele está HOJE" e sobrescreve o passado a cada troca — a
// pergunta "quem saiu do Nata neste mês?" não tinha como ser respondida. Cada linha aqui é um
// movimento, e é isso que a aba de transferências lista.
//
// ⚠️ NINGUÉM escreve `Jogador.TimeId` na mão. Quem troca chama Services/TransferenciasDeTime,
// que grava a coluna e a linha do histórico juntas — uma troca que escape do serviço não
// "some" da tela do time (o TimeId muda igual), ela some do HISTÓRICO, que é o buraco mais
// difícil de notar: a aba simplesmente mostra uma transferência a menos.
public class TransferenciaDeTime
{
    public int Id { get; set; }

    public int JogadorId { get; set; }
    public Jogador Jogador { get; set; } = null!;

    // De onde saiu. NULO = não tinha time antes — é uma ESTREIA, não uma saída, e a aba
    // precisa dessa diferença pra não anunciar que alguém "deixou o time" que nunca teve.
    public int? TimeAnteriorId { get; set; }
    public Time? TimeAnterior { get; set; }

    // Pra onde foi. NULO = tirou a camisa e não vestiu outra (saiu do time no perfil).
    public int? TimeNovoId { get; set; }
    public Time? TimeNovo { get; set; }

    public DateTime Em { get; set; }
}
