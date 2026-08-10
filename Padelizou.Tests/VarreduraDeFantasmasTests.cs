using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A varredura APAGA linha de banco a partir de uma resposta de rede. São dois jeitos de errar,
// e os dois são mudos:
//
//   * apagar demais — tira do canal alguém que estava perfeitamente alcançável, e a pessoa
//     simplesmente para de receber aviso sem nunca saber por quê;
//   * sondar demais — cada aparelho VIVO sondado recebe uma notificação que ninguém pediu
//     (as inscrições nascem com `userVisibleOnly`), e é desse clique de "desligar" que
//     ninguém volta.
//
// O caso real que originou tudo isso: em 10/08/2026 um registro Apple respondeu 2xx por ONZE
// DIAS depois de o app sair da tela de início. Por isso 2xx é INCONCLUSIVO aqui, nunca "vivo",
// e nunca autoriza apagar nada.
public class VarreduraDeFantasmasTests
{
    private class SondaFalsa : ISondaDePush
    {
        private readonly ResultadoDaSonda _resposta;
        public List<int> Sondados { get; } = new();

        public SondaFalsa(ResultadoDaSonda resposta) => _resposta = resposta;

        public Task<ResultadoDaSonda> SondarAsync(PushSubscriptionJogador registro)
        {
            Sondados.Add(registro.Id);
            return Task.FromResult(_resposta);
        }
    }

    private static PushSubscriptionJogador Registro(int id, int jogadorId, string host, DateTime criadoEm) =>
        new()
        {
            Id = id,
            JogadorId = jogadorId,
            Endpoint = $"https://{host}/fake/{id}",
            P256dh = "p",
            Auth = "a",
            CriadoEm = criadoEm,
        };

    private static DbPadelContext ContextoCom(params PushSubscriptionJogador[] registros)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.AddRange(registros);
        ctx.SaveChanges();
        return ctx;
    }

    // ⚠️ O teste mais importante do arquivo. 2xx foi exatamente o que o fantasma de 30/07
    // respondeu por onze dias — se ele autorizasse apagar, a varredura limparia registros vivos.
    [Fact]
    public async Task Registro_que_ainda_responde_2xx_NAO_e_apagado()
    {
        var ctx = ContextoCom(
            Registro(1, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 7, 30)),
            Registro(2, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 8, 10)));

        var relatorio = await new VarreduraDeFantasmas(ctx, new SondaFalsa(ResultadoDaSonda.Inconclusivo)).RodarAsync();

        Assert.Equal(1, relatorio.Suspeitos);
        Assert.Equal(0, relatorio.Apagados);
        Assert.Equal(1, relatorio.Inconclusivos);
        Assert.Equal(2, ctx.Set<PushSubscriptionJogador>().Count());
    }

    [Fact]
    public async Task Registro_que_o_servidor_de_push_confirma_morto_e_apagado()
    {
        var ctx = ContextoCom(
            Registro(1, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 7, 30)),
            Registro(2, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 8, 10)));

        var relatorio = await new VarreduraDeFantasmas(ctx, new SondaFalsa(ResultadoDaSonda.Morto)).RodarAsync();

        Assert.Equal(1, relatorio.Apagados);

        // Some o VELHO e fica o NOVO — trocar os dois seria apagar justamente o que funciona.
        var sobrou = Assert.Single(ctx.Set<PushSubscriptionJogador>());
        Assert.Equal(2, sobrou.Id);
    }

    // Erro de rede não é prova de morte. Sem esta guarda, um minuto de instabilidade viraria
    // uma limpeza silenciosa de gente alcançável.
    [Fact]
    public async Task Erro_de_envio_nao_apaga_nada()
    {
        var ctx = ContextoCom(
            Registro(1, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 7, 30)),
            Registro(2, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 8, 10)));

        var relatorio = await new VarreduraDeFantasmas(ctx, new SondaFalsa(ResultadoDaSonda.Erro)).RodarAsync();

        Assert.Equal(0, relatorio.Apagados);
        Assert.Equal(1, relatorio.ComErro);
        Assert.Equal(2, ctx.Set<PushSubscriptionJogador>().Count());
    }

    // Quem tem UM registro só não é suspeito de nada — e é a maioria da tabela. Sondar essa
    // gente é o cenário que faz todo aparelho vivo receber uma notificação do nada.
    [Fact]
    public async Task Quem_tem_um_registro_so_nunca_e_sondado()
    {
        var ctx = ContextoCom(
            Registro(1, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 7, 30)),
            Registro(2, jogadorId: 10, "web.push.apple.com", new DateTime(2026, 8, 4)),
            Registro(3, jogadorId: 11, "fcm.googleapis.com", new DateTime(2026, 8, 5)));

        var sonda = new SondaFalsa(ResultadoDaSonda.Morto);
        var relatorio = await new VarreduraDeFantasmas(ctx, sonda).RodarAsync();

        Assert.Empty(sonda.Sondados);
        Assert.Equal(0, relatorio.Suspeitos);
        Assert.Equal(3, ctx.Set<PushSubscriptionJogador>().Count());
    }

    // Celular e computador da mesma pessoa: dois registros, servidores de push DIFERENTES, os
    // dois vivos. Não é o padrão do fantasma, e sondar aqui incomodaria um aparelho que
    // funciona.
    [Fact]
    public async Task Dois_aparelhos_em_servidores_diferentes_nao_sao_suspeitos()
    {
        var ctx = ContextoCom(
            Registro(1, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 7, 30)),
            Registro(2, jogadorId: 9, "fcm.googleapis.com", new DateTime(2026, 8, 9)));

        var sonda = new SondaFalsa(ResultadoDaSonda.Morto);
        await new VarreduraDeFantasmas(ctx, sonda).RodarAsync();

        Assert.Empty(sonda.Sondados);
        Assert.Equal(2, ctx.Set<PushSubscriptionJogador>().Count());
    }

    // O registro mais NOVO é o que o aparelho acabou de criar. Ele nunca entra na sonda, mesmo
    // quando existem três da mesma pessoa no mesmo servidor.
    [Fact]
    public async Task O_registro_mais_novo_nunca_e_sondado()
    {
        var ctx = ContextoCom(
            Registro(1, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 7, 30)),
            Registro(2, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 8, 4)),
            Registro(3, jogadorId: 9, "web.push.apple.com", new DateTime(2026, 8, 10)));

        var sonda = new SondaFalsa(ResultadoDaSonda.Inconclusivo);
        await new VarreduraDeFantasmas(ctx, sonda).RodarAsync();

        Assert.Equal(new[] { 1, 2 }, sonda.Sondados);
        Assert.DoesNotContain(3, sonda.Sondados);
    }

    // A sonda é MUDA de propósito: o payload leva `tipo: "sonda"` e o sw.js devolve sem mostrar
    // nada. Se o combinado quebrar de um lado só, a varredura vira spam — e o defeito aparece
    // no celular dos outros, nunca aqui.
    [Fact]
    public void O_combinado_da_sonda_muda_existe_dos_dois_lados()
    {
        var servico = Arquivo(Path.Combine("Services", "VarreduraDeFantasmas.cs"));
        var sw = Arquivo(Path.Combine("wwwroot", "sw.js"));

        Assert.Contains("tipo = \"sonda\"", servico);
        Assert.True(sw.Contains("data.tipo === \"sonda\"") && sw.Contains("return"),
            "O sw.js não reconhece mais o payload de sonda. Sem isso, cada registro sondado "
            + "vira uma notificação no aparelho de alguém que não pediu nada.");
    }

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
