using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// PORTEIRO DESLIGADO NUM AMBIENTE DE TESTE TEM QUE GRITAR NO LOG.
//
// 🐛 O DEFEITO QUE ESTE ARQUIVO PRENDE (achado no servidor, 01/09/2026): o drop-in do systemd
// do dev tinha a linha
//
//     Environment=" Entrega__SoPara__2=almeidalucascoelho@gmail.com\
//
// com um espaço à esquerda e uma barra no fim. O processo subiu sem essa variável — conferido
// em /proc/<pid>/environ, que trazia só `__0` e `__1`. Ninguém foi avisado de nada.
//
// Naquele caso a lista continuou com dois destinos, então a falha foi FECHADA: o dev deixou de
// mandar mensagem pro Lucas e pronto. O perigo é o degrau seguinte — `Restringindo` é
// `_permitidos.Count > 0`, e **lista vazia quer dizer LIBERA TUDO**. É o padrão certo pra
// produção (que roda sem a chave de propósito) e o errado pro dev, que roda com cópia do banco
// de produção: uma malformação que apagasse as duas linhas restantes faria o ambiente de teste
// mandar e-mail, WhatsApp e push pra gente de verdade.
//
// O porteiro já avisava quando ESTÁ restringindo. O que faltava era o contrário, que é
// justamente o estado silencioso: config perdida não produz erro nenhum, só ausência.
public class PorteiroDesligadoNoDevGritaTests
{
    // Logger de bolso: `LogWarning` é método de extensão, então espião de mock exigiria casar
    // a assinatura genérica do `Log`. Quinze linhas aqui leem melhor do que isso.
    private sealed class LoggerQueAnota<T> : ILogger<T>
    {
        public List<string> Avisos { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? erro,
            Func<TState, Exception?, string> formatar)
        {
            if (nivel == LogLevel.Warning) Avisos.Add(formatar(estado, erro));
        }
    }

    private static BetaSettings Beta(bool ambienteDeTeste) => new() { AmbienteDeTeste = ambienteDeTeste };

    [Fact]
    public void Saida_sem_lista_num_ambiente_de_teste_avisa()
    {
        // O estado perigoso: o dev acha que está protegido e não está.
        var logger = new LoggerQueAnota<PorteiroDaSaida>();

        var porteiro = new PorteiroDaSaida(
            Options.Create(new EntregaSettings { SoPara = [] }),
            Options.Create(Beta(ambienteDeTeste: true)),
            logger);

        Assert.False(porteiro.Restringindo);
        Assert.Contains(logger.Avisos, a => a.Contains("SAÍDA LIBERADA"));
    }

    [Fact]
    public void Producao_sem_lista_NAO_avisa()
    {
        // Produção roda com a lista vazia de propósito, todo dia. Avisar aqui seria um Warning
        // permanente no log — e log que sempre grita é log que ninguém lê.
        var logger = new LoggerQueAnota<PorteiroDaSaida>();

        var porteiro = new PorteiroDaSaida(
            Options.Create(new EntregaSettings { SoPara = [] }),
            Options.Create(Beta(ambienteDeTeste: false)),
            logger);

        Assert.False(porteiro.Restringindo);
        Assert.Empty(logger.Avisos);
    }

    [Fact]
    public void Ambiente_de_teste_COM_lista_avisa_que_esta_restringindo()
    {
        // O aviso que já existia continua: ambiente que cala mensagem diz isso no start.
        var logger = new LoggerQueAnota<PorteiroDaSaida>();

        var porteiro = new PorteiroDaSaida(
            Options.Create(new EntregaSettings { SoPara = ["felipe.bonamigo@gmail.com"] }),
            Options.Create(Beta(ambienteDeTeste: true)),
            logger);

        Assert.True(porteiro.Restringindo);
        Assert.Contains(logger.Avisos, a => a.Contains("SAÍDA RESTRITA"));
    }

    [Fact]
    public void Entrada_sem_lista_num_ambiente_de_teste_avisa()
    {
        // O gêmeo: sem `Entrada:SoEstas`, qualquer conta do banco copiado da produção entra no
        // ambiente de teste.
        var logger = new LoggerQueAnota<PorteiroDaEntrada>();

        var porteiro = new PorteiroDaEntrada(
            Options.Create(new EntradaSettings { SoEstas = [] }),
            Options.Create(Beta(ambienteDeTeste: true)),
            logger);

        Assert.False(porteiro.Restringindo);
        Assert.Contains(logger.Avisos, a => a.Contains("ENTRADA LIBERADA"));
    }

    [Fact]
    public void Entrada_em_producao_sem_lista_NAO_avisa()
    {
        var logger = new LoggerQueAnota<PorteiroDaEntrada>();

        var porteiro = new PorteiroDaEntrada(
            Options.Create(new EntradaSettings { SoEstas = [] }),
            Options.Create(Beta(ambienteDeTeste: false)),
            logger);

        Assert.False(porteiro.Restringindo);
        Assert.Empty(logger.Avisos);
    }
}
