using SkiaSharp;

namespace Padelizou.Services;

// O PNG do "Seu ano no padel".
//
// ⚠️ NENHUM ZERO APARECE AQUI, e isso é regra e não estética. "0 TÍTULOS" num card que a pessoa
// vai postar é um motivo pra ela NÃO postar — e a maioria dos jogadores não tem título nenhum.
// É a mesma decisão que tirou do ranking o "1º lugar — Fulano — 0 pontos": número que só sabe
// dizer que você não fez nada não vai pra tela. Título, parceiro e clube são seções que existem
// quando existem, e o card se recompõe sem elas.
//
// ⚠️ AS ALTURAS SÃO CONSTANTES NOMEADAS, e o desenho anda com um cursor que soma a ALTURA DE
// CADA BLOCO. A primeira versão somava números escolhidos no olho e três blocos se
// sobrepuseram: a pílula do ano por cima da foto, o "1 TÍTULO NO ANO" por cima dos rótulos
// APROVEITAMENTO/TORNEIOS, e "15 jogos" por cima do rodapé. O motivo era um só e vale anotar:
// `Pilula` e `FotoRedonda` recebem o CENTRO, e `Texto` recebe a LINHA DE BASE — misturar as
// duas convenções num mesmo `y += ...` erra por meia altura de bloco, toda vez.
public static class CartaoDoAno
{
    // ── A régua vertical, de cima pra baixo ────────────────────────────────────────────────
    private const float LogoCentro = 128;
    private const float LogoAltura = 92;

    private const float TituloBase = 250;       // linha de base de "SEU ANO NO PADEL"
    private const float AnoCentro = 322;        // centro da pílula do ano (altura 90 → 277..367)
    private const float AnoTamanho = 46;

    private const float FotoRaio = 104;
    private const float FotoCentro = 500;       // 396..604

    private const float NomeBase = 668;

    private const float NumerosBase = 770;      // linha de base dos números de cima
    private const float NumerosEntreLinhas = 146;
    private const float RotuloAbaixoDoNumero = 44;
    // Onde o bloco dos quatro números termina: rótulo de baixo + a descida da letra.
    private const float NumerosFim = NumerosBase + NumerosEntreLinhas + RotuloAbaixoDoNumero + 12;

    private const float PilulaTituloAltura = 84; // 40 de texto + 44 de folga (ver Pilula)
    private const float DestaqueRotuloAteNome = 54;
    private const float DestaqueNomeAteDetalhe = 40;

