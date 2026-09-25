namespace Padelizou.ViewModels;

// Uma linha da tela "Desistências" do organizador. ⚠️ Tipo próprio, e não a entidade crua, pelo
// mesmo motivo do ProfessorNoAdmin: os nomes e o rótulo do motivo são RESOLVIDOS na leitura, e
// mandar a entidade obrigaria a Razor a fazer isso — definição que mora na tela é definição que
// muda sem ninguém perceber.
public record SaidaNaTela(
    DateTime SaiuEm,

    // Os nomes de quem saiu, já juntos ("Fulano e Beltrano"). Resolvidos agora porque o
    // histórico guarda id, não cópia do nome (ver Models/SaidaDoTorneio).
    string QuemSaiu,

    string Categoria,

    // "Desistiu", "O organizador removeu" ou "Não pagou no prazo".
    string Motivo,

    // Quem apertou o botão. Vazio quando foi o sistema.
    string? QuemPediu,

    string? Observacao,
    bool EstavaPaga,
    bool AbriuVaga);
