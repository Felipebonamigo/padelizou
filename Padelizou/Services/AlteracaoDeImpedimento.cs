using Padelizou.Models;

namespace Padelizou.Services;

// Os quatro turnos como UMA escolha, e não como quatro booleanos soltos.
//
// O modelo guarda quatro `bool` por motivo histórico, mas a regra real é que só um deles pode
// ser true (ver Services/ImpedimentoUnico). Um enum é o formato honesto pra tela e pro POST:
// "qual turno?" tem uma resposta, e quatro caixas de seleção convidam a marcar duas.
public enum TurnoDoImpedimento
{
    Nenhum,
    QuintaNoite,
    SextaNoite,
    SabadoManha,
    SabadoTarde,

    // ---- A CONCENTRAÇÃO (08/09/2026) — o AVESSO dos quatro de cima ----
    //
    // 🗣️ Felipe: "criar uma opção, lá nos impedimentos, de 'colocar os 2 jogos na sexta' [...]
    // apenas para os organizadores e adm do sistema, para que nós possamos auxiliar algumas
    // pessoas".
    //
    // `SextaNoite` quer dizer "NÃO pode na sexta"; `SoSextaNoite` quer dizer "só na sexta". São
    // perguntas opostas, mas UMA escolha só — e é por isso que moram no mesmo enum: a tela tem
    // um `<select>` só, e a inscrição tem um turno só. Dois dropdowns lado a lado convidariam a
    // marcar impedimento e concentração ao mesmo tempo, que é justamente o estado impossível.
    //
    // ⚠️ ONDE CADA UMA MORA É DIFERENTE: o impedimento vira os quatro booleanos de
    // Models/Dupla; a concentração vira `Dupla.ConcentrarJogosEm` (Services/ConcentracaoDeJogos).
    // Marcar uma zera a outra — ver `Aplicar`.
    //
    // ⚠️ SÃO DO ORGANIZADOR, e a trava não é a tela: `MotivoParaNaoAlterar` recusa estes três
    // no caminho do jogador, porque um POST montado à mão chega com eles do mesmo jeito.
    SoSextaNoite,
    SoSabadoManha,
    SoSabadoTarde,
}

// Trocar o impedimento depois de já estar inscrito.
//
// 🗣️ Pedido do Felipe, 02/09/2026: "permita a pessoa alterar o impedimento, até o fechamento
// das inscrições". Até aqui o turno era escolhido uma vez, na inscrição, e não tinha volta —
// quem descobria o compromisso depois só resolvia falando com o organizador.
//
// ⚠️ ISTO ENCOSTA EM DINHEIRO, e é por isso que a régua veio dele, não de mim: o torneio cobra
// `TaxaPorImpedimento` por janela marcada, e o `ValorInscricao` é CONGELADO quando a inscrição
// nasce. A régua, nas palavras dele: "se já tem outro impedimento, mantém o mesmo custo; se
// não, avisa que é cobrado e o valor que é adicionado".
//
// Ela cai redonda porque a quantidade só vive em 0 ou 1: trocar é de graça, marcar cobra,
// tirar devolve ao valor devido.
public static class AlteracaoDeImpedimento
{
    // O turno que a inscrição tem HOJE. Um lugar só pra ler os quatro booleanos — espalhar
    // essa leitura é como as telas passam a discordar sobre a mesma dupla.
    public static TurnoDoImpedimento TurnoAtual(Dupla dupla)
    {
        // A concentração responde ANTES: ela e o impedimento são exclusivos (`Aplicar` zera um
        // ao gravar o outro), mas perguntar por ela primeiro deixa a exclusividade explícita em
        // vez de depender de os quatro booleanos estarem realmente limpos.
        if (dupla.ConcentrarJogosEm is TurnoDeConcentracao concentrada
            && concentrada != TurnoDeConcentracao.Nenhuma)
        {
            return concentrada switch
            {
                TurnoDeConcentracao.SextaNoite => TurnoDoImpedimento.SoSextaNoite,
                TurnoDeConcentracao.SabadoManha => TurnoDoImpedimento.SoSabadoManha,
                _ => TurnoDoImpedimento.SoSabadoTarde,
            };
        }

        if (dupla.ImpedimentoQuintaNoite) return TurnoDoImpedimento.QuintaNoite;
        if (dupla.ImpedimentoSextaNoite) return TurnoDoImpedimento.SextaNoite;
        if (dupla.ImpedimentoSabadoManha) return TurnoDoImpedimento.SabadoManha;
        if (dupla.ImpedimentoSabadoTarde) return TurnoDoImpedimento.SabadoTarde;
        return TurnoDoImpedimento.Nenhum;
    }

    // Este turno é uma concentração ("só jogo na sexta") e não um impedimento?
    public static bool EhConcentracao(TurnoDoImpedimento turno) =>
        turno is TurnoDoImpedimento.SoSextaNoite
              or TurnoDoImpedimento.SoSabadoManha
              or TurnoDoImpedimento.SoSabadoTarde;