    public static byte[] Desenhar(RetrospectivaVM ano, FonteDoCartao fontes, string webRootPath)
    {
        var foto = CartaoCompartilhavel.Ler(webRootPath, ano.Jogador.FotoPerfil);
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, CartaoDeCampeao.LogoDaMarca);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                CartaoCompartilhavel.Logo(
                    canvas, logo, CartaoCompartilhavel.Largura / 2f, LogoCentro, LogoAltura);

                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "SEU ANO NO PADEL", TituloBase, fontes.Forte, 68,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140);

                CartaoCompartilhavel.Pilula(canvas, ano.Ano.ToString(), AnoCentro, fontes, AnoTamanho);

                CartaoCompartilhavel.FotoRedonda(
                    canvas, foto, CartaoCompartilhavel.Largura / 2f, FotoCentro, FotoRaio,
                    fontes, ano.Jogador.Nome);

                CartaoCompartilhavel.TextoCentralizado(
                    canvas, ano.Jogador.ComoChamar, NomeBase, fontes.Forte, 56,
                    CartaoCompartilhavel.LimeClaro, CartaoCompartilhavel.Largura - 160, tamanhoMinimo: 30);

                Numeros(canvas, fontes, ano);

                float y = NumerosFim;

                if (ano.Titulos > 0)
                {
                    // A pílula recebe o CENTRO: desce meia altura antes de desenhar e a outra
                    // metade depois. Foi exatamente aqui que ela subia por cima dos rótulos.
                    y += 28 + PilulaTituloAltura / 2f;
                    var texto = ano.Titulos == 1 ? "1 TÍTULO NO ANO" : $"{ano.Titulos} TÍTULOS NO ANO";
                    CartaoCompartilhavel.Pilula(canvas, texto, y, fontes, 40);
                    y += PilulaTituloAltura / 2f;
                }

                Destaques(canvas, fontes, ano, y);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            foto?.Dispose();
            logo?.Dispose();
        }
    }

    private static string Jogos(int quantos) => quantos == 1 ? "1 jogo" : $"{quantos} jogos";

    // ⚠️ Os quatro números são FIXOS de propósito. Jogos, vitórias e aproveitamento saem do que
    // a pessoa jogou; torneios é o único que aceita zero, porque "0 torneios" conta uma história
    // verdadeira e sem vergonha — você jogou o ano todo com a panelinha. Título, que é o número
    // que humilha quando é zero, virou seção opcional lá em cima.
    private static void Numeros(SKCanvas canvas, FonteDoCartao fontes, RetrospectivaVM ano)
    {
        var meio = CartaoCompartilhavel.Largura / 2f;
        const float coluna = 250;

        Caixa(canvas, fontes, meio - coluna, NumerosBase, ano.Jogos.ToString(), "JOGOS");
        Caixa(canvas, fontes, meio + coluna, NumerosBase, ano.Vitorias.ToString(), "VITÓRIAS");

        var segunda = NumerosBase + NumerosEntreLinhas;
        Caixa(canvas, fontes, meio - coluna, segunda, $"{ano.AproveitamentoPct}%", "APROVEITAMENTO");
        Caixa(canvas, fontes, meio + coluna, segunda, ano.Torneios.ToString(), "TORNEIOS");
    }

    // Parceiro do ano e clube do ano.
    //
    // ⚠️ LADO A LADO quando existem os dois, e centrado quando existe um só. Empilhados eles não
    // cabiam: com título, parceiro e clube ao mesmo tempo — que é o card de quem mais joga,
    // justamente quem mais posta — a última linha passava POR BAIXO do rodapé.
    private static void Destaques(SKCanvas canvas, FonteDoCartao fontes, RetrospectivaVM ano, float y)
    {
        (string Rotulo, string Nome, string Detalhe)? parceiro = ano.Parceiro == null ? null
            : ("PARCEIRO DO ANO", ano.Parceiro.Nome, $"{Jogos(ano.Parceiro.Jogos)} juntos");
        (string Rotulo, string Nome, string Detalhe)? clube = ano.Clube == null ? null
            : ("SUA QUADRA", ano.Clube.Nome, Jogos(ano.Clube.Jogos));

        var meio = CartaoCompartilhavel.Largura / 2f;
        y += 58;

        if (parceiro != null && clube != null)
        {
            const float coluna = 258;
            const float largura = 460;
            Destaque(canvas, fontes, parceiro.Value, meio - coluna, y, largura);
            Destaque(canvas, fontes, clube.Value, meio + coluna, y, largura);
            return;
        }

        var unico = parceiro ?? clube;
        if (unico != null)
        {
            Destaque(canvas, fontes, unico.Value, meio, y, CartaoCompartilhavel.Largura - 160);
        }
    }

    // Um número grande com o rótulo pequeno embaixo, centrado em `x`. `y` é a linha de base do
    // NÚMERO; o rótulo desce `RotuloAbaixoDoNumero`.
    private static void Caixa(
        SKCanvas canvas, FonteDoCartao fontes, float x, float y, string numero, string rotulo)
    {
        CartaoCompartilhavel.Texto(canvas, numero, x, y, fontes.Forte, 92,
            CartaoCompartilhavel.Branco, 420, tamanhoMinimo: 48);

        // O rótulo encolhe sozinho: "APROVEITAMENTO" é bem mais largo que "JOGOS" e num tamanho
        // fixo invadiria a coluna do lado.
        CartaoCompartilhavel.Texto(canvas, rotulo, x, y + RotuloAbaixoDoNumero, fontes.Media, 30,
            CartaoCompartilhavel.Apagado, 430, tamanhoMinimo: 20);
    }

    // "rótulo pequeno / nome grande / detalhe", centrado em `x`. `y` é a base do RÓTULO.
    private static void Destaque(
        SKCanvas canvas, FonteDoCartao fontes,
        (string Rotulo, string Nome, string Detalhe) bloco, float x, float y, float largura)
    {
        CartaoCompartilhavel.Texto(canvas, bloco.Rotulo, x, y, fontes.Media, 30,
            CartaoCompartilhavel.Apagado, largura, tamanhoMinimo: 20);

        CartaoCompartilhavel.Texto(canvas, bloco.Nome, x, y + DestaqueRotuloAteNome, fontes.Forte, 46,
            CartaoCompartilhavel.Branco, largura, tamanhoMinimo: 24);

        CartaoCompartilhavel.Texto(canvas, bloco.Detalhe,
            x, y + DestaqueRotuloAteNome + DestaqueNomeAteDetalhe, fontes.Normal, 28,
            CartaoCompartilhavel.LimeClaro, largura, tamanhoMinimo: 18);
    }
}
