namespace Padel.Core;

public enum TipoDeEventoDaBola { Quique, Parede, Rede, CruzouRede, Saiu }
public enum QualParede { Lateral, Fundo }
public enum Superficie { Chao, Vidro, Grade }

/// <summary>Um acontecimento físico da bola. O árbitro é quem decide o que ele significa.</summary>
public readonly record struct EventoDaBola(TipoDeEventoDaBola Tipo, float X, float Y, float Z, int Lado, QualParede Parede = QualParede.Lateral, Superficie Superficie = Superficie.Chao)
{
    /// <summary>Em CruzouRede: pra que lado a bola foi.</summary>
    public int Para => Lado;
}

/// <summary>Velocidade inicial de um golpe, com o efeito (spin, em rad/s) que a raquete imprimiu.</summary>
public readonly record struct Velocidade(float Vx, float Vy, float Vz, float Tempo, float Wx = 0, float Wy = 0, float Wz = 0);

/// <summary>
/// Física da bola em 3D (x, y no chão; z pra cima), metros, segundos, quilos, rad/s — com os números
/// da bola de padel de verdade: gravidade, arrasto quadrático, efeito Magnus do spin, quique com
/// transferência de spin (topspin acelera, slice freia), vidro (elástico e liso) diferente de grade
/// (mata a bola), rede como obstáculo, saída por cima da parede.
/// Superfícies e coeficientes: valores de literatura de tênis/padel, marcados pra calibrar com vídeo.
/// </summary>
public sealed class Bola
{
    // Bola de padel (regra FIP): 56–59,4 g; 6,35–6,77 cm de diâmetro; quique de 135–145 cm ao cair de 2,54 m.
    public const float Massa = 0.057f;
    public const float Raio = 0.0335f;
    public const float G = 9.81f;
    private const float DensidadeDoAr = 1.2f;
    private const float CoeficienteDeArrasto = 0.55f;     // bola de feltro nesta faixa de velocidade
    private static readonly float Area = MathF.PI * Raio * Raio;
    /// <summary>Desaceleração por arrasto = KArrasto · v² (≈ 0,02 /m → uma bola a 30 m/s perde ~18 % em 10 m).</summary>
    public static readonly float KArrasto = 0.5f * DensidadeDoAr * CoeficienteDeArrasto * Area / Massa;
    private static readonly float KMagnus = 0.5f * DensidadeDoAr * Area / Massa;
    private const float MomentoDeInercia = 2f / 3f;         // casca esférica: I = (2/3) m R²

    // Superfícies: (restituição normal, atrito). Chão: no vácuo seria sqrt(1,40 / 2,54) ≈ 0,74; com o arrasto do ar
    // na descida e na subida, 0,775 é o que faz a bola solta de 2,54 m subir os 1,35–1,45 m da regra (há teste disso).
    public const float RestituicaoDoChao = 0.775f, AtritoDoChao = 0.6f;
    public const float RestituicaoDoVidro = 0.85f, AtritoDoVidro = 0.25f;   // vidro devolve mais e escorrega
    public const float RestituicaoDaGrade = 0.4f, AtritoDaGrade = 0.8f;     // grade mata a bola
    public const float RestituicaoDaRede = 0.2f;
    private const float DecaimentoDoSpinNoAr = 0.12f;       // fração por segundo
    public const float PassoMaximo = 1f / 240f;
    public const float VzMinimoParaQuicar = 0.7f;           // abaixo disso a bola para de quicar e rola

    public float X, Y, Z;
    public float Vx, Vy, Vz;
    /// <summary>Spin em rad/s. Topspin de uma bola indo em +y é −x; sidespin é o eixo z.</summary>
    public float Wx, Wy, Wz;
    public bool EmJogo;
    public bool Rolando;
    public bool Parada;

    public Bola() => Reiniciar();

    public void Reiniciar()
    {
        X = 0; Y = 0; Z = 1;
        Vx = 0; Vy = 0; Vz = 0;
        Wx = 0; Wy = 0; Wz = 0;
        EmJogo = false; Rolando = false; Parada = false;
    }

    public Bola Clonar() => (Bola)MemberwiseClone();

    public void Posicionar(float x, float y, float z)
    {
        X = x; Y = y; Z = z;
        Vx = 0; Vy = 0; Vz = 0;
        Wx = 0; Wy = 0; Wz = 0;
        Rolando = false; Parada = false;
    }

    public void Lancar(Velocidade v)
    {
        Vx = v.Vx; Vy = v.Vy; Vz = v.Vz;
        Wx = v.Wx; Wy = v.Wy; Wz = v.Wz;
        EmJogo = true; Rolando = false; Parada = false;
    }

