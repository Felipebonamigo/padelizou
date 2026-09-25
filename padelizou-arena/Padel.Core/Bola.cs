namespace Padel.Core;

public enum TipoDeEventoDaBola { Quique, Parede, Rede, CruzouRede, Saiu }
public enum QualParede { Lateral, Fundo }

/// <summary>Um acontecimento físico da bola. O árbitro é quem decide o que ele significa.</summary>
public readonly record struct EventoDaBola(TipoDeEventoDaBola Tipo, float X, float Y, float Z, int Lado, QualParede Parede = QualParede.Lateral)
{
    /// <summary>Em CruzouRede: pra que lado a bola foi.</summary>
    public int Para => Lado;
}

public readonly record struct Velocidade(float Vx, float Vy, float Vz, float Tempo);

/// <summary>
/// Física da bola em 3D (x, y no chão; z pra cima), metros e segundos: gravidade, arrasto leve,
/// quique, rebote nas quatro paredes, rede como obstáculo, saída por cima da parede.
/// </summary>
public sealed class Bola
{
    public const float G = 9.81f;
    public const float Arrasto = 0.04f;            // fração da velocidade perdida por segundo no ar
    public const float RestituicaoDoChao = 0.7f;   // bola de padel tem menos pressão que a de tênis
    public const float AtritoDoChao = 0.86f;
    public const float RestituicaoDaParede = 0.78f;
    public const float RestituicaoDaRede = 0.2f;
    public const float PassoMaximo = 1f / 240f;
    public const float VzMinimoParaQuicar = 0.7f;  // abaixo disso a bola para de quicar e rola

    public float X, Y, Z;
    public float Vx, Vy, Vz;
    public bool EmJogo;
    public bool Rolando;
    public bool Parada;

    public Bola() => Reiniciar();

    public void Reiniciar()
    {
        X = 0; Y = 0; Z = 1;
        Vx = 0; Vy = 0; Vz = 0;
        EmJogo = false; Rolando = false; Parada = false;
    }

    public Bola Clonar() => (Bola)MemberwiseClone();

    public void Posicionar(float x, float y, float z)
    {
        X = x; Y = y; Z = z;
        Vx = 0; Vy = 0; Vz = 0;
        Rolando = false; Parada = false;
    }

    public void Lancar(Velocidade v)
    {
        Vx = v.Vx; Vy = v.Vy; Vz = v.Vz;
        EmJogo = true; Rolando = false; Parada = false;
    }

    public float Rapidez => MathF.Sqrt(Vx * Vx + Vy * Vy + Vz * Vz);

    /// <summary>Avança dt segundos em sub-passos, acumulando os eventos do intervalo.</summary>
    public void Avancar(float dt, List<EventoDaBola> eventos)
    {
        if (!EmJogo || Parada) return;
        float restante = dt;
        while (restante > 1e-7f && EmJogo && !Parada)
        {
            float h = MathF.Min(PassoMaximo, restante);
            Passo(h, eventos);
            restante -= h;
        }
    }

    private void Passo(float h, List<EventoDaBola> eventos)
    {
        float yAntes = Y, zAntes = Z;
        if (Rolando)
        {
            float freio = MathF.Max(0, 1 - 2.5f * h);
            Vx *= freio; Vy *= freio; Vz = 0; Z = 0;
            if (MathF.Sqrt(Vx * Vx + Vy * Vy) < 0.05f) { Vx = 0; Vy = 0; Parada = true; return; }
        }
        else
        {
            Vz -= G * h;
            float ar = 1 - Arrasto * h;
            Vx *= ar; Vy *= ar; Vz *= ar;
        }
        X += Vx * h; Y += Vy * h; Z += Vz * h;

        // Rede: plano y = 0 até AlturaDaRede.
        if ((yAntes < 0 && Y >= 0) || (yAntes > 0 && Y <= 0))
        {
            float fracao = yAntes / (yAntes - Y);
            float zNaRede = zAntes + (Z - zAntes) * fracao;
            int veioDe = yAntes > 0 ? 1 : -1;
            if (zNaRede < Quadra.AlturaDaRede)
            {
                Y = veioDe * 0.03f;
                Vy = -Vy * RestituicaoDaRede;
                Vx *= 0.4f;
                Vz = MathF.Min(Vz, 0) * 0.3f;
                eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Rede, X, Y, Z, veioDe));
            }
            else
            {
                eventos.Add(new EventoDaBola(TipoDeEventoDaBola.CruzouRede, X, Y, zNaRede, -veioDe));
            }
        }

        // Chão.
        if (Z <= 0 && !Rolando)
        {
            Z = 0;
            Vz = -Vz * RestituicaoDoChao;
            Vx *= AtritoDoChao; Vy *= AtritoDoChao;
            eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Quique, X, Y, 0, Quadra.LadoDe(Y)));
            if (Vz < VzMinimoParaQuicar) { Vz = 0; Rolando = true; }
        }

        // Paredes laterais.
        if (MathF.Abs(X) > Quadra.MeiaLargura)
        {
            if (Z > Quadra.AlturaDaParede) { Sair(eventos); return; }
            X = MathF.Sign(X) * (Quadra.MeiaLargura - 0.001f);
            Vx = -Vx * RestituicaoDaParede;
            eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Parede, X, Y, Z, Quadra.LadoDe(Y), QualParede.Lateral));
        }
        // Paredes de fundo.
        if (MathF.Abs(Y) > Quadra.MeioComprimento)
        {
            if (Z > Quadra.AlturaDaParede) { Sair(eventos); return; }
            Y = MathF.Sign(Y) * (Quadra.MeioComprimento - 0.001f);
            Vy = -Vy * RestituicaoDaParede;
            eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Parede, X, Y, Z, Quadra.LadoDe(Y), QualParede.Fundo));
        }
    }

    private void Sair(List<EventoDaBola> eventos)
    {
        EmJogo = false;
        eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Saiu, X, Y, Z, Quadra.LadoDe(Y)));
    }
}

public static class Golpes
{
    /// <summary>
    /// Velocidade pra levar a bola da origem ao alvo (no chão) em tempoDeVoo segundos. Se a trajetória
    /// não passa a rede com folga, alonga o tempo (arco mais alto) até passar. ignorarRede deixa a
    /// bola ir baixa de propósito (é como se erra na rede).
    /// </summary>
    public static Velocidade Calcular(float x0, float y0, float z0, float alvoX, float alvoY, float tempoDeVoo = 0.9f, float folgaNaRede = 0.3f, bool ignorarRede = false)
    {
        float t = tempoDeVoo;
        Velocidade resultado = default;
        for (int i = 0; i < 16; i++)
        {
            float compensacao = 1 / (1 - Bola.Arrasto * t / 2);   // a bola perde velocidade no ar
            float vx = (alvoX - x0) / t * compensacao;
            float vy = (alvoY - y0) / t * compensacao;
            float vz = (0.5f * Bola.G * t * t - z0) / t;
            resultado = new Velocidade(vx, vy, vz, t);
            if (ignorarRede) break;
            bool cruza = MathF.Sign(alvoY) != MathF.Sign(y0) && y0 != 0;
            if (!cruza || vy == 0) break;
            float tRede = -y0 / vy;
            float zNaRede = z0 + vz * tRede - 0.5f * Bola.G * tRede * tRede;
            if (zNaRede >= Quadra.AlturaDaRede + folgaNaRede) break;
            t *= 1.1f;
        }
        return resultado;
    }
}
