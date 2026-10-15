namespace Padel.Core;

/// <summary>Um ponto do rastro: onde a bola estará e em quantos segundos a partir de agora.</summary>
public readonly record struct PontoDoRastro(float X, float Y, float Z, float T);

/// <summary>
/// Os pontos, um por passo (o i-ésimo é a bola ao fim do passo i + 1), e o índice do primeiro depois da batida no vidro
/// do lado de quem recebe, já quicada — o começo da saída de parede; -1 se ela não vem pela frente.
/// </summary>
public sealed record Rastro(IReadOnlyList<PontoDoRastro> Pontos, int PrimeiroDepoisDaParede);

/// <summary>
/// O rastro-fantasma da saída de parede (pilar 1 da fatia no Unreal: "a parede é a estrela"): por onde a bola vai se
/// ninguém bater nela, até deixar de ter jogo do lado <c>lado</c> (o de quem recebe). É a física da partida rodada
/// adiante numa cópia da bola, no passo da partida — então o rastro é, bit a bit, o caminho da bola (RastroFantasmaTests).
/// <para>
/// Quem desenha escolhe o trecho e a hora pelo nível de assistência do design: no Casual, o trecho a partir de
/// <see cref="Rastro.PrimeiroDepoisDaParede"/> assim que a bola vem; no Padrão, o rastro pedido no passo do evento Parede
/// (aí ele inteiro é a saída); no Pro, nada.
/// </para>
/// </summary>
public static class RastroFantasma
{
    /// <summary>O passo da partida (Partida.Avancar a 120 Hz).</summary>
    public const float Passo = 1f / 120f;
    /// <summary>3 s: mais que isso, nenhuma bola segue em jogo sem alguém bater.</summary>
    public const int PassosMaximos = 360;

    /// <summary>
    /// O rastro de agora até: o segundo quique no <paramref name="lado"/> (contando o que já houve, <paramref name="jaQuicou"/>);
    /// o vidro desse lado antes do quique (bola morta, não é saída); a bola quicar ou bater na parede do outro lado, cruzar
    /// a rede de volta, sair, parar ou rolar; ou <see cref="PassosMaximos"/>. O ponto do passo que encerra entra.
    /// </summary>
    public static Rastro Prever(Bola bola, int lado, bool jaQuicou)
    {
        var pontos = new List<PontoDoRastro>();
        if (!bola.EmJogo || bola.Parada || bola.Rolando) return new Rastro(pontos, -1);
        var b = bola.Clonar();
        var eventos = new List<EventoDaBola>(4);
        int quiques = jaQuicou ? 1 : 0;
        int primeiroDepoisDaParede = -1;
        float t = 0;
        for (int i = 0; i < PassosMaximos; i++)
        {
            eventos.Clear();
            b.Avancar(Passo, eventos);
            t += Passo;
            pontos.Add(new PontoDoRastro(b.X, b.Y, b.Z, t));
            bool acabou = false;
            foreach (var e in eventos)
            {
                switch (e.Tipo)
                {
                    case TipoDeEventoDaBola.Quique:
                        if (e.Lado != lado || ++quiques >= 2) acabou = true;
                        break;
                    case TipoDeEventoDaBola.Parede:
                        if (e.Lado != lado || quiques == 0) acabou = true;
                        else if (primeiroDepoisDaParede < 0) primeiroDepoisDaParede = i;
                        break;
                    case TipoDeEventoDaBola.CruzouRede:
                        if (e.Para != lado) acabou = true;
                        break;
                    case TipoDeEventoDaBola.Saiu:
                        acabou = true;
                        break;
                }
                if (acabou) break;   // o que vem depois no mesmo passo já é de bola morta (o vidro depois do 2º quique)
            }
            if (acabou || !b.EmJogo || b.Parada || b.Rolando) break;
        }
        return new Rastro(pontos, primeiroDepoisDaParede);
    }
}