    public float Rapidez => MathF.Sqrt(Vx * Vx + Vy * Vy + Vz * Vz);
    public float RapidezHorizontal => MathF.Sqrt(Vx * Vx + Vy * Vy);
    public float Spin => MathF.Sqrt(Wx * Wx + Wy * Wy + Wz * Wz);
    public float SpinRpm => Spin * 60f / (2 * MathF.PI);

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
            float freioDoSpin = MathF.Max(0, 1 - 3f * h);
            Wx *= freioDoSpin; Wy *= freioDoSpin; Wz *= freioDoSpin;
            if (MathF.Sqrt(Vx * Vx + Vy * Vy) < 0.05f) { Vx = 0; Vy = 0; Parada = true; return; }
        }
        else
        {
            Vz -= G * h;
            float v = Rapidez;
            if (v > 1e-4f)
            {
                // Arrasto quadrático: a = −K·v·v⃗.
                float f = KArrasto * v * h;
                Vx -= Vx * f; Vy -= Vy * f; Vz -= Vz * f;
                // Magnus: a = K·Cl·v·(ω̂ × v⃗), com Cl = 1 / (2 + v / (R·ω)) (Štěpánek).
                float w = Spin;
                if (w > 1f)
                {
                    float cl = 1f / (2f + v / (Raio * w));
                    float cx = Wy * Vz - Wz * Vy, cy = Wz * Vx - Wx * Vz, cz = Wx * Vy - Wy * Vx;
                    float k = KMagnus * cl * v / w * h;
                    Vx += cx * k; Vy += cy * k; Vz += cz * k;
                }
                float decaimento = 1 - DecaimentoDoSpinNoAr * h;
                Wx *= decaimento; Wy *= decaimento; Wz *= decaimento;
            }
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
                Wx *= 0.3f; Wy *= 0.3f; Wz *= 0.3f;
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
            Rebater(0, 0, 1, RestituicaoDoChao, AtritoDoChao);
            eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Quique, X, Y, 0, Quadra.LadoDe(Y)));
            if (Vz < VzMinimoParaQuicar) { Vz = 0; Rolando = true; }
        }

        // Paredes laterais: vidro até 3 m, grade de 3 a 4 m, acima disso a bola sai.
        if (MathF.Abs(X) > Quadra.MeiaLargura)
        {
            if (Z > Quadra.AlturaDaParede) { Sair(eventos); return; }
            float sinal = MathF.Sign(X);
            X = sinal * (Quadra.MeiaLargura - 0.001f);
            var superficie = RebaterNaParede(-sinal, 0);
            eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Parede, X, Y, Z, Quadra.LadoDe(Y), QualParede.Lateral, superficie));
        }
        // Paredes de fundo.
        if (MathF.Abs(Y) > Quadra.MeioComprimento)
        {
            if (Z > Quadra.AlturaDaParede) { Sair(eventos); return; }
            float sinal = MathF.Sign(Y);
            Y = sinal * (Quadra.MeioComprimento - 0.001f);
            var superficie = RebaterNaParede(0, -sinal);
            eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Parede, X, Y, Z, Quadra.LadoDe(Y), QualParede.Fundo, superficie));
        }
    }

    private Superficie RebaterNaParede(float nx, float ny)
    {
        bool grade = Z > Quadra.AlturaDoVidro;
        if (grade) Rebater(nx, ny, 0, RestituicaoDaGrade, AtritoDaGrade);
        else Rebater(nx, ny, 0, RestituicaoDoVidro, AtritoDoVidro);
        return grade ? Superficie.Grade : Superficie.Vidro;
    }

    /// <summary>
    /// Colisão com um plano de normal n (apontando pra dentro da quadra): restituição na normal e, na tangente,
    /// o impulso de atrito que leva o ponto de contato a rolar (limitado por μ vezes o impulso normal) — é o que
    /// transfere spin: topspin sai mais rápido e com mais spin, slice freia, sidespin desvia no vidro.
    /// </summary>
    private void Rebater(float nx, float ny, float nz, float restituicao, float atrito)
    {
        float vn = Vx * nx + Vy * ny + Vz * nz;
        if (vn >= 0) return;
        // Normal.
        float impulsoNormal = -(1 + restituicao) * vn;
        Vx += impulsoNormal * nx; Vy += impulsoNormal * ny; Vz += impulsoNormal * nz;
        // Velocidade do ponto de contato (em −R·n): v_t − R (ω × n).
        float vnDepois = Vx * nx + Vy * ny + Vz * nz;
        float tx = Vx - vnDepois * nx, ty = Vy - vnDepois * ny, tz = Vz - vnDepois * nz;
        float cx = Wy * nz - Wz * ny, cy = Wz * nx - Wx * nz, cz = Wx * ny - Wy * nx;
        float sx = tx - Raio * cx, sy = ty - Raio * cy, sz = tz - Raio * cz;
        float s = MathF.Sqrt(sx * sx + sy * sy + sz * sz);
        if (s < 1e-5f) return;
        // Impulso (por massa) pra rolar: Δv = −s·α/(1+α); limitado pelo atrito: μ·(1+e)·|vn|.
        float desejado = s * MomentoDeInercia / (1 + MomentoDeInercia);
        float maximo = atrito * impulsoNormal;
        float j = MathF.Min(desejado, maximo);
        float dvx = -sx / s * j, dvy = -sy / s * j, dvz = -sz / s * j;
        Vx += dvx; Vy += dvy; Vz += dvz;
        // Δω = −(1/(α·R)) (n × Δv).
        float k = -1f / (MomentoDeInercia * Raio);
        Wx += k * (ny * dvz - nz * dvy);
        Wy += k * (nz * dvx - nx * dvz);
        Wz += k * (nx * dvy - ny * dvx);
    }

    private void Sair(List<EventoDaBola> eventos)
    {
        EmJogo = false;
        eventos.Add(new EventoDaBola(TipoDeEventoDaBola.Saiu, X, Y, Z, Quadra.LadoDe(Y)));
    }
}

