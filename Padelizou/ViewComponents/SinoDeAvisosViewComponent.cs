using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Security.Claims;

namespace Padelizou.ViewComponents;

// O sino do menu, com quantos avisos não lidos.
//
// O contador é o que faz a tela de Notificações existir de verdade: sem ele, ninguém abre uma
// caixa de entrada "por via das dúvidas" — a pessoa só entra quando o sistema avisa que tem
// coisa nova esperando.
//
// É um COUNT com índice em JogadorId (poucas linhas por pessoa) e só pra quem está logado.
// Mesma escolha do MenuClube: consulta, e não claim carimbada no login — aviso chega o tempo
// todo, e um número que só muda quando a pessoa sai e entra de novo estaria errado quase
// sempre. Se um dia a tabela crescer a ponto de o COUNT pesar, o índice composto com `LidaEm`
// é o próximo passo — hoje seria otimizar o que ninguém sente.
public class SinoDeAvisosViewComponent : ViewComponent
{
    // Acima disso o número vira "9+". Um "37" no sino não muda o que a pessoa vai fazer, e
    // ainda estoura a bolinha no celular.
    private const int Teto = 9;

    private readonly DbPadelContext _context;

    // Onde o número já foi contado nesta requisição. O sino aparece DUAS vezes na mesma
    // página — na barra (celular) e dentro do menu (desktop) — e é o mesmo número: sem isto
    // seriam dois COUNT em toda tela do site, só porque o CSS esconde um dos dois.
    private const string ChaveDaContagem = "pdz.avisos.nao-lidos";

    public SinoDeAvisosViewComponent(DbPadelContext context) => _context = context;

    // `naBarra`: o sino de fora do sanduíche, que é o que o celular enxerga. Muda só o
    // desenho — o número e a regra são os mesmos.
    public async Task<IViewComponentResult> InvokeAsync(bool naBarra = false)
    {
        var claim = (User as ClaimsPrincipal)?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var jogadorId)) return View(naBarra ? "Barra" : "Default", 0);

        int naoLidos = await ContarAsync(jogadorId);

        ViewBag.Teto = Teto;
        return View(naBarra ? "Barra" : "Default", naoLidos);
    }

    private async Task<int> ContarAsync(int jogadorId)
    {
        if (HttpContext.Items.TryGetValue(ChaveDaContagem, out var jaContado) && jaContado is int n)
        {
            return n;
        }

        int naoLidos = await _context.AvisosDoJogador
            .CountAsync(a => a.JogadorId == jogadorId && a.LidaEm == null);

        HttpContext.Items[ChaveDaContagem] = naoLidos;
        return naoLidos;
    }
}
