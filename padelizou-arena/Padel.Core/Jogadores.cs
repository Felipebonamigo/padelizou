namespace Padel.Core;

public enum Dificuldade { Facil, Medio, Dificil }
public enum TipoDeGolpe { Saque, Normal, Ataque, Defesa, Lob, Smash, Erro }
public enum Formacao { Fundo, Rede }

/// <summary>Um golpe decidido: onde a bola deve cair e em quanto tempo.</summary>
public readonly record struct Golpe(float AlvoX, float AlvoY, float TempoDeVoo, TipoDeGolpe Tipo, bool IgnorarRede = false);

/// <summary>
/// Entrada de um humano, no referencial DELE: Dy &lt; 0 é "rumo à rede", Dx &gt; 0 é "minha direita".
/// A partida converte pro mundo pelo lado do jogador — assim o cliente de cima e o de baixo mandam a mesma coisa.
/// </summary>
public readonly record struct Entrada(float Dx, float Dy, bool AcaoPressionada, bool AcaoSegurada)
{
    public static readonly Entrada Vazia = default;
}

public sealed record PerfilDeIA(float Velocidade, float Reacao, float ErroDeMira, float ErroDePrevisao, float ChanceDeErro, float ChanceDeLob);

public static class Perfis
{
    public static readonly PerfilDeIA Facil = new(Velocidade: 4.2f, Reacao: 0.45f, ErroDeMira: 1.5f, ErroDePrevisao: 1.1f, ChanceDeErro: 0.14f, ChanceDeLob: 0.2f);
    public static readonly PerfilDeIA Medio = new(Velocidade: 5.2f, Reacao: 0.25f, ErroDeMira: 0.9f, ErroDePrevisao: 0.7f, ChanceDeErro: 0.07f, ChanceDeLob: 0.3f);
    public static readonly PerfilDeIA Dificil = new(Velocidade: 6.2f, Reacao: 0.10f, ErroDeMira: 0.5f, ErroDePrevisao: 0.35f, ChanceDeErro: 0.025f, ChanceDeLob: 0.35f);
    /// <summary>O parceiro do humano: confiável sem ser um muro.</summary>
    public static readonly PerfilDeIA Parceiro = new(Velocidade: 5.4f, Reacao: 0.18f, ErroDeMira: 0.8f, ErroDePrevisao: 0.55f, ChanceDeErro: 0.04f, ChanceDeLob: 0.3f);

    public static PerfilDeIA Por(Dificuldade d) => d switch
    {
        Dificuldade.Facil => Facil,
        Dificuldade.Dificil => Dificil,
        _ => Medio,
    };
}

public sealed class Jogador
{
    public const float VelocidadeDoHumano = 5.8f;

    public int Time { get; }
    /// <summary>0 = metade direita do time, 1 = esquerda.</summary>
    public int Indice { get; }
    public string Nome { get; }
    public bool Humano { get; set; }
    public float Velocidade { get; set; }
    public int Lado { get; }
    public float X, Y;
    public float Alcance { get; }
    public float AlturaMaxima { get; } = 2.7f;
    public float Cooldown;
    public int Golpes;

    public Jogador(int time, int indice, string nome, bool humano, float velocidade)
    {
        Time = time; Indice = indice; Nome = nome; Humano = humano; Velocidade = velocidade;
        Lado = Quadra.LadoDoTime(time);
        Alcance = humano ? 1.35f : 1.1f;   // braço + raquete; o humano ganha folga de propósito
        X = XDaMetade;
        Y = Lado * 6f;
    }

    public float XDaMetade => Lado * (Indice == 0 ? 2.5f : -2.5f);

    private void Limitar()
    {
        X = Util.Limitar(X, -4.7f, 4.7f);
        Y = Lado > 0 ? Util.Limitar(Y, 0.5f, 9.7f) : Util.Limitar(Y, -9.7f, -0.5f);
    }

    /// <summary>Direção no MUNDO (não no referencial do jogador).</summary>
    public void Mover(float dx, float dy, float dt)
    {
        float n = MathF.Sqrt(dx * dx + dy * dy);
        if (n < 1e-6f) return;
        float forca = MathF.Min(1, n);
        X += dx / n * forca * Velocidade * dt;
        Y += dy / n * forca * Velocidade * dt;
        Limitar();
    }

    public bool IrPara(float x, float y, float dt)
    {
        float dx = x - X, dy = y - Y;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 0.03f) return true;
        float passo = MathF.Min(d, Velocidade * dt);
        X += dx / d * passo;
        Y += dy / d * passo;
        Limitar();
        return d - passo < 0.03f;
    }

    public float DistanciaAte(float x, float y) => Util.Distancia(X, Y, x, y);

    public bool Alcanca(Bola bola) => DistanciaAte(bola.X, bola.Y) <= Alcance && bola.Z >= 0 && bola.Z <= AlturaMaxima;

    public void AvancarTempo(float dt) => Cooldown = MathF.Max(0, Cooldown - dt);

    /// <summary>0 = golpe confortável; cresce quando esticado, com a bola rente ao chão ou rápida.</summary>
    public float DificuldadeDoGolpe(Bola bola)
    {
        float esticado = DistanciaAte(bola.X, bola.Y) / Alcance;
        return Util.Limitar(esticado * 0.8f + (bola.Z < 0.3f ? 0.4f : 0) + (bola.Rapidez > 18 ? 0.4f : 0), 0, 1.6f);
    }
}

