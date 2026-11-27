using Padelizou.Models;

namespace Padelizou.Services;

// FOLGA PROS MELHORES 1ºs, PELA CAMPANHA (27/11/2026).
//
// 🗣️ Rafael (Treta): "os escolhidos para ir direto pra semi são os melhores 1ºs, e não
// exatamente o A1". Desde 12/09/2026 o robô grava no sorteio o cruzamento POR COLOCAÇÃO
// (CruzamentoDoMataMata.Padrao), e a folga fica cravada por letra antes de qualquer jogo.
//
// Ligada, a categoria guarda esta MARCA no lugar do desenho (`Categoria.CruzamentoDoMataMata`,
// sem migration) e:
//  • o robô não grava desenho nenhum: espera TODOS os grupos fecharem e monta a abertura pelo
//    motor de campanha (ChaveamentoMataMata.MontarPrimeiraFase sem desenho — melhores pegam a
//    folga, semeadura sem repetir grupo);
//  • o congelamento da aprovação pula a categoria (ele só preenche campo NULO);
//  • a prévia avisa que as folgas saem da campanha.
//
// ⚠️ A marca não é um desenho: `CruzamentoDoMataMata.Ler` devolve nulo pra ela, e quem lê um
// desenho cai no padrão. Por isso quem MONTA a chave pergunta aqui antes.
public static class AberturaPelaCampanha
{
    public const string Marca = "campanha";

    public static bool Ligada(Categoria? categoria) =>
        string.Equals(categoria?.CruzamentoDoMataMata?.Trim(), Marca, StringComparison.OrdinalIgnoreCase);
}
