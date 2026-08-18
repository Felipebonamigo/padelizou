using Padelizou.Models;

namespace Padelizou.Services;

// Casa o extrato do banco contra a fila do Pix direto — a parte que decide "isto aqui é
// dinheiro de quem", sem tocar banco de dados nem HTTP. Testável sozinha, o que importa
// porque é a peça que decide se um Pix vira mensalidade paga.
//
// Duas situações, dois níveis de confiança:
//
//   1. O Pix carrega o TxId da NOSSA cobrança (Services/PixDireto.TxId) e o valor bate com
//      o que a cobrança pedia — é a mesma confiança que já existe hoje quando o admin olha
//      o extrato manualmente e confere o `PDZ...`. Confirma sozinho.
//
//   2. O Pix não tem TxId nenhum (o professor pagou combinado por fora, tipo WhatsApp) mas
//      o CPF de quem pagou é de um professor cadastrado — vira SUGESTÃO na fila do
//      /Admin/PixDireto, não confirmação. CPF batendo não é prova suficiente sozinho: podia
//      ser qualquer Pix da vida pessoal do professor caindo por coincidência. Quem decide é
//      o admin, com um clique — a mesma tela de sempre, sem tela nova.
public static class ConciliacaoAutomaticaDoPix
{
    public record Resultado(
        List<(Pagamento Pendente, PixRecebidoDoBanco Credito)> ParaConfirmar,
        List<(Jogador Professor, PixRecebidoDoBanco Credito)> ParaSugerir);

    public static Resultado Conciliar(
        IReadOnlyList<PixRecebidoDoBanco> recebidos,
        IReadOnlyList<Pagamento> pendentes,   // MetodoPagamento==PixDireto, Status Pendente/AguardandoConfirmacao
        IReadOnlyList<Jogador> professores)   // candidatos ao casamento por CPF
    {
        var paraConfirmar = new List<(Pagamento, PixRecebidoDoBanco)>();
        var paraSugerir = new List<(Jogador, PixRecebidoDoBanco)>();

        // Cada cobrança pendente só pode ser paga por UM Pix — depois de casada, sai da mesa
        // pra não ser reaproveitada por um segundo crédito de valor igual no mesmo lote.
        var pendentesLivres = pendentes.ToList();

        foreach (var credito in recebidos.OrderBy(r => r.Horario))
        {
            var porTxId = credito.TxId == null
                ? null
                : pendentesLivres.FirstOrDefault(p =>
                    PixDireto.TxId(p.Id) == credito.TxId && p.Valor == credito.Valor);

            if (porTxId != null)
            {
                paraConfirmar.Add((porTxId, credito));
                pendentesLivres.Remove(porTxId);
                continue;
            }

            if (credito.CpfCnpjPagador == null) continue;

            var soDigitos = new string(credito.CpfCnpjPagador.Where(char.IsDigit).ToArray());
            var professor = professores.FirstOrDefault(j => j.Cpf == soDigitos);
            if (professor != null)
                paraSugerir.Add((professor, credito));
        }

        return new Resultado(paraConfirmar, paraSugerir);
    }
}
