using System.Globalization;
using System.Text;

namespace Padelizou.Services;

// O "Pix copia e cola" — o BR Code do Banco Central — montado aqui dentro, sem API de banco.
//
// Por que dá pra gerar sozinho: um código ESTÁTICO carrega só chave, valor e uns rótulos. Não
// precisa de integração nenhuma, e é isso que torna o recebimento direto possível hoje: o
// dinheiro cai na conta do Padelizou e o custo por transação é zero.
//
// ⚠️ O preço disso é a CONCILIAÇÃO. Código estático não avisa ninguém quando é pago — não há
// webhook. Quem confirma que o dinheiro entrou é uma pessoa, olhando o extrato do banco (ver
// Services/PixDireto). Trocar isto por Pix de verdade com API do banco é o que elimina esse
// passo manual, e o resto do fluxo já está pronto pra receber a confirmação automática.
//
// Formato: EMV®QRCPS-MPM. Cada campo é ID(2) + tamanho(2) + valor, e o CRC16 do fim cobre o
// payload inteiro, incluindo os próprios caracteres "6304".
public static class PixCopiaECola
{
    // Limites do BR Code. Estourar não dá erro do nosso lado: quem recusa o código é o banco
    // do pagador, e aí a pessoa só vê "QR inválido" sem a menor pista do motivo.
    private const int MaxNome = 25;
    private const int MaxCidade = 15;
    private const int MaxTxId = 25;

    // Usados quando a limpeza não deixa nada aproveitável (nome só de acento, por exemplo).
    // O BR Code exige esses dois campos preenchidos — emitir vazio geraria um código quebrado.
    private const string NomePadrao = "PADELIZOU";
    private const string CidadePadrao = "PORTO ALEGRE";

    public static string Montar(string chave, string nome, string cidade, decimal valor, string txId)
    {
        // O campo 01 (Point of Initiation) fica de FORA de propósito: o exemplo canônico do
        // BCB para código estático não o traz, e é a forma que mais banco aceita sem discutir.
        var contaPix = Campo("00", "br.gov.bcb.pix") + Campo("01", chave.Trim());

        var payload =
            Campo("00", "01") +                                          // versão do formato
            Campo("26", contaPix) +
            Campo("52", "0000") +                                        // MCC: não classificado
            Campo("53", "986") +                                         // moeda: BRL
            Campo("54", valor.ToString("0.00", CultureInfo.InvariantCulture)) +
            Campo("58", "BR") +
            Campo("59", ComPadrao(Limpar(nome, MaxNome), NomePadrao)) +
            Campo("60", ComPadrao(Limpar(cidade, MaxCidade), CidadePadrao)) +
            // 62-05 é o "Reference Label": o identificador que vários bancos mostram no
            // extrato. É o que dá uma chance de casar o Pix com a cobrança sem adivinhação —
            // mas não dá pra CONTAR com ele, porque nem todo banco exibe. Por isso a tela do
            // admin também mostra valor e pagador.
            Campo("62", Campo("05", Limpar(txId, MaxTxId)));

        // O CRC entra por último e cobre o "6304" dele mesmo — daí concatenar antes de calcular.
        var comCabecalhoDoCrc = payload + "6304";
        return comCabecalhoDoCrc + Crc16(comCabecalhoDoCrc);
    }

    // ID + tamanho em 2 dígitos + valor. Nenhum campo daqui passa de 99 caracteres (a maior
    // chave Pix possível é um e-mail de 77), então o "00" nunca transborda.
    private static string Campo(string id, string valor) =>
        id + valor.Length.ToString("00", CultureInfo.InvariantCulture) + valor;

    private static string ComPadrao(string valor, string padrao) =>
        valor.Length == 0 ? padrao : valor;

    // O BR Code é ASCII. Acento vira letra sem acento (não vira lixo), e o que sobra fora da
    // tabela é descartado — "São José" tem que virar "SAO JOSE", não "S?O JOS?".
    private static string Limpar(string texto, int max)
    {
        var semAcento = texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

        var sb = new StringBuilder();
        foreach (var c in semAcento)
            if (c < 128 && !char.IsControl(c)) sb.Append(c);

        var limpo = sb.ToString().Trim().ToUpperInvariant();
        // O Trim de novo depois de cortar: truncar no meio de um espaço deixaria o campo
        // terminando em branco, e aí o tamanho declarado não bate com o que o banco lê.
        return limpo.Length > max ? limpo[..max].Trim() : limpo;
    }

    // CRC16/CCITT-FALSE: polinômio 0x1021, começa em 0xFFFF, sem reflexão e sem xor final.
    // É o que a especificação do BR Code manda, e errar aqui dá "QR inválido" em todo banco.
    // Público pra o teste poder ancorar no vetor oficial do algoritmo ("123456789" → 29B1) —
    // um CRC caseiro validado só contra ele mesmo não prova nada.
    public static string Crc16(string texto)
    {
        ushort crc = 0xFFFF;

        foreach (byte b in Encoding.UTF8.GetBytes(texto))
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