/// <summary>
/// Controla os jogadores de um time que não são humanos. Quando a bola muda de trajetória, simula a
/// física pra frente (mesma Bola, mesmo passo), decide quem busca e onde — com erro de leitura por
/// dificuldade, corrigido a cada quique/parede — e o outro cobre. Na hora de bater escolhe o alvo:
/// o buraco entre os adversários, lob quando eles estão na rede, smash em bola alta, erro forçado
/// quando esticada.
/// </summary>
public sealed class IA
{
    private sealed class Plano
    {
        public required Jogador Responsavel;
        public float AlvoX, AlvoY, AlvoT;
        public float CriadoEm;
    }

    private readonly record struct Candidato(float X, float Y, float Z, float T, bool Voleio);

    public int Time { get; }
    public PerfilDeIA Perfil { get; }
    public Formacao Posicao { get; private set; } = Formacao.Fundo;
    private readonly Aleatorio _aleatorio;
    private Plano? _plano;
    private float _relogio;

    public IA(int time, PerfilDeIA perfil, Aleatorio aleatorio)
    {
        Time = time; Perfil = perfil; _aleatorio = aleatorio;
    }

    /// <summary>Jogador que está indo buscar a bola, se houver (pra depuração e pra animação).</summary>
    public Jogador? Responsavel => _plano?.Responsavel;

    public void AoSacar(Partida partida, int timeSacador)
    {
        Posicao = timeSacador == Time ? Formacao.Rede : Formacao.Fundo;   // quem saca sobe; quem recebe fica atrás
        Planejar(partida, novaTrajetoria: true);
    }

    public void AoGolpear(Partida partida, Golpe golpe, Jogador jogador)
    {
        if (jogador.Time == Time)
        {
            bool naFrente = MathF.Abs(jogador.Y) < 5.5f;
            Posicao = golpe.Tipo is TipoDeGolpe.Lob or TipoDeGolpe.Smash || naFrente ? Formacao.Rede : Formacao.Fundo;
        }
        Planejar(partida, novaTrajetoria: true);
    }

    /// <summary>Bola quicou ou bateu na parede: releitura da trajetória, sem novo tempo de reação.</summary>
    public void AoRebater(Partida partida)
    {
        if (_plano is not null) Planejar(partida, novaTrajetoria: false);
    }

    private void Planejar(Partida partida, bool novaTrajetoria)
    {
        var bola = partida.Bola;
        var arbitro = partida.Arbitro;
        var planoAnterior = _plano;
        _plano = null;
        if (!bola.EmJogo || arbitro.Golpeador == Time) return;
        var candidatos = Prever(bola, arbitro);
        if (candidatos.Count == 0) return;
        var meus = partida.JogadoresDoTime(Time);

        Jogador? escolhido = null;
        Candidato alvo = default;
        foreach (var c in candidatos)
        {
            Jogador? melhor = null;
            float melhorFolga = float.NegativeInfinity;
            foreach (var j in meus)
            {
                float distancia = j.DistanciaAte(c.X, c.Y);
                if (c.Voleio && !(MathF.Abs(j.Y) < 5 && distancia < 2.5f)) continue;
                float folga = c.T - (distancia / j.Velocidade + (j.Humano ? 0 : Perfil.Reacao));
                if (folga >= 0 && folga > melhorFolga) { melhorFolga = folga; melhor = j; }
            }
            if (melhor is not null) { escolhido = melhor; alvo = c; break; }
        }
        if (escolhido is null)
        {
            // Ninguém chega a tempo: o mais perto do último ponto possível tenta mesmo assim.
            alvo = candidatos[^1];
            float menor = float.PositiveInfinity;
            foreach (var j in meus)
            {
                float d = j.DistanciaAte(alvo.X, alvo.Y);
                if (d < menor) { menor = d; escolhido = j; }
            }
        }

        // Erro de leitura: maior quanto mais longe (no tempo) a bola está; a releitura corrige.
        float sigma = Perfil.ErroDePrevisao * MathF.Min(1, alvo.T / 0.8f);
        int meuLado = Quadra.LadoDoTime(Time);
        _plano = new Plano
        {
            Responsavel = escolhido!,
            AlvoX = Util.Limitar(alvo.X + _aleatorio.Gaussiana() * sigma, -4.7f, 4.7f),
            AlvoY = meuLado * Util.Limitar(MathF.Abs(alvo.Y) + _aleatorio.Gaussiana() * sigma * 0.7f, 0.5f, 9.7f),
            AlvoT = alvo.T,
            CriadoEm = novaTrajetoria || planoAnterior is null ? _relogio : planoAnterior.CriadoEm,
        };
    }

