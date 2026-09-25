namespace Padel.Core.Rede;

public enum TipoDeMensagem : byte { Ola = 1, BemVindo = 2, Recusado = 3, Sala = 4, Comecou = 5, Entradas = 6, Instantaneo = 7 }

public enum MotivoDaRecusa : byte { VersaoIncompativel = 1, SalaCheia = 2, PartidaEmAndamento = 3 }

/// <summary>Cliente → host, canal confiável: quero entrar. Versao é a do protocolo do cliente (vai no cabeçalho).</summary>
public sealed record Ola(string Nome, byte Versao = Protocolo.Versao);

/// <summary>Host → cliente, confiável: entrou, é o jogador Indice, a partida é esta e a semente é esta.</summary>
public sealed record BemVindo(int Indice, OpcoesDaPartida Opcoes, uint Semente);

/// <summary>Host → cliente, confiável: não entrou, e por quê. VersaoDoHost pra dizer "atualize o jogo".</summary>
public sealed record Recusado(MotivoDaRecusa Motivo, byte VersaoDoHost = Protocolo.Versao);

/// <summary>Host → todos, confiável: quem está em cada vaga ("" = vaga livre, vira IA).</summary>
public sealed record Sala(string[] Nomes);

/// <summary>Host → todos, confiável: a partida começou; quem é humano em cada vaga e os nomes.</summary>
public sealed record Comecou(bool[] Humanos, string[] Nomes);

/// <summary>
/// Uma entrada de um tick, como vai pela rede. A direção vai quantizada (-127..127 = -1..1) e no referencial do
/// jogador. Os apertos vão como CONTADORES (mod 256), não como "apertou agora": se o pacote se perde, o próximo
/// traz o contador maior e o aperto chega; se o pacote se repete, o contador é o mesmo e o aperto não duplica.
/// </summary>
public readonly record struct EntradaDeRede(uint Seq, sbyte Dx, sbyte Dy, bool AcaoSegurada, byte ContadorDeAcao, byte ContadorDeLob)
{
    public float DirecaoX => Dx / 127f;
    public float DirecaoY => Dy / 127f;

    public static sbyte Quantizar(float valor)
    {
        if (float.IsNaN(valor)) return 0;
        return (sbyte)MathF.Round(Math.Clamp(valor, -1f, 1f) * 127f);
    }
}
