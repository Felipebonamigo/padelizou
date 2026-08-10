using Padelizou.Models;

namespace Padelizou.Services;

// QUANDO o torneio acontece, escrito como as telas mostram.
//
// ⚠️ NASCEU DE UMA REGRA QUE MORAVA SÓ NO CARD DA LISTAGEM, e de uma tela que não mostrava nada:
// a página do torneio (Details) dizia o local, o preço, quando as inscrições fecham e quando sai
// a chave — **e não dizia em que dia o torneio é**. Só o card da listagem sabia. Descoberto em
// 09/08/2026 adiando o NATA PADEL TOUR de 26–27/09 pra 10–11/10: quem abrisse o link direto do
// torneio não tinha como saber a data nova.
//
// Copiar a formatação do card pro Details resolveria a tela e criaria o problema de sempre — duas
// cópias da mesma regra, uma delas fadada a ficar pra trás na próxima mudança (ver a lição da
// regra duplicada). Então a régua saiu do card e virou este arquivo, que os dois leem.
public static class DataDoTorneioNaTela
{
    // "10/10 a 11/10/2026" — torneio de mais de um dia mostra o INTERVALO. Só o primeiro dia
    // esconderia metade do evento de quem está decidindo se consegue ir.
    //
    // O ano aparece uma vez só, no fim: repetir em cada ponta é ruído, e o que muda entre as
    // duas datas quase sempre é só o dia.
    public static string Frase(Torneio torneio)
    {
        if (torneio.DataInicio is not { } inicio) return "Data a definir";

        return torneio.DataFim is { } fim && fim.Date > inicio.Date
            ? $"{inicio:dd/MM} a {fim:dd/MM/yyyy}"
            : inicio.ToString("dd/MM/yyyy");
    }

    // O torneio já tem data marcada? Serve pra tela decidir se vale gastar uma linha com isso —
    // "Data a definir" ao lado do local e do preço é ruído no cabeçalho, mas no CARD da listagem
    // é informação (ali a pessoa está comparando torneios e precisa saber que este ainda não tem
    // dia).
    public static bool TemData(Torneio torneio) => torneio.DataInicio != null;
}
