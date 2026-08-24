using Microsoft.Extensions.Logging.Abstractions;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A camada fiscal antes de existir provedor: a interface, a fila e as regras de tentativa.
//
// Os testes cobrem as três decisões que custam dinheiro se estiverem erradas:
//   1. a venda NUNCA trava por causa da nota;
//   2. a mesma venda nunca vira duas notas;
//   3. rejeição não vira tentativa em laço — porque cada tentativa é um crédito pago.
public class FilaDeNotasTests
{
    private static readonly DateTime Agora = new(2026, 8, 24, 20, 0, 0);

    // ---------- O emissor desligado é o estado NORMAL de hoje ----------

    [Fact]
    public async Task Sem_provedor_contratado_nada_e_emitido_e_isso_e_de_proposito()
    {
        // É o que garante que ligar o cadastro fiscal em produção não dispare nota nenhuma
        // por engano — e o que permite MEDIR o volume de um clube piloto antes de assinar
        // contrato com provedor.
        var emissor = new EmissorFiscalDesligado();
        Assert.False(emissor.Configurado);

        var doc = new DocumentoParaEmitir(NotaFiscal.Nfce, new Clube { Nome = "Chakra" }, 50m,
            new List<ItemDoDocumento>());

        var resposta = await emissor.EmitirAsync(doc);
        Assert.False(resposta.Aceita);
        Assert.NotNull(resposta.Mensagem);
    }

    // ---------- Quando uma venda vira nota ----------

    [Fact]
    public void So_comanda_fechada_com_dinheiro_vira_nota()
    {
        var fechada = ComandaDe(Comanda.Fechada, 50m, BarDoClube.Dinheiro);
        Assert.True(FilaDeNotas.DeveEmitirPorComanda(fechada));

        // Comanda aberta ainda não é venda.
        Assert.False(FilaDeNotas.DeveEmitirPorComanda(ComandaDe(Comanda.Aberta, 50m, BarDoClube.Dinheiro)));
        Assert.False(FilaDeNotas.DeveEmitirPorComanda(ComandaDe(Comanda.Cancelada, 50m, BarDoClube.Dinheiro)));
    }

    [Fact]
    public void Cortesia_e_comanda_zerada_ficam_fora_porque_nota_de_zero_e_rejeicao_certa()
    {
        // Pagaríamos crédito pra ser recusados pela SEFAZ.
        Assert.False(FilaDeNotas.DeveEmitirPorComanda(ComandaDe(Comanda.Fechada, 50m, BarDoClube.Cortesia)));
        Assert.False(FilaDeNotas.DeveEmitirPorComanda(ComandaDe(Comanda.Fechada, 0m, BarDoClube.Dinheiro)));
    }

    // ---------- O teto de tentativas ----------

    [Fact]
    public void Rejeicao_definitiva_nao_tenta_de_novo_nem_uma_vez()
    {
        // NCM inexistente, CNPJ inválido: erro de cadastro não melhora repetindo, e cada
        // repetição é mais um crédito pago pra receber o mesmo "não".
        var definitiva = new RespostaDaEmissao(false, Mensagem: "NCM inexistente", ValeTentarDeNovo: false);
        Assert.Equal(NotaFiscal.Manual, FilaDeNotas.StatusDepoisDaResposta(definitiva, tentativasFeitas: 1));
    }

    [Fact]
    public void Rejeicao_temporaria_tenta_de_novo_ate_o_teto_e_para
        ()
    {
        var temporaria = new RespostaDaEmissao(false, Mensagem: "SEFAZ fora do ar");

        Assert.Equal(NotaFiscal.Rejeitada, FilaDeNotas.StatusDepoisDaResposta(temporaria, 1));
        Assert.Equal(NotaFiscal.Rejeitada, FilaDeNotas.StatusDepoisDaResposta(temporaria, 2));

        // Na terceira, vira problema de gente em vez de continuar queimando crédito.
        Assert.Equal(NotaFiscal.Manual, FilaDeNotas.StatusDepoisDaResposta(temporaria, 3));
        Assert.Equal(NotaFiscal.Manual, FilaDeNotas.StatusDepoisDaResposta(temporaria, 9));
    }

