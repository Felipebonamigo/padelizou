using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Services;

namespace Padelizou.Tests;

// O porteiro da saída pros testes, num lugar só.
//
// Ele entrou como dependência dos TRÊS canais de mensagem (e-mail, WhatsApp e push), então
// aparece na montagem de vários testes que não têm nada a ver com ele. Uma fábrica com nome
// evita que cada um deles repita a construção — e, quando a assinatura mudar de novo, só esta
// linha muda.
public static class PorteiroDeTeste
{
    // SEM ARGUMENTO ele não restringe nada, que é o estado da PRODUÇÃO — e é o que quase todo
    // teste quer, porque quase todo teste está conferindo o que o sistema faz quando a mensagem
    // pode sair. Quem testa a restrição passa a lista na chamada.
    public static PorteiroDaSaida Com(params string[] soPara) =>
        new(Options.Create(new EntregaSettings { SoPara = soPara }), NullLogger<PorteiroDaSaida>.Instance);
}
