namespace Padelizou.Services;

// O interruptor das MOLDURAS — mesmo padrão dos Desafios: o código inteiro está no ar, mas
// nasce DESLIGADO. O Felipe pediu (14/08/2026, com tudo já publicado): "não publica nada
// ainda das molduras, estou pensando em elas serem por conquistas, e essas enfeitadas serem
// pagas". Enquanto ele decide o modelo, ninguém vê a porta — e ligar, quando for a hora, é
// uma linha no systemd (`Molduras__Habilitado=true`), não um deploy.
public class MoldurasSettings
{
    // false = em decisão: só admin do Padelizou entra na tela (pra avaliar ao vivo).
    // true  = liberado pros jogadores.
    public bool Habilitado { get; set; }

    // As DIVERTIDAS (Livre no catálogo) têm interruptor PRÓPRIO, e a decisão do Felipe
    // (14/08/2026) é elas ficarem GUARDADAS: o modelo dele é moldura por conquista já, e as
    // enfeitadas viram produto pago no futuro — quando houver volume e um jeito de cobrar
    // que não seja microtransação com confirmação manual. Desligado, elas não aparecem no
    // guarda-roupa e o POST recusa; os desenhos ficam prontos no repo esperando o dia.
    public bool DivertidasLiberadas { get; set; }
}