    // O valor que vai pra `Dupla.ConcentrarJogosEm`. `Nenhuma` pra tudo que não é concentração.
    public static TurnoDeConcentracao Concentracao(TurnoDoImpedimento turno) => turno switch
    {
        TurnoDoImpedimento.SoSextaNoite => TurnoDeConcentracao.SextaNoite,
        TurnoDoImpedimento.SoSabadoManha => TurnoDeConcentracao.SabadoManha,
        TurnoDoImpedimento.SoSabadoTarde => TurnoDeConcentracao.SabadoTarde,
        _ => TurnoDeConcentracao.Nenhuma,
    };

    // Quantas TAXAS este turno custa: 0 ou 1.
    //
    // 💰 CONCENTRAÇÃO CUSTA ZERO — decisão do Felipe (08/09/2026): é favor do organizador, não
    // flexibilidade comprada. Contá-la como impedimento cobraria por um pedido que partiu dele,
    // e ainda por cima uma taxa só por três janelas tiradas da grade.
    private static int Taxas(TurnoDoImpedimento turno) =>
        turno == TurnoDoImpedimento.Nenhum || EhConcentracao(turno) ? 0 : 1;

    // Quanto o valor da inscrição MUDA se o turno virar `novo`. Positivo = a dupla passa a
    // dever mais; negativo = passa a dever menos; zero = troca, ou torneio que não cobra.
    //
    // ⚠️ Quem tinha impedimento PAGO e vira concentração passa a dever MENOS — o outro lado da
    // decisão de que ela é grátis. Nada estorna sozinho: quem acerta é o organizador, como já
    // era pra troca de impedimento (ver AlterarImpedimentoOrganizador).
    public static decimal QuantoMudaOValor(Dupla dupla, Torneio torneio, TurnoDoImpedimento novo) =>
        (Taxas(novo) - Taxas(TurnoAtual(dupla))) * torneio.TaxaPorImpedimento;

    // Devolve o motivo da recusa, ou null quando pode alterar.
    //
    // `novo` é opcional porque a tela pergunta duas coisas em momentos diferentes: "dá pra
    // mexer nesta inscrição?" (pra decidir se mostra o formulário) e "dá pra gravar ESTA
    // troca?" (no POST). Sem o turno, responde só a primeira.
    public static string? MotivoParaNaoAlterar(Dupla? dupla, Torneio? torneio, int quemPede,
        TurnoDoImpedimento? novo = null)
    {
        if (dupla == null || torneio == null) return "Não encontrei essa inscrição.";

        // ⚠️ FRONTEIRA DE CONFIANÇA (08/09/2026). "Os 2 jogos na sexta" é favor do organizador —
        // o Felipe pediu "apenas para os organizadores e adm do sistema". A tela do jogador não
        // oferece as três opções, mas tela não é trava: um POST montado à mão chega com elas do
        // mesmo jeito, e sem esta recusa qualquer inscrito se daria a concentração — e ainda
        // ABAIXARIA o próprio valor devido, já que ela é de graça.
        if (novo is { } pedido && EhConcentracao(pedido))
        {
            return "Concentrar os 2 jogos num turno só é coisa do organizador — fale com ele "
                 + "se você precisa de um horário assim.";
        }

        // Time não passa por aqui: o Jogador1Id dele é o organizador que o cadastrou, e sem
        // esta linha o organizador mexeria no impedimento de um time pela porta do jogador.
        if (dupla.EhTime) return "Times são gerenciados pelo organizador na tela de times.";

        if (dupla.Jogador1Id != quemPede && dupla.Jogador2Id != quemPede)
            return "Essa inscrição não é sua.";

        // O limite que o Felipe pediu, e ele tem motivo de grade: depois do sorteio os jogos
        // já estão marcados e a janela nova obrigaria a remontar tudo.
        if (torneio.Status != "Inscrições Abertas")
            return "As inscrições já foram encerradas — fale com o organizador pra mudar o impedimento.";

        // ⚠️ INSCRIÇÃO PAGA só aceita alteração que NÃO mexe no valor — ou seja, a troca de um
        // turno por outro, que é justamente a mais comum ("não posso mais na sexta, posso no
        // sábado"). Marcar um impedimento novo criaria cobrança extra; tirar criaria
        // devolução, e devolução aqui é o botão de estorno do organizador, na mão (ESTORNO.md).
        // Fingir que a tela resolve isso sozinha é como o dinheiro fica pendurado sem ninguém
        // saber.
        if (novo is { } turno && dupla.Pago && QuantoMudaOValor(dupla, torneio, turno) != 0)
        {
            return "Essa inscrição já está paga: mudar isso mexeria no valor. "
                 + "Fale com o organizador — ele acerta o pagamento e a mudança junto.";
        }

        return null;
    }

