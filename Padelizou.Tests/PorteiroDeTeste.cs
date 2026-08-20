using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Services;

namespace Padelizou.Tests;

// Os porteiros do ambiente, prontos pros testes, num lugar só.
//
// Eles entraram como dependência de coisas muito usadas — os TRÊS canais de mensagem, o
// middleware do portão e o AuthController —, então aparecem na montagem de dezenas de testes
// que não têm nada a ver com ambiente restrito. Uma fábrica com nome evita que cada um deles
// repita a construção, e faz a próxima mudança de assinatura ser uma linha só.
public static class PorteiroDeTeste
{
    // Quem pode RECEBER mensagem. Sem argumento não restringe nada, que é o estado da
    // PRODUÇÃO — e é o que quase todo teste quer, porque quase todo teste está conferindo o
    // que o sistema faz quando a mensagem pode sair. Quem testa a restrição passa a lista.
    public static PorteiroDaSaida Saida(params string[] soPara) =>
        new(Options.Create(new EntregaSettings { SoPara = soPara }), NullLogger<PorteiroDaSaida>.Instance);

    // Quem pode ENTRAR no ambiente. Mesma regra: sem argumento, ninguém é barrado.
    public static PorteiroDaEntrada Entrada(params string[] soEstas) =>
        new(Options.Create(new EntradaSettings { SoEstas = soEstas }), NullLogger<PorteiroDaEntrada>.Instance);
}
