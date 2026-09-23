using Padelizou.Models;

namespace Padelizou.Services;

// QUEM NÃO SUSTENTA O PLANO PARA DE AGENDAR — pedido do Felipe em 22/09/2026: *"ao vencer o
// teste, ele tem q bloquear marcar novas aulas e mexer nas coisas, fica apenas a visualização
// do que ja esta marcado"*.
//
// ⚠️ "PODE AGENDAR" NÃO É RÉGUA NOVA: é `PlanoDoProfessor.CondicoesDeAssinante` — em teste,
// assinante em dia ou cortesia. Com o Avulso fora de cartaz (22/09), ela virou a definição
// inteira, e o que mora AQUI é só o PRAZO entre perder o direito e a agenda fechar. Escrever
// uma segunda régua de quem-pode seria a cópia que um dia discorda da primeira — foi assim que
// a Mesa de Controle quebrou em 31/07.
//
// ⚠️ É UM EIXO À PARTE DA TAXA, e os dois prazos são diferentes de propósito. Os 7 dias de
// `DiasDeCarencia` decidem quanto a aula CUSTA; os 10 daqui decidem quando a agenda FECHA.
// Colar os dois faria a porta bater no mesmo instante em que a taxa sobe, e o Felipe pediu
// explicitamente um prazo pra quem já foi cliente: *"se ele ja pagou alguma vez, coloca o aviso
// de 10 dias"*.
public static class BloqueioDoProfessor
{
    // ⚠️ 10h, E ISSO NÃO É ESTÉTICA. O Felipe pediu aviso "1 hora antes", e o varredor só
    // entrega em hora civilizada (`LembreteDeInscricaoNaoPaga`: 9h às 21h). Com o bloqueio às
    // 10h, o aviso de 1 hora cai exatamente na `PrimeiraHora`. Qualquer hora menor torna esse
    // aviso impossível de entregar e faz o item 3 nascer quebrado.
    public const int HoraDoBloqueio = LembreteDeInscricaoNaoPaga.PrimeiraHora + 1;

    // O INSTANTE EM QUE A AGENDA FECHA. Nulo = não há bloqueio à vista (bloqueio dormente, ou
    // professor cujo relógio nunca começou).
    public static DateTime? BloqueiaEm(Jogador professor, PlanoProfessorSettings cfg) =>
        Prazo(professor, cfg, cfg.DiasAteOBloqueio);

    // A conta das duas datas, com o número de dias por parâmetro: bloqueio e cancelamento
    // partem do MESMO ponto e só diferem nisso.
    private static DateTime? Prazo(Jogador professor, PlanoProfessorSettings cfg, int dias)
    {
        // ⚠️ A TRAVA QUE IMPEDE O DEPLOY DE FECHAR A BASE INTEIRA NUM SEGUNDO. Sem ela, subir
        // isto bloquearia no primeiro tique todo professor dos baldes "Avulsos" e "Sem escolha"
        // do /Admin/Professores — gente que nunca foi avisada de nada, cujo vencimento é de
        // meses atrás. Mesmo raciocínio do `JanelaDoAvisoDeQueda`: vencimento velho não vira
        // cobrança nova. Nula = DORMENTE, e é assim que este código sobe sem mudar nada.
        if (cfg.BloqueioAPartirDe is not DateTime estreia) return null;

        // O fim do último direito que ele teve. Vale o que durou MAIS: olhar só a cortesia
        // fecharia a agenda de quem tem mensalidade paga e viva por baixo dela.
        DateTime? ultimoDireito = Maior(
            professor.AssinaturaProfessorPagaAte,
            professor.CortesiaProfessorAte,
            PlanoDoProfessor.FimDoTeste(professor, cfg));

        // Sem relógio não há prazo: quem nunca abriu o plano (nem o painel de aulas) não tem
        // `TesteProfessorInicio`, e não dá pra bloquear alguém por um prazo que nunca correu.
        if (ultimoDireito is not DateTime fim) return null;

        // Quem já foi cliente de verdade ganha os 10 dias. Quem só usou o teste, não: o teste
        // já é a tolerância, e `PlanoDoProfessor` nunca deu carência a ele.
        //
        // ⚠️ `+1 DIA` NO CASO DO TESTE, e não a hora exata do fim. O teste de alguém que entrou
        // às 14h30 acaba às 14h30; bloquear às 10h DAQUELE dia fecharia a agenda de quem ainda
        // está em teste — `CondicoesDeAssinante` ainda diz sim naquela hora. O dia seguinte é o
        // primeiro instante em que as duas réguas concordam.
        var jaFoiCliente = professor.AssinaturaProfessorPagaAte != null
                        || professor.CortesiaConcedidaEm != null;

        var calculado = fim.Date.AddDays(jaFoiCliente ? dias : 1).AddHours(HoraDoBloqueio);

        // A estreia é PISO, nunca teto: quem vence depois dela segue o próprio relógio.
        var piso = estreia.Date.AddHours(HoraDoBloqueio);
        return calculado < piso ? piso : calculado;
    }

    // O INSTANTE EM QUE AS AULAS JÁ MARCADAS CAEM (item 4). Nulo pelos mesmos motivos do
    // BloqueiaEm — dormente, ou relógio que nunca começou.
    //
    // ⚠️ MORA AQUI, COLADO NO BLOQUEIO, e não no serviço que cancela: as duas datas saem do
    // MESMO ponto de partida (o fim do último direito), e a escada de avisos precisa das duas
    // pra saber quando parar de repetir. Uma segunda conta noutro arquivo é como elas passam a
    // discordar no primeiro refactor.
    public static DateTime? CancelaAulasEm(Jogador professor, PlanoProfessorSettings cfg) =>
        Prazo(professor, cfg, cfg.DiasAteCancelarAsAulas);

    // A agenda está fechada AGORA?
    public static bool EstaBloqueado(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        // ⚠️ ESTA LINHA PRIMEIRO, SEMPRE. É a invariante que mais importa do desenho: bloquear
        // quem está pagando é perder o cliente no dia em que ele honrou o combinado. Um
        // pagamento (ou uma cortesia) empurra `BloqueiaEm` pra frente sozinho — mas isto aqui
        // é o cinto que segura mesmo se um dia o cálculo errar.
        if (PlanoDoProfessor.CondicoesDeAssinante(professor, agora, cfg)) return false;

        return BloqueiaEm(professor, cfg) is DateTime quando && agora >= quando;
    }

    private static DateTime? Maior(params DateTime?[] datas)
    {
        DateTime? maior = null;
        foreach (var d in datas)
            if (d is DateTime v && (maior == null || v > maior)) maior = v;
        return maior;
    }
}
