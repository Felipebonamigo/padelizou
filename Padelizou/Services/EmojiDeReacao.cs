using System.Globalization;
using System.Text;

namespace Padelizou.Services;

// A PENEIRA DA REAÇÃO: "isto é UM emoji?" — 12/09/2026.
//
// 🗣️ Felipe escolheu **teclado de emoji livre** em vez de paleta fechada: qualquer emoji que a
// pessoa digitar. É essa escolha que traz este arquivo. Com lista fechada, validar é comparar
// com a lista; sem ela, a coluna aceita o texto que o POST mandar — e um POST montado à mão
// gravaria "PAGUE AQUI: bit.ly/..." como reação de um jogo que o torneio inteiro vê. Validação
// em fronteira de confiança é um dos degraus que a escada do CLAUDE.md nunca encurta.
//
// A régua tem três partes, e nenhuma delas é uma lista de emoji:
//   1. UM GRAFEMA SÓ. Quem sabe onde um emoji termina é o `StringInfo` da BCL, que quebra por
//      UAX#29 — 🇧🇷 (dois indicadores regionais), 👨‍👩‍👧 (ZWJ), 👍🏽 (tom de pele) e 1️⃣ (keycap) são
//      um grafema cada, e "🔥🔥" são dois. Reimplementar isso à mão seria o degrau 3 da escada
//      ignorado.
//   2. TODO CODE POINT É DE EMOJI. Símbolo fora do ASCII, ou uma das peças que montam emoji
//      (ZWJ, seletor de variação, tom de pele, keycap, tag de subdivisão).
//   3. PELO MENOS UM SÍMBOLO DE VERDADE, senão um seletor de variação solto passaria.
//
// ⚠️ ATALHO DELIBERADO: emoji de pontuação legada (‼️ ⁉️) fica fora — o code point base deles é
// pontuação, não símbolo, e aceitar a categoria inteira abriria a porta pra "!" e "?" virarem
// reação. A saída, se alguém reclamar, é a propriedade Extended_Pictographic do Unicode, que a
// BCL não expõe (viria de tabela nossa ou de pacote novo — degrau 5 da escada diz que não).
public static class EmojiDeReacao
{
    // Teto da coluna. A sequência mais longa que existe de verdade é a bandeira de subdivisão
    // (🏴󠁧󠁢󠁳󠁣󠁴󠁿 = 14 unidades UTF-16); 32 dá folga e ainda impede que uma sequência montada à mão
    // encha a chave primária.
    public const int TamanhoMaximo = 32;

    private const char ZeroWidthJoiner = '‍';
    private const char SeletorDeTexto = '︎';      // VS15 — pede a forma preto e branco
    private const char SeletorDeEmoji = '️';      // VS16 — pede a forma colorida
    private const char EncaixeDoKeycap = '⃣';     // COMBINING ENCLOSING KEYCAP

    // O emoji do jeito que ele vai pro banco, ou NULO se não for UM emoji.
    //
    // ⚠️ NORMALIZAR NÃO É FRESCURA DE FORMATO: 👍 e 👍️ (com o VS16 invisível no fim) chegam de
    // teclados diferentes, são o MESMO desenho na tela, e sem isto o card mostraria duas
    // pílulas idênticas de 1 voto cada — quem clicasse na "outra" juraria que a reação sumiu.
    // A forma gravada é a LONGA, com o seletor: ele é inócuo em quem já nasce colorido (👍) e é
    // justamente o que faz o ❤ virar ❤️ em vez de um coração preto na fileira.
    public static string? Normalizar(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        var texto = bruto.Trim();
        if (texto.Length > TamanhoMaximo) return null;

        // UM grafema só: o que a pessoa vê como um emoji.
        if (StringInfo.GetNextTextElementLength(texto) != texto.Length) return null;

        if (!EhEmoji(texto)) return null;

        // A forma longa. Só o caso de UM code point é ambíguo — nas sequências (ZWJ, bandeira,
        // keycap) o seletor, quando cabe, já vem do teclado no lugar certo, e enfiá-lo no fim
        // quebraria a sequência.
        var runas = texto.EnumerateRunes().ToList();
        if (runas.Count == 1) return texto + SeletorDeEmoji;

        return texto;
    }

    private static bool EhEmoji(string grafema)
    {
        bool temKeycap = grafema.Contains(EncaixeDoKeycap);
        bool temSimbolo = false;

        foreach (var runa in grafema.EnumerateRunes())
        {
            // As peças que montam emoji e que, sozinhas, não são desenho nenhum.
            if (runa.Value is ZeroWidthJoiner or SeletorDeTexto or SeletorDeEmoji or EncaixeDoKeycap)
                continue;

            // Tags de bandeira de subdivisão (🏴󠁧󠁢󠁳󠁣󠁴󠁿 e as irmãs).
            if (runa.Value is >= 0xE0020 and <= 0xE007F)
                continue;

            // O dígito, o # e o * do keycap — e SÓ quando o encaixe do keycap está no grafema.
            // Sem essa amarra, "7" viraria reação.
            if (temKeycap && (runa.Value is >= '0' and <= '9' or '#' or '*'))
                continue;

            // ⚠️ SÍMBOLO **FORA DO ASCII**. A categoria sozinha deixaria passar "+" (Sm), "^"
            // (Sk) e "<" — nenhum deles é emoji, e o "<" é o que o modal monta com innerHTML.
            if (runa.Value > 0x7F && Rune.GetUnicodeCategory(runa) is
                    UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.MathSymbol)
            {
                temSimbolo = true;
                continue;
            }

            return false;   // letra, dígito solto, espaço, pontuação, controle: não é emoji
        }

        return temSimbolo || temKeycap;
    }
}
