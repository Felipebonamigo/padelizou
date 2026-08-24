using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A franquia do plano Fiscal: o que está incluído na mensalidade e o que passa a ser cobrado.
//
// São regras de DINHEIRO, e cada uma delas é uma promessa escrita na tela do plano. Errar pra
// mais é cobrar do cliente o que foi prometido de graça; errar pra menos é pagar do bolso o
// crédito do provedor. Por isso as quatro regras têm teste próprio e nominal.
public class FranquiaFiscalTests
{
    private static readonly PlanoClubeSettings Cfg = new();
    private static readonly DateTime Marco = new(2026, 3, 10, 14, 0, 0);

    // ---------- As quatro regras, puras ----------

    [Fact]
    public void So_nota_autorizada_consome_franquia()
    {
        Assert.True(FranquiaFiscal.ConsomeFranquia(NotaFiscal.Autorizada));

        // Nenhum destes virou documento: não podem comer a cota do cliente.
        Assert.False(FranquiaFiscal.ConsomeFranquia(NotaFiscal.Pendente));
        Assert.False(FranquiaFiscal.ConsomeFranquia(NotaFiscal.Enviada));
        Assert.False(FranquiaFiscal.ConsomeFranquia(NotaFiscal.Manual));

        // ⚠️ Rejeitada CUSTOU crédito nosso e mesmo assim não consome franquia. O prejuízo da
        // rejeição fica do nosso lado de propósito — é o que nos obriga a validar o cadastro
        // antes de mandar, em vez de repassar o erro pro cliente.
        Assert.False(FranquiaFiscal.ConsomeFranquia(NotaFiscal.Rejeitada));
    }

    [Fact]
    public void Cancelamento_nao_devolve_franquia()
    {
        // A nota foi emitida, o documento existiu, o custo aconteceu. Devolver a cota seria
        // dar de graça um documento que já foi pago ao provedor.
        Assert.True(FranquiaFiscal.ConsomeFranquia(NotaFiscal.Cancelada));
    }

    [Fact]
    public void Os_dois_baldes_sao_separados_e_nao_se_compensam()
    {
        // Serviço estourado, cupom quase vazio. Se houvesse compensação, a sobra de 590 cupons
        // pagaria as 20 notas de serviço a mais — e não paga.
        var servico = new BaldeDaFranquia(NotaFiscal.Nfse, Cota: 150, Consumidas: 170, Volume: 170, 0.30m);
        var cupom = new BaldeDaFranquia(NotaFiscal.Nfce, Cota: 600, Consumidas: 10, Volume: 10, 0.15m);

        Assert.Equal(20, servico.Excedente);
        Assert.Equal(0, cupom.Excedente);

        var medida = new MedidaDaFranquia(Marco, servico, cupom);
        Assert.Equal(6m, medida.ValorDoExcedente);   // 20 × R$ 0,30, e nada abatido do cupom
        Assert.True(medida.TemExcedente);
    }

    [Fact]
    public void A_franquia_e_do_mes_e_nao_acumula()
    {
        // Não existe "sobrou de fevereiro". Cada mês nasce com a cota cheia, e o mês é
        // recortado pelo LimitesDoMes — fim aberto, pra não perder nem duplicar a nota do
        // último segundo.
        var (inicio, fim) = FranquiaFiscal.LimitesDoMes(new DateTime(2026, 3, 31, 23, 59, 59));

        Assert.Equal(new DateTime(2026, 3, 1), inicio);
        Assert.Equal(new DateTime(2026, 4, 1), fim);

        // O último segundo de março é de março; o primeiro instante de abril já não é.
        Assert.True(new DateTime(2026, 3, 31, 23, 59, 59) < fim);
        Assert.False(new DateTime(2026, 4, 1, 0, 0, 0) < fim);
    }

    [Fact]
    public void A_competencia_e_o_mes_da_venda_e_nao_o_da_resposta_da_SEFAZ()
    {
        // A venda das 23h50 de 31/01 cuja nota só autoriza depois da virada continua sendo de
        // janeiro: é a fatura de janeiro que o clube confere contra o próprio caixa.
        var nota = new NotaFiscal
        {
            CriadaEm = new DateTime(2026, 1, 31, 23, 50, 0),
            RespondidaEm = new DateTime(2026, 2, 1, 0, 5, 0),
            Status = NotaFiscal.Autorizada,
        };

        Assert.Equal(1, FranquiaFiscal.MesDeCompetencia(nota).Month);
    }

    // ---------- A conta ----------

    [Fact]
    public void Dentro_da_cota_nao_se_cobra_nada_a_mais()
    {
        var balde = new BaldeDaFranquia(NotaFiscal.Nfce, Cota: 600, Consumidas: 600, Volume: 600, 0.15m);

        // Exatamente na cota ainda está DENTRO — o excedente começa no 601º.
        Assert.Equal(0, balde.Excedente);
        Assert.Equal(0m, balde.ValorDoExcedente);
        Assert.Equal(0, balde.Restantes);
        Assert.Equal(100, balde.PercentualUsado);
    }

