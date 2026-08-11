using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // "Jogadores por região": de onde vem quem se cadastra, e em que ritmo cada lugar cresce.
    //
    // A tela de métricas responde QUANTOS entraram por dia/semana/mês; esta responde ONDE —
    // e as duas juntas respondem a pergunta que decide divulgação: "o grupo que eu mandei o
    // link em Canoas rendeu alguma coisa?". Sem o recorte por lugar, dez cadastros no mesmo
    // dia são dez cadastros, venham de uma cidade nova ou do mesmo condomínio de sempre.
    //
    // Toda a regra de agrupar (estado escrito de quatro jeitos, cidade de três, e quem não
    // escreveu a UF) mora em Services/JogadoresPorRegiao. Aqui só se busca e se fatia.
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> Regioes(string? por = null, string? agrupar = null, string? regiao = null)
        {
            // Tela de leitura, como a de Métricas: qualquer administrador entra, e o assistente
            // do sistema também (a trava dele é o verbo, e aqui não há gravação nenhuma).
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var agora = DateTime.Now;
            var agrupamento = FaixasDeMetricas.Normalizar(agrupar);
            var faixas = FaixasDeMetricas.Series(agrupamento, agora);

            // ⚠️ `ExcluidoEm == null`: apagar a conta apaga cidade e estado junto (ver
            // Services/ExclusaoDeConta). Quem saiu entraria em "não informado" e engordaria
            // justamente o buraco da medição, fingindo ser gente que esqueceu de preencher.
            //
            // A base inteira vem pra memória de propósito: as duas colunas são texto livre e
            // o agrupamento não vira SQL — é a mesma escolha já feita no filtro de cidade do
            // ranking. São três campos curtos por pessoa.
            var pessoas = await _context.Jogadores
                .Where(j => j.ExcluidoEm == null)
                .AsNoTracking()
                .Select(j => new PessoaNaRegiao(j.Cidade, j.Estado, j.CriadoEm))
                .ToListAsync();

            var vm = new RegioesAdminVM
            {
                Por = JogadoresPorRegiao.Normalizar(por),
                Agrupamento = agrupamento,
                TotalContado = pessoas.Count,
                HerdaramAUfDaCidade = JogadoresPorRegiao.QuantasHerdaramAUfDaCidade(pessoas),
                RotulosDasFaixas = faixas.Select(f => FaixasDeMetricas.Rotulo(agrupamento, f)).ToList(),
            };

            // As duas contagens do topo saem sempre das DUAS visões, não da que está aberta:
            // "12 cidades em 3 estados" é o tamanho da base, e não muda porque a tela está
            // mostrando uma coisa ou outra.
            var porEstado = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorEstado, pessoas);
            var porCidade = JogadoresPorRegiao.Agrupar(JogadoresPorRegiao.PorCidade, pessoas);

            vm.QuantosEstados = porEstado.Count(g => !g.NaoInformada);
            vm.QuantasCidades = porCidade.Count(g => !g.NaoInformada);
            vm.SemEstado = porEstado.Where(g => g.NaoInformada).Sum(g => g.Total);
            vm.SemCidade = porCidade.Where(g => g.NaoInformada).Sum(g => g.Total);

            var grupos = vm.Por == JogadoresPorRegiao.PorCidade ? porCidade : porEstado;

            foreach (var grupo in grupos)
            {
                var linha = new RegiaoNaTelaVM
                {
                    Rotulo = grupo.Rotulo,
                    Chave = grupo.Chave,
                    NaoInformada = grupo.NaoInformada,
                    Total = grupo.Total,
                    SemData = grupo.SemData,
                };

                foreach (var inicio in faixas)
                    linha.PorFaixa.Add(grupo.Entre(inicio, FaixasDeMetricas.ProximaFaixa(agrupamento, inicio)));

                vm.Regioes.Add(linha);
            }

            // Região que não existe (link velho, cidade que ninguém mais mora) abre a tela sem
            // detalhe nenhum, em vez de dar erro — é uma tela de leitura.
            vm.ChaveSelecionada = vm.Regioes.Any(r => r.Chave == regiao) ? regiao : null;

            return View(vm);
        }
    }
}
