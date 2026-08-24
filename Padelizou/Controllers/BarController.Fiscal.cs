using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;

namespace Padelizou.Controllers;

// O cadastro fiscal do clube — CNPJ, inscrições, regime e endereço da nota.
//
// Ainda não emite nada, e é assim de propósito: emissão sem cadastro pronto é rejeição da
// SEFAZ com fila no balcão. Esta tela existe pra que, no dia em que o provedor entrar, a
// única coisa que falte seja o provedor.
//
// A tela responde a UMA pergunta, e ela é do dono do clube, não do contador: **falta o quê
// pra eu emitir nota?** Por isso o checklist é o herói aqui e o formulário é o coadjuvante.
public partial class BarController
{
    [HttpGet]
    public async Task<IActionResult> Fiscal(int id)
    {
        // Quem está na Gestão e clica em "Fiscal" vai pra tela do plano, onde o Fiscal está à
        // venda — um 403 seria fechar a porta na cara de quem quer pagar mais. E quem esbarra
        // no módulo EM CONSTRUÇÃO leva Forbid, porque ali não há nada pra vender ainda: quem
        // decide qual dos dois é o ModuloFiscal.
        var acesso = await _fiscal.AcessoAsync(id, UsuarioId());

        if (acesso == AcessoAoModulo.SemPlano)
            return RedirectToAction("Index", "PlanoClube", new { id });
        if (acesso != AcessoAoModulo.Liberado)
            return await RecusarAsync(id);

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        ViewBag.EmConstrucao = _fiscal.EmConstrucao;
        ViewBag.FaltaNfce = DadosFiscaisDoClube.FaltaParaEmitir(clube, DadosFiscaisDoClube.Nfce);
        ViewBag.FaltaNfse = DadosFiscaisDoClube.FaltaParaEmitir(clube, DadosFiscaisDoClube.Nfse);

        // Quantos produtos do cardápio já dariam pra sair numa nota. É o outro lado do
        // checklist: o clube pode ter CNPJ, certificado e tudo em ordem e mesmo assim não
        // vender nada, porque nenhum item do cardápio tem NCM.
        var produtos = await _context.ProdutosBar
            .Where(p => p.ClubeId == id && p.Ativo)
            .ToListAsync();

        ViewBag.ProdutosAtivos = produtos.Count;
        ViewBag.ProdutosProntos = produtos.Count(p =>
            FiscalDoProduto.FaltaParaEmitir(p.TipoFiscal, p.Ncm, p.Cfop, p.UnidadeComercial).Count == 0);

        // O medidor da franquia do mês. Enquanto não há provedor, o que ele mostra é VOLUME —
        // quantos documentos a operação geraria — e é esse número que decide se a cota do
        // plano está certa (ver Services/FranquiaFiscal).
        ViewBag.Franquia = await _franquia.DoMesAsync(id, DateTime.Now);
        ViewBag.EmissorConfigurado = _emissor.Configurado;

        return View(clube);
    }

    // A recusa desta tela — e ela tem DUAS caras, porque tem duas causas.
    //
    // Pra quem não manda no clube vale o Forbid() de sempre, que cai no /Auth/AcessoNegado do
    // site inteiro. Pra quem manda, o que faltou não foi permissão, foi a obra: esse recebe a
    // tela que diz isso, com 403 no cabeçalho (a porta está fechada de verdade) e corpo
    // escrito — sem corpo o UseStatusCodePages reexecutaria a página de erro por cima, como o
    // Program.cs já registra no caso do rate limiter.
    //
    // Um único lugar porque as duas ações desta tela recusam pela mesma razão, e duas cópias
    // de uma regra divergem.
    private async Task<IActionResult> RecusarAsync(int clubeId)
    {
        if (!await _fiscal.SoFaltaEntrarNoArAsync(clubeId, UsuarioId())) return Forbid();

        ViewBag.ClubeId = clubeId;
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("FiscalEmConstrucao");
    }

    [HttpPost]
    public async Task<IActionResult> SalvarFiscal(int clubeId, string? cnpj, string? razaoSocial,
        string? inscricaoEstadual, string? inscricaoMunicipal, int? regimeTributario,
        string? cep, string? logradouro, string? numeroEndereco, string? complemento, string? bairro)
    {
        if (!await _fiscal.PodeUsarAsync(clubeId, UsuarioId())) return await RecusarAsync(clubeId);

        var clube = await _context.Clubes.FindAsync(clubeId);
        if (clube == null) return NotFound();

        // O CNPJ chega mascarado da tela ("68.185.754/0001-05") e vai limpo pro banco — mesma
        // regra do CPF do jogador, pelo mesmo motivo: a máscara é do navegador e pode não rodar.
        var cnpjLimpo = Documentos.SomenteDigitosOuNulo(cnpj);
        var cepLimpo = Documentos.SomenteDigitosOuNulo(cep);

        if (DadosFiscaisDoClube.ProblemaComOsDados(cnpjLimpo, razaoSocial, regimeTributario, cepLimpo, null)
            is { } problema)
        {
            TempData["ErroBar"] = problema;
            return RedirectToAction(nameof(Fiscal), new { id = clubeId });
        }

        clube.Cnpj = cnpjLimpo;
        clube.RazaoSocial = VazioViraNulo(razaoSocial);
        clube.InscricaoEstadual = VazioViraNulo(inscricaoEstadual);
        clube.InscricaoMunicipal = VazioViraNulo(inscricaoMunicipal);
        clube.RegimeTributario = regimeTributario;
        clube.Cep = cepLimpo;
        clube.NumeroEndereco = VazioViraNulo(numeroEndereco);
        clube.Complemento = VazioViraNulo(complemento);

        // O CEP manda no endereço, como já manda no perfil do jogador (ver ConsultaDeCep): o
        // que o ViaCEP responde vale mais do que o digitado, porque é ele que traz o código
        // IBGE do município — o campo que a nota exige e que ninguém sabe de cabeça.
        //
        // ⚠️ Serviço fora do ar NÃO pode travar a gravação: o resto do cadastro é preenchido
        // do mesmo jeito e o IBGE fica pra próxima. Recusar aqui seria transformar uma queda
        // na casa dos outros em cadastro que ninguém consegue salvar.
        if (cepLimpo != null)
        {
            var consulta = await _cep.BuscarAsync(cepLimpo);
            if (consulta.Situacao == ResultadoDoCep.Achou && consulta.Endereco is { } achado)
            {
                clube.Logradouro = VazioViraNulo(logradouro) ?? achado.Logradouro;
                clube.Bairro = VazioViraNulo(bairro) ?? achado.Bairro;
                clube.Uf = achado.Uf;
                if (achado.Ibge != null) clube.MunicipioIbge = achado.Ibge;
            }
            else
            {
                clube.Logradouro = VazioViraNulo(logradouro);
                clube.Bairro = VazioViraNulo(bairro);
            }
        }
        else
        {
            clube.Logradouro = VazioViraNulo(logradouro);
            clube.Bairro = VazioViraNulo(bairro);
        }

        await _context.SaveChangesAsync();

        var falta = DadosFiscaisDoClube.FaltaParaEmitir(clube, DadosFiscaisDoClube.Nfce);
        TempData["SucessoBar"] = falta.Count == 0
            ? "Dados fiscais salvos — o cadastro do clube está completo."
            : $"Dados fiscais salvos. {DadosFiscaisDoClube.ResumoDoQueFalta(falta)}";

        return RedirectToAction(nameof(Fiscal), new { id = clubeId });
    }
}