    [Fact]
    public void O_excedente_conta_so_o_que_passou()
    {
        var balde = new BaldeDaFranquia(NotaFiscal.Nfce, Cota: 600, Consumidas: 750, Volume: 750, 0.15m);

        Assert.Equal(150, balde.Excedente);
        Assert.Equal(22.5m, balde.ValorDoExcedente);
        Assert.Equal(FranquiaFiscal.Situacao.Estourou, balde.Situacao);

        // O percentual passa de 100 de propósito: a tela precisa dizer o tamanho do estouro.
        Assert.Equal(125, balde.PercentualUsado);
    }

    [Fact]
    public void O_aviso_vem_antes_de_estourar_e_nao_depois()
    {
        // Avisar só em 100% é avisar depois que a conta subiu.
        var tranquilo = new BaldeDaFranquia(NotaFiscal.Nfce, 600, Consumidas: 479, Volume: 479, 0.15m);
        var perto = new BaldeDaFranquia(NotaFiscal.Nfce, 600, Consumidas: 480, Volume: 480, 0.15m);

        Assert.Equal(FranquiaFiscal.Situacao.Tranquilo, tranquilo.Situacao);
        Assert.Equal(FranquiaFiscal.Situacao.Perto, perto.Situacao);   // 80% exatos

        // ⚠️ A LINHA QUE ESTE TESTE EXISTE PRA GUARDAR: 479/600 é 79,8%, que ARREDONDA pra 80.
        // A primeira versão decidia a situação em cima do percentual arredondado e avisava um
        // documento antes da hora. O número da tela e o número da decisão são outros.
        Assert.Equal(80, tranquilo.PercentualUsado);
        Assert.Equal(FranquiaFiscal.Situacao.Tranquilo, tranquilo.Situacao);
    }

    [Fact]
    public void A_pior_situacao_dos_dois_baldes_e_a_do_mes()
    {
        // Um balde estourado não pode ficar escondido atrás do outro que sobrou.
        var estourado = new BaldeDaFranquia(NotaFiscal.Nfse, 150, Consumidas: 200, Volume: 200, 0.30m);
        var folgado = new BaldeDaFranquia(NotaFiscal.Nfce, 600, Consumidas: 1, Volume: 1, 0.15m);

        Assert.Equal(FranquiaFiscal.Situacao.Estourou,
            new MedidaDaFranquia(Marco, estourado, folgado).Situacao);

        var perto = new BaldeDaFranquia(NotaFiscal.Nfse, 150, Consumidas: 130, Volume: 130, 0.30m);
        Assert.Equal(FranquiaFiscal.Situacao.Perto,
            new MedidaDaFranquia(Marco, perto, folgado).Situacao);
    }

    [Fact]
    public void Cota_zerada_nao_divide_por_zero()
    {
        // Cota vem de configuração e configuração pode chegar zerada. Uma tela que rebenta
        // porque alguém digitou 0 no appsettings é pior que a cota errada.
        var balde = new BaldeDaFranquia(NotaFiscal.Nfce, Cota: 0, Consumidas: 5, Volume: 5, 0.15m);

        Assert.Equal(0, balde.PercentualUsado);
        Assert.Equal(5, balde.Excedente);
    }

    // ---------- A leitura que serve HOJE: volume, não consumo ----------

    [Fact]
    public async Task Com_o_emissor_desligado_o_consumo_e_zero_e_o_volume_e_o_que_importa()
    {
        // ⚠️ Este é o teste do serviço tal como ele existe hoje. Nada é emitido, então a
        // franquia não é tocada — mas o volume mede o clube, que é o número que falta pra
        // fechar a cota do plano (FISCAL.md: confirmar com três meses de dados).
        using var ctx = TestInfra.NovoContexto();
        await SemearAsync(ctx, (NotaFiscal.Nfce, NotaFiscal.Pendente, 40),
                               (NotaFiscal.Nfse, NotaFiscal.Pendente, 12));

        var medida = await Medidor(ctx).DoMesAsync(1, Marco);

        Assert.Equal(0, medida.Cupom.Consumidas);
        Assert.Equal(0, medida.Servico.Consumidas);
        Assert.False(medida.TemExcedente);

        Assert.Equal(40, medida.Cupom.Volume);
        Assert.Equal(12, medida.Servico.Volume);
        Assert.Equal(52, medida.VolumeTotal);

        // E tudo isso está esperando emissão — é o que a tela chama de "ainda não virou nota".
        Assert.Equal(40, medida.Cupom.NaoEmitidas);
    }