    // A VERSÃO DO ORGANIZADOR (aba Pagamentos, 07/09/2026): ele enxerga quem pediu impedimento
    // pra qual horário, e pode corrigir — não é `MotivoParaNaoAlterar` de novo porque a
    // pergunta é outra. Aquele pergunta "é MEU impedimento?"; aqui o organizador está mexendo
    // no de OUTRA pessoa, de propósito, então não há checagem de dono.
    //
    // ⚠️ O DINHEIRO TEM RÉGUA DIFERENTE, E FOI O FELIPE QUEM DECIDIU: ele PODE alterar mesmo
    // numa dupla já paga — a tela mostra quanto isso muda (`QuantoMudaOValor`), mas o ajuste
    // do dinheiro em si continua manual, do lado dele (marcar/desmarcar pago, ou o estorno na
    // mão de ESTORNO.md). Nada é cobrado nem estornado sozinho por esta função.
    public static string? MotivoParaOrganizadorNaoAlterar(Dupla? dupla, Torneio? torneio, bool jaSorteou)
    {
        if (dupla == null || torneio == null) return "Não encontrei essa inscrição.";

        // Time não tem impedimento de horário — ele joga conforme a grade que o organizador
        // monta, e as quatro janelas são coisa de PESSOA, não de time cadastrado por ele.
        if (dupla.EhTime) return "Times não têm impedimento de horário — eles jogam conforme a grade do organizador.";

        // ⚠️ A JANELA É `jaSorteou` (Partida existindo), NÃO "Inscrições Abertas" como no
        // jogador — mesma régua de TrocarCategoriaDupla/ReabrirInscricoes/DesfazerSorteio/
        // DesfazerRodadasAmericano. A grade já montada é o que quebraria; o organizador pode
        // corrigir mesmo com as inscrições já encerradas, que é o caso mais comum de precisar
        // disto (ele revisa DEPOIS de fechar, antes de sortear).
        if (jaSorteou)
            return "As chaves já foram sorteadas — mudar o impedimento agora bagunçaria a grade já montada.";

        return null;
    }

    // Grava a troca. Não decide nada: quem decide é o MotivoParaNaoAlterar, que o chamador já
    // consultou.
    public static void Aplicar(Dupla dupla, Torneio torneio, TurnoDoImpedimento novo,
        int quemAlterou, DateTime agora)
    {
        var diferenca = QuantoMudaOValor(dupla, torneio, novo);

        dupla.ImpedimentoQuintaNoite = novo == TurnoDoImpedimento.QuintaNoite;
        dupla.ImpedimentoSextaNoite = novo == TurnoDoImpedimento.SextaNoite;
        dupla.ImpedimentoSabadoManha = novo == TurnoDoImpedimento.SabadoManha;
        dupla.ImpedimentoSabadoTarde = novo == TurnoDoImpedimento.SabadoTarde;

        // ⚠️ EXCLUSIVIDADE, e ela sai de graça desta ordem: um `novo` de concentração deixa os
        // quatro booleanos acima em false, e um `novo` de impedimento cai no `null` daqui. Não
        // existe estado com os dois marcados, e nenhuma tela precisa saber disso.
        var concentracao = Concentracao(novo);
        dupla.ConcentrarJogosEm = concentracao == TurnoDeConcentracao.Nenhuma ? null : concentracao;

        // ⚠️ Nulo continua nulo. Inscrição anterior à coluna `ValorInscricao` não tem valor
        // congelado de propósito (ver Models/Dupla): inventar um número aqui seria adivinhar o
        // preço que valia no dia em que ela nasceu.
        if (dupla.ValorInscricao is decimal valor) dupla.ValorInscricao = valor + diferenca;

        // 🗣️ "deixe registrado quem marcou o impedimento". Sem isto, a dupla que chega no dia
        // reclamando do horário não tem como saber qual dos dois mexeu — e o organizador,
        // menos ainda.
        dupla.ImpedimentoAlteradoPorId = quemAlterou;
        dupla.ImpedimentoAlteradoEm = agora;
    }

    // O rótulo que a pessoa lê. Fica aqui, e não na view, porque a lista do sorteio e a tela
    // da inscrição precisam dizer a MESMA coisa sobre o mesmo turno.
    public static string Rotulo(TurnoDoImpedimento turno) => turno switch
    {
        TurnoDoImpedimento.QuintaNoite => "Quinta à noite",
        TurnoDoImpedimento.SextaNoite => "Sexta à noite",
        TurnoDoImpedimento.SabadoManha => "Sábado de manhã",
        TurnoDoImpedimento.SabadoTarde => "Sábado à tarde",
        // Delegado, não copiado: o rótulo da concentração já é escrito por quem manda nela, e
        // duas cópias do mesmo texto é como a aba e a lista passam a dizer coisas diferentes
        // sobre a mesma dupla.
        _ when EhConcentracao(turno) => ConcentracaoDeJogos.Rotulo(Concentracao(turno)),
        _ => "Sem impedimento",
    };
}
