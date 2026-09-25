using Padel.Core.Rede;

namespace Padel.Core.Tests;

/// <summary>
/// A conexão que nunca se completa: ninguém hospedando no endereço, ou o host recusando no nível do transporte (ENet
/// com as vagas cheias). O transporte desiste e avisa Desconectou ANTES de qualquer Conectou — e o cliente tem que
/// sair do "Conectando", não esperar pra sempre (achado da revisão do Godot: o jogo ficava parado em "Conectando em …").
/// </summary>
public class RedeConexaoQueFalhaTests
{
    /// <summary>Um transporte roteirizado: entrega os eventos dados, um por Processar, e engole o que se envia.</summary>
    private sealed class TransporteRoteirizado(params EventoDoTransporte[] roteiro) : ITransporte
    {
        private readonly Queue<EventoDoTransporte> _roteiro = new(roteiro);
        private readonly Queue<EventoDoTransporte> _prontos = new();

        public void Enviar(int par, Canal canal, ReadOnlySpan<byte> dados) { }
        public void Desconectar(int par) { }
        public void Processar() { if (_roteiro.Count > 0) _prontos.Enqueue(_roteiro.Dequeue()); }
        public bool TentarReceber(out EventoDoTransporte evento) => _prontos.TryDequeue(out evento);
    }

    [Fact]
    public void Transporte_que_desiste_antes_do_aperto_de_mao_deixa_o_cliente_Desconectado()
    {
        var cliente = new ClienteDaPartida(new TransporteRoteirizado(new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, 1)), "Ana");
        Assert.Equal(FaseDoCliente.Conectando, cliente.Fase);
        cliente.Passo();
        Assert.Equal(FaseDoCliente.Desconectado, cliente.Fase);
    }

    [Fact]
    public void Transporte_que_conecta_e_cai_antes_da_resposta_deixa_o_cliente_Desconectado()
    {
        var cliente = new ClienteDaPartida(new TransporteRoteirizado(
            new EventoDoTransporte(TipoDeEventoDoTransporte.Conectou, 1),
            new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, 1)), "Ana");
        cliente.Passo();
        Assert.Equal(FaseDoCliente.AguardandoResposta, cliente.Fase);
        cliente.Passo();
        Assert.Equal(FaseDoCliente.Desconectado, cliente.Fase);
    }

    [Fact]
    public void Desconectou_de_outro_par_depois_de_conectar_nao_derruba_o_cliente()
    {
        // Guarda contra a correção larga demais: só o par do host (ou a falha antes de haver par) derruba.
        var cliente = new ClienteDaPartida(new TransporteRoteirizado(
            new EventoDoTransporte(TipoDeEventoDoTransporte.Conectou, 1),
            new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, 2)), "Ana");
        cliente.Passo();
        cliente.Passo();
        Assert.Equal(FaseDoCliente.AguardandoResposta, cliente.Fase);
    }
}