/// <summary>Efeito pedido num golpe, em rpm: topspin positivo (backspin/slice negativo) e sidespin (positivo curva pra esquerda de quem bate).</summary>
public readonly record struct Efeito(float TopspinRpm, float SidespinRpm)
{
    public static readonly Efeito Nenhum = default;
}

public static class Golpes
{
    private const float RpmParaRadPorSegundo = 2 * MathF.PI / 60f;

    /// <summary>
    /// Velocidade e spin pra levar a bola da origem ao alvo (no chão) em cerca de tempoDeVoo segundos, com a
    /// física de verdade (arrasto e Magnus): chute analítico no vácuo, depois simula e corrige o lançamento
    /// até cair a menos de 12 cm do alvo; se bate na rede, sobe o arco. ignorarRede pula a correção e deixa a
    /// bola ir baixa de propósito (é como se erra na rede ou no vidro).
    /// </summary>
    public static Velocidade Calcular(float x0, float y0, float z0, float alvoX, float alvoY, float tempoDeVoo = 0.9f, float folgaNaRede = 0.3f, bool ignorarRede = false, Efeito efeito = default)
    {
        float dx = alvoX - x0, dy = alvoY - y0;
        float distancia = MathF.Sqrt(dx * dx + dy * dy);
        float dirX = distancia > 1e-4f ? dx / distancia : 0, dirY = distancia > 1e-4f ? dy / distancia : 1;
        // Topspin de quem vai em d̂ é o eixo (up × d̂) = (−dy, dx, 0); sidespin é o eixo z.
        float wt = efeito.TopspinRpm * RpmParaRadPorSegundo, ws = efeito.SidespinRpm * RpmParaRadPorSegundo;
        float wx = -dirY * wt, wy = dirX * wt, wz = ws;

        float t = tempoDeVoo;
        var v = ChuteNoVacuo(x0, y0, z0, alvoX, alvoY, t, distancia, wx, wy, wz);
        if (ignorarRede) return v;

        var clone = new Bola();
        var eventos = new List<EventoDaBola>(8);
        for (int i = 0; i < 12; i++)
        {
            clone.Posicionar(x0, y0, z0);
            clone.Lancar(v);
            eventos.Clear();
            float tempoDeQueda = 0;
            bool bateuNaRede = false, caiu = false;
            float caiuX = 0, caiuY = 0;
            for (int passo = 0; passo < 4 * 60 && clone.EmJogo && !caiu && !bateuNaRede; passo++)
            {
                clone.Avancar(1f / 60f, eventos);
                tempoDeQueda += 1f / 60f;
                foreach (var e in eventos)
                {
                    if (e.Tipo == TipoDeEventoDaBola.Rede) { bateuNaRede = true; break; }
                    if (e.Tipo is TipoDeEventoDaBola.Quique or TipoDeEventoDaBola.Parede or TipoDeEventoDaBola.Saiu) { caiu = true; caiuX = e.X; caiuY = e.Y; break; }
                }
                eventos.Clear();
            }
            if (bateuNaRede)
            {
                // Arco mais alto: recomeça do chute no vácuo com mais tempo de voo; as iterações seguintes corrigem a distância.
                t *= 1.12f;
                v = ChuteNoVacuo(x0, y0, z0, alvoX, alvoY, t, distancia, wx, wy, wz);
                continue;
            }
            if (!caiu) break;
            float erroX = alvoX - caiuX, erroY = alvoY - caiuY;
            if (erroX * erroX + erroY * erroY < 0.12f * 0.12f) break;
            float tv = MathF.Max(0.2f, tempoDeQueda);
            v = v with { Vx = v.Vx + erroX / tv * 0.9f, Vy = v.Vy + erroY / tv * 0.9f };
        }
        return v;
    }

    private static Velocidade ChuteNoVacuo(float x0, float y0, float z0, float alvoX, float alvoY, float t, float distancia, float wx, float wy, float wz)
    {
        float compensacao = 1 + Bola.KArrasto * distancia * 0.5f;   // o arrasto quadrático encurta a bola
        float vx = (alvoX - x0) / t * compensacao;
        float vy = (alvoY - y0) / t * compensacao;
        float vz = (0.5f * Bola.G * t * t - z0) / t;
        return new Velocidade(vx, vy, vz, t, wx, wy, wz);
    }
}