    [Fact]
    public async Task O_medidor_separa_por_tipo_e_conta_o_que_consome()
    {
        using var ctx = TestInfra.NovoContexto();
        await SemearAsync(ctx,
            (NotaFiscal.Nfce, NotaFiscal.Autorizada, 610),   // consome, e estoura os 600
            (NotaFiscal.Nfce, NotaFiscal.Cancelada, 5),      // consome (não devolve)
            (NotaFiscal.Nfce, NotaFiscal.Rejeitada, 7),      // NÃO consome, mas é volume
            (NotaFiscal.Nfse, NotaFiscal.Autorizada, 3));

        var medida = await Medidor(ctx).DoMesAsync(1, Marco);

        Assert.Equal(615, medida.Cupom.Consumidas);
        Assert.Equal(622, medida.Cupom.Volume);
        Assert.Equal(15, medida.Cupom.Excedente);
        Assert.Equal(2.25m, medida.Cupom.ValorDoExcedente);

        Assert.Equal(3, medida.Servico.Consumidas);
        Assert.Equal(0, medida.Servico.Excedente);

        Assert.Equal(2.25m, medida.ValorDoExcedente);
    }

    [Fact]
    public async Task Mes_passado_nao_entra_na_conta_deste_mes()
    {
        using var ctx = TestInfra.NovoContexto();

        // Fevereiro estourado, março limpo. Se a franquia acumulasse — ou se o recorte de mês
        // estivesse errado — março nasceria devendo.
        ctx.NotasFiscais.AddRange(Enumerable.Range(0, 700).Select(i => new NotaFiscal
        {
            ClubeId = 1, Tipo = NotaFiscal.Nfce, Status = NotaFiscal.Autorizada,
            CriadaEm = new DateTime(2026, 2, 15, 10, 0, 0),
        }));
        await ctx.SaveChangesAsync();

        var marco = await Medidor(ctx).DoMesAsync(1, Marco);
        Assert.Equal(0, marco.Cupom.Consumidas);
        Assert.False(marco.TemExcedente);

        var fevereiro = await Medidor(ctx).DoMesAsync(1, new DateTime(2026, 2, 20));
        Assert.Equal(700, fevereiro.Cupom.Consumidas);
        Assert.Equal(100, fevereiro.Cupom.Excedente);
    }

    [Fact]
    public async Task A_franquia_e_de_um_clube_so()
    {
        // O clube vizinho estourando a cota dele não pode aparecer na fatura deste.
        using var ctx = TestInfra.NovoContexto();
        await SemearAsync(ctx, (NotaFiscal.Nfce, NotaFiscal.Autorizada, 10));

        ctx.NotasFiscais.AddRange(Enumerable.Range(0, 900).Select(i => new NotaFiscal
        {
            ClubeId = 2, Tipo = NotaFiscal.Nfce, Status = NotaFiscal.Autorizada, CriadaEm = Marco,
        }));
        await ctx.SaveChangesAsync();

        var medida = await Medidor(ctx).DoMesAsync(1, Marco);

        Assert.Equal(10, medida.Cupom.Consumidas);
        Assert.False(medida.TemExcedente);
    }

    [Fact]
    public async Task Clube_sem_movimento_nenhum_mede_zero_sem_quebrar()
    {
        using var ctx = TestInfra.NovoContexto();
        var medida = await Medidor(ctx).DoMesAsync(1, Marco);

        Assert.Equal(0, medida.VolumeTotal);
        Assert.Equal(FranquiaFiscal.Situacao.Tranquilo, medida.Situacao);
        Assert.Equal(Cfg.FranquiaNfceMensal, medida.Cupom.Restantes);
    }

    // ---------- A tabela e a tela contam a mesma história ----------

    [Fact]
    public void Os_numeros_da_franquia_vem_de_configuracao()
    {
        // Estavam escritos à mão dentro da tela do plano. A franquia é justamente o número
        // que vai mudar quando o piloto trouxer dados — não pode exigir republicar o site.
        Assert.Equal(150, Cfg.FranquiaNfseMensal);
        Assert.Equal(600, Cfg.FranquiaNfceMensal);
        Assert.Equal(0.30m, Cfg.ExcedenteNfse);
        Assert.Equal(0.15m, Cfg.ExcedenteNfce);

        // A régua contra o concorrente: 25% mais que os 100+500 do Gripo (FISCAL.md).
        Assert.True(Cfg.FranquiaNfseMensal >= 100 * 1.25m);
        Assert.True(Cfg.FranquiaNfceMensal >= 500 * 1.2m);
    }

    // ---------- Apoio ----------

    private static MedidorDeFranquia Medidor(DbPadelContext ctx) =>
        new(ctx, Options.Create(Cfg));

    private static async Task SemearAsync(DbPadelContext ctx,
        params (string Tipo, string Status, int Quantas)[] lotes)
    {
        foreach (var (tipo, status, quantas) in lotes)
        {
            ctx.NotasFiscais.AddRange(Enumerable.Range(0, quantas).Select(i => new NotaFiscal
            {
                ClubeId = 1,
                Tipo = tipo,
                Status = status,
                CriadaEm = Marco,
                Valor = 10m,
            }));
        }

        await ctx.SaveChangesAsync();
    }
}