    [Fact]
    public void Aceita_pelo_provedor_fica_esperando_o_webhook()
    {
        // "Aceita" é só "recebi e vou processar" — a autorização chega depois, e é por isso
        // que não se fica perguntando (polling dobra o custo).
        var ok = new RespostaDaEmissao(true, IdNoProvedor: "abc-123");
        Assert.Equal(NotaFiscal.Enviada, FilaDeNotas.StatusDepoisDaResposta(ok, 1));
    }

    [Fact]
    public void A_espera_entre_tentativas_cresce_porque_a_sefaz_cai_por_minutos()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), FilaDeNotas.EsperaAntesDeRepetir(1));
        Assert.Equal(TimeSpan.FromMinutes(15), FilaDeNotas.EsperaAntesDeRepetir(2));
        Assert.Equal(TimeSpan.FromHours(1), FilaDeNotas.EsperaAntesDeRepetir(3));
    }

    [Fact]
    public void Nota_no_teto_de_tentativas_nao_sai_mais_da_fila_sozinha()
    {
        var nova = new NotaFiscal { Status = NotaFiscal.Pendente };
        Assert.True(FilaDeNotas.PodeEnviar(nova, Agora));

        // Já enviada, esperando webhook: não reenvia (seria nota duplicada).
        Assert.False(FilaDeNotas.PodeEnviar(new NotaFiscal { Status = NotaFiscal.Enviada }, Agora));
        Assert.False(FilaDeNotas.PodeEnviar(new NotaFiscal { Status = NotaFiscal.Autorizada }, Agora));
        Assert.False(FilaDeNotas.PodeEnviar(new NotaFiscal { Status = NotaFiscal.Manual }, Agora));

        // Rejeitada há pouco: espera a vez.
        var recente = new NotaFiscal
        {
            Status = NotaFiscal.Rejeitada, Tentativas = 1, EnviadaEm = Agora.AddSeconds(-30)
        };
        Assert.False(FilaDeNotas.PodeEnviar(recente, Agora));

        // Passada a espera, tenta.
        Assert.True(FilaDeNotas.PodeEnviar(recente, Agora.AddMinutes(3)));

        // No teto, nunca mais.
        var estourada = new NotaFiscal
        {
            Status = NotaFiscal.Rejeitada,
            Tentativas = FilaDeNotas.MaximoDeTentativas,
            EnviadaEm = Agora.AddDays(-1)
        };
        Assert.False(FilaDeNotas.PodeEnviar(estourada, Agora));
    }

    // ---------- Idempotência: a mesma venda nunca vira duas notas ----------

    [Fact]
    public async Task A_mesma_comanda_nunca_gera_duas_notas()
    {
        // Nota duplicada não é bug de tela: é problema fiscal no CNPJ do cliente. O balcão
        // clica duas vezes, a página recarrega, o webhook reenvia — nada disso pode duplicar.
        using var ctx = TestInfra.NovoContexto();
        var comanda = await SemearComandaAsync(ctx);
        var notas = new NotasDoClube(ctx, NullLogger<NotasDoClube>.Instance);

        var primeira = await notas.EnfileirarDaComandaAsync(comanda);
        var segunda = await notas.EnfileirarDaComandaAsync(comanda);

        Assert.NotNull(primeira);
        Assert.Equal(primeira!.Id, segunda!.Id);
        Assert.Single(ctx.NotasFiscais);
    }

    [Fact]
    public async Task A_nota_nasce_pendente_com_o_valor_e_o_CPF_congelados_da_venda()
    {
        // Congelados de propósito: a comanda pode ser corrigida depois, e a nota tem que
        // dizer o que foi emitido, não o que o cadastro virou.
        using var ctx = TestInfra.NovoContexto();
        var comanda = await SemearComandaAsync(ctx);
        var notas = new NotasDoClube(ctx, NullLogger<NotasDoClube>.Instance);

        var nota = await notas.EnfileirarDaComandaAsync(comanda);

        Assert.Equal(NotaFiscal.Pendente, nota!.Status);
        Assert.Equal(NotaFiscal.Nfce, nota.Tipo);
        Assert.Equal(comanda.Total, nota.Valor);
        Assert.Equal("11144477735", nota.CpfConsumidor);
        Assert.Equal(0, nota.Tentativas);
    }

    [Fact]
    public async Task Comanda_de_cortesia_nao_entra_na_fila()
    {
        using var ctx = TestInfra.NovoContexto();
        var comanda = await SemearComandaAsync(ctx, forma: BarDoClube.Cortesia);
        var notas = new NotasDoClube(ctx, NullLogger<NotasDoClube>.Instance);

        Assert.Null(await notas.EnfileirarDaComandaAsync(comanda));
        Assert.Empty(ctx.NotasFiscais);
    }

    [Fact]
    public async Task As_pendencias_do_clube_sao_so_o_que_precisa_de_gente()
    {
        using var ctx = TestInfra.NovoContexto();
        await SemearComandaAsync(ctx);

        ctx.NotasFiscais.AddRange(
            new NotaFiscal { ClubeId = 1, Status = NotaFiscal.Pendente },
            new NotaFiscal { ClubeId = 1, Status = NotaFiscal.Autorizada },
            new NotaFiscal { ClubeId = 1, Status = NotaFiscal.Rejeitada, Mensagem = "NCM inválido" },
            new NotaFiscal { ClubeId = 1, Status = NotaFiscal.Manual, Mensagem = "CNPJ recusado" },
            new NotaFiscal { ClubeId = 2, Status = NotaFiscal.Rejeitada });
        await ctx.SaveChangesAsync();

        var pendencias = await new NotasDoClube(ctx, NullLogger<NotasDoClube>.Instance).PendenciasAsync(1);

        // Pendente e autorizada não pedem nada de ninguém; a do outro clube não é problema deste.
        Assert.Equal(2, pendencias.Count);
        Assert.All(pendencias, n => Assert.True(n.PedeAtencao));
    }

    [Fact]
    public void A_tela_e_a_fila_contam_a_mesma_historia()
    {
        var manual = new NotaFiscal { Status = NotaFiscal.Manual, Mensagem = "CNPJ recusado" };
        Assert.Contains("Precisa de você", FilaDeNotas.Situacao(manual));
        Assert.Contains("CNPJ recusado", FilaDeNotas.Situacao(manual));

        var ok = new NotaFiscal { Status = NotaFiscal.Autorizada, Numero = "123" };
        Assert.Contains("123", FilaDeNotas.Situacao(ok));
        Assert.True(ok.EstaResolvida);
        Assert.False(ok.PedeAtencao);
    }

    // ---------- Apoio ----------

    private static Comanda ComandaDe(string status, decimal total, string forma)
    {
        var c = new Comanda { ClubeId = 1, Status = status, FormaPagamento = forma };
        if (total > 0) c.Itens.Add(new ItemComanda { PrecoUnitario = total, Quantidade = 1 });
        return c;
    }

    private static async Task<Comanda> SemearComandaAsync(DbPadelContext ctx, string? forma = null)
    {
        var dono = new Jogador { Id = 1, Nome = "Dono", Cpf = "1" };
        var clube = new Clube { Id = 1, Nome = "Chakra Padel", DonoId = 1 };

        var comanda = new Comanda
        {
            Id = 1, ClubeId = 1, Numero = 1, NomeCliente = "Rafael",
            DiaReferencia = Agora.Date, Status = Models.Comanda.Fechada,
            FormaPagamento = forma ?? BarDoClube.Dinheiro,
            CpfConsumidor = "11144477735",
        };
        comanda.Itens.Add(new ItemComanda { Id = 1, Descricao = "Heineken", PrecoUnitario = 12m, Quantidade = 2 });

        ctx.Jogadores.Add(dono);
        ctx.Clubes.Add(clube);
        ctx.Comandas.Add(comanda);
        await ctx.SaveChangesAsync();

        return comanda;
    }
}
