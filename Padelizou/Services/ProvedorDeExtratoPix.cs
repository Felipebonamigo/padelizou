namespace Padelizou.Services;

// Um Pix que caiu na conta do Padelizou, do jeito que o extrato do banco descreve — com ou
// sem cobrança nossa por trás. É a peça que faz o ConciliacaoAutomaticaDoPix não precisar
// saber que existe um "Inter": ele só recebe uma lista destes e casa contra o banco.
public record PixRecebidoDoBanco(
    string EndToEndId,
    decimal Valor,
    DateTime Horario,

    // CPF ou CNPJ de quem pagou, só dígitos — é o que permite casar um Pix SEM cobrança
    // (mandado combinado por fora, tipo WhatsApp) contra um professor cadastrado.
    string? CpfCnpjPagador,
    string? NomePagador,

    // O identificador que a PRÓPRIA cobrança levou no campo 62-05 do BR Code
    // (Services/PixDireto.TxId) — quando o banco do pagador repassa. Nulo quando o Pix não
    // veio de uma cobrança nossa, ou quando o banco dele não repassa o campo.
    string? TxId);

// A fonte do extrato — hoje é o Inter, mas o resto do sistema (a conciliação, a fila do
// admin) não sabe disso. Trocar de banco um dia é escrever OUTRA classe que implementa isto,
// e nada mais muda.
public interface IProvedorDeExtratoPix
{
    Task<IReadOnlyList<PixRecebidoDoBanco>> ListarRecebidosAsync(
        DateTime desde, DateTime hasta, CancellationToken ct);
}