    /// <summary>Pontos em que a bola estará batível no nosso lado, em ordem de tempo.</summary>
    private List<Candidato> Prever(Bola bola, Arbitro arbitro)
    {
        var clone = bola.Clonar();
        int meuLado = Quadra.LadoDoTime(Time);
        const float passo = 1f / 60f;
        var candidatos = new List<Candidato>();
        var eventos = new List<EventoDaBola>();
        int quiques = 0;
        float t = 0;
        bool acabou = false;
        for (int i = 0; i < 240 && clone.EmJogo && !clone.Parada && !acabou; i++)
        {
            eventos.Clear();
            clone.Avancar(passo, eventos);
            t += passo;
            foreach (var e in eventos)
            {
                if (e.Tipo != TipoDeEventoDaBola.Quique) continue;
                if (e.Lado == meuLado) quiques += 1; else acabou = true;
                if (quiques >= 2) acabou = true;
            }
            if (acabou) break;
            if (Quadra.LadoDe(clone.Y) != meuLado) continue;
            if (quiques == 0 && arbitro.EmSaque) continue;
            if (clone.Rolando) break;
            if (clone.Z < 0.35f || clone.Z > 2.3f) continue;
            if (quiques > 0 && clone.Vz > 0 && clone.Z < 0.9f) continue;   // subindo logo depois do quique: espera
            candidatos.Add(new Candidato(clone.X, clone.Y, clone.Z, t, Voleio: quiques == 0));
            if (candidatos.Count > 150) break;
        }
        return candidatos;
    }

    public void Reagir(float dt, Partida partida)
    {
        _relogio += dt;
        foreach (var j in partida.JogadoresDoTime(Time))
        {
            if (j.Humano) continue;
            var plano = _plano;
            if (plano is not null && plano.Responsavel == j)
            {
                if (_relogio - plano.CriadoEm < Perfil.Reacao) continue;
                j.IrPara(plano.AlvoX, plano.AlvoY, dt);
            }
            else
            {
                float profundidade = Posicao == Formacao.Rede ? 3.2f : 6.5f;
                j.IrPara(j.XDaMetade, j.Lado * profundidade, dt);
            }
        }
    }

    public Golpe EscolherGolpe(Jogador jogador, Bola bola, Partida partida)
    {
        var r = _aleatorio;
        int ladoDoAlvo = -jogador.Lado;
        var adversarios = partida.JogadoresDoTime(1 - Time);
        float dificuldade = jogador.DificuldadeDoGolpe(bola);

        if (r.Proximo() < Perfil.ChanceDeErro * (1 + 2.5f * dificuldade))
        {
            return r.Proximo() < 0.5f
                ? new Golpe((r.Proximo() - 0.5f) * 6, ladoDoAlvo * 0.3f, 0.45f, TipoDeGolpe.Erro, IgnorarRede: true)     // na rede
                : new Golpe((r.Proximo() - 0.5f) * 6, ladoDoAlvo * 11.5f, 0.55f, TipoDeGolpe.Erro, IgnorarRede: true);   // no vidro sem quicar
        }
        if (bola.Z > 1.6f && MathF.Abs(jogador.Y) < 6)
        {
            return new Golpe((r.Proximo() - 0.5f) * 7, ladoDoAlvo * (3 + r.Proximo() * 3), 0.42f, TipoDeGolpe.Smash);
        }
        int adversariosNaRede = adversarios.Count(a => MathF.Abs(a.Y) < 4.5f);
        if (adversariosNaRede >= 1 && r.Proximo() < Perfil.ChanceDeLob)
        {
            return new Golpe((r.Proximo() - 0.5f) * 5, ladoDoAlvo * 8.2f, 1.7f, TipoDeGolpe.Lob);
        }
        // Golpe de fundo pro buraco: o canto mais longe dos dois adversários.
        float melhorX = 0, melhorDistancia = -1;
        foreach (float x in new[] { -3.3f, 0f, 3.3f })
        {
            float d = adversarios.Min(a => a.DistanciaAte(x, ladoDoAlvo * 6.8f));
            if (d > melhorDistancia) { melhorDistancia = d; melhorX = x; }
        }
        float ruido = Perfil.ErroDeMira * (1 + 1.2f * dificuldade);
        float alvoX = Util.Limitar(melhorX + r.Gaussiana() * ruido, -4.5f, 4.5f);
        float alvoY = ladoDoAlvo * Util.Limitar(MathF.Abs(ladoDoAlvo * 6.8f + r.Gaussiana() * ruido * 0.6f), 2, 9.4f);
        return new Golpe(alvoX, alvoY, 0.85f, TipoDeGolpe.Normal);
    }
}
