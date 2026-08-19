namespace Padelizou.Services;

// A CAMPANHA TAMBÉM MOVE O PADELÍMETRO — o ajuste que entra quando a final da categoria
// fecha. Espec em RANKING.md ("A campanha também move o número"); mudou a regra, muda LÁ
// primeiro.
//
// Por que existe (decisão do Felipe, 19/08/2026): o Elo puro cobra devagar demais de quem
// precisa DESCER. Quem sobe de categoria não pode mais jogar a anterior — e, apanhando da
// categoria nova, perde POUCO por derrota, porque a expectativa desconta o adversário mais
// forte. Subir às vezes leva anos; quem subia e não parava em pé ficava preso no andar de
// cima, sem número pra voltar. Ser campeão empurra pra cima; ficar na chave (ou cair na
// estreia do mata-mata) empurra pra baixo.
//
// ⚠️ AS PORTAS DA FAIXA LIMITAM O AJUSTE — é o que impede a régua de ser rígida (aviso do
// Felipe na mesma conversa: tem gente que passa ~20 torneios na mesma categoria, e isso é
// normal). Em todo torneio METADE do campo não sai da chave e só UM é campeão; uma pena
// solta drenaria o jogador mediano uma faixa inteira em 20 torneios sem ele ter piorado.
// Então: a pena leva ATÉ a linha de descida da faixa da categoria jogada (piso − 50, a
// folga da histerese) e para nela — dali pra baixo só derrota de verdade move; o bônus
// leva ATÉ a linha de subida (teto + 1) e para nela — farmar título de categoria fraca não
// infla. Quem joga PRA CIMA (nível abaixo da linha de descida da categoria) não leva pena:
// aventura não é campanha ruim.
//
// O tamanho é UM JOGO típico de propósito (K estável entre iguais ≈ ±10): "título vale um
// jogo ganho a mais, ficar na chave custa um jogo perdido a mais" é uma frase que qualquer
// jogador confere. Vice e fases do meio não levam nada — esses o Elo já pagou jogo a jogo.
//
// Puro como o Padelimetro: recebe fases e números, devolve delta e motivo. Quem decide
// QUANDO e PRA QUEM aplicar (final fechada, só quem entrou em quadra, porteiros de
// torneio) é o PadelimetroService.
public static class CampanhaNoPadelimetro
{
    public const int BonusDoCampeao = 10;

    // Ficou na fase de grupos enquanto a categoria seguiu adiante.
    public const int PenaDeNaoSairDaChave = -10;

    // Saiu da chave e caiu logo na estreia do mata-mata — metade da pena de quem nem saiu.
    public const int PenaDeCairNaEstreia = -5;

    // A fase de quem nunca foi eliminado no mata-mata: Dupla.UltimaFase nasce valendo isso
    // e jogo de grupo não carimba o perdedor (ver FinalizarPartida).
    public const string FicouNaChave = "Grupos";

    // A PRIMEIRA fase de mata-mata que a categoria DE FATO jogou — quem caiu nela caiu na
    // estreia. Vem das partidas que existem, e não do tamanho teórico do quadro: com bye a
    // fase de abertura é a que o robô gerou.
    public static string? PrimeiraFaseDoMataMata(IEnumerable<string?> fasesDasPartidas)
    {
        string? primeira = null;
        int melhor = int.MaxValue;
        foreach (var fase in fasesDasPartidas)
        {
            if (!ChaveamentoMataMata.EhFaseDeMataMata(fase)) continue;

            int ordem = DesfazerDoJogo.OrdemDaFase(fase);
            if (ordem >= 0 && ordem < melhor)
            {
                melhor = ordem;
                primeira = fase;
            }
        }
        return primeira;
    }

    // O ajuste de UM jogador, já limitado pelas portas da faixa, com o texto do extrato.
    // Nulo = a campanha não mexe neste número (vice, fase do meio, categoria sem faixa,
    // nível já na porta ou além dela).
    public static (int Delta, string Motivo)? Ajuste(int nivelAtual, string? ultimaFase,
        string? primeiraFaseDoMataMata, string? nomeCategoria)
    {
        // Sem mata-mata não houve chave pra sair — não há campanha pra cobrar.
        if (primeiraFaseDoMataMata == null) return null;

        // Mista, casal e nome fora da convenção não têm faixa: sem porta pra régua mirar,
        // sem ajuste (os JOGOS dessas categorias seguem movendo o número normalmente).
        var faixa = FaixasDePadelimetro.DaCategoria(nomeCategoria);
        if (faixa == null) return null;

        if (ultimaFase == CampeoesDoTorneio.FaseDeCampeao)
        {
            int linhaDeSubida = FaixasDePadelimetro.LinhaDeSubida(faixa);

            // Número já acima da categoria: nada a provar aqui — é a trava anti-farm.
            if (nivelAtual >= linhaDeSubida) return null;

            return (Math.Min(BonusDoCampeao, linhaDeSubida - nivelAtual),
                $"Campeão da {nomeCategoria} — bônus de campanha");
        }

        (int Valor, string Motivo)? pena =
            ultimaFase == FicouNaChave
                ? (PenaDeNaoSairDaChave, $"Não saiu da chave na {nomeCategoria} — ajuste de campanha")
            // Quando a estreia do mata-mata É a final (1 grupo, final direta), quem perdeu
            // é o VICE: chegou na final, não leva pena de estreia.
            : ultimaFase == primeiraFaseDoMataMata && primeiraFaseDoMataMata != "Final"
                ? (PenaDeCairNaEstreia, $"Caiu na estreia do mata-mata da {nomeCategoria} — ajuste de campanha")
            : null;
        if (pena == null) return null;

        int linhaDeDescida = FaixasDePadelimetro.LinhaDeDescida(faixa);

        // Na porta ou abaixo dela: quem está jogando PRA CIMA não é punido pela aventura,
        // e quem já chegou na linha de descida só desce por derrota de verdade.
        if (nivelAtual <= linhaDeDescida) return null;

        return (-Math.Min(-pena.Value.Valor, nivelAtual - linhaDeDescida), pena.Value.Motivo);
    }
}
