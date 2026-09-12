namespace Padelizou.Services;

// A aprovação das chaves: entre o sorteio e a chave virar pública, alguém de confiança confere
// antes de soltar pros jogadores. Pedido do Felipe, 22/08/2026: "colocar uma tela antes de...
// dizer como liberar das chaves".
//
// O sorteio (GerarChaves) continua rodando e gravando tudo igual — grupos, jogos, horários.
// O que muda é o status que ele grava no fim: em vez de ir direto pra "Fase de Grupos" (que
// já é público), ele para aqui. Só quando alguém aprova (AprovarChaves) é que o torneio vira
// "Fase de Grupos" de verdade e o aviso "as chaves saíram" sai pros jogadores.
//
// Quem pode ver e aprovar é a MESMA régua de sempre pra gerir o torneio — organizador desta
// categoria, admin raiz ou admin nomeado (TorneiosController.EhOrganizadorAsync). Não é papel
// de acesso novo, só mais um portão na régua que já existe.
public static class AprovacaoDeChaves
{
    public const string Pendente = "Chaves em Aprovação";

    // O JOGO JÁ É PÚBLICO? — o predicado ÚNICO de todo leitor da grade que fala com jogador
    // (10/09/2026).
    //
    // 🗣️ Felipe: *"chegou a notificação de horarios para as pessoas, e nao poderia chegar, lembra
    // que pedi para nao chegar as notificações e nem nada até publicar?"* — e o print: a Home
    // dizendo "Seu próximo jogo: sáb. 12/09 às 08:00" com o torneio em "Chaves em Aprovação".
    //
    // 🕳️ O `AprovarChaves` era o único que AVISAVA, e por isso parecia fechado. Mas três leitores
    // entregavam o horário sem olhar a aprovação: a agenda/ICS (o calendário do celular notifica
    // evento novo — foi a notificação que chegou), o "próximo jogo" da Home e o push de quadra
    // atrasada. A mesma lição da aba Jogos, de horas antes: régua de visibilidade escrita num
    // lugar protege aquele lugar, não o dado. Daí uma expressão só, traduzível pro SQL, que cada
    // leitor põe no `Where`.
    //
    // Pelo caminho `Categoria.Torneio`, e não por `Partida.TorneioId`: este é nulo em partida
    // fora de torneio (jogo semanal), e a categoria é obrigatória.
    public static readonly System.Linq.Expressions.Expression<Func<Models.Partida, bool>> Publicada =
        p => p.Categoria.Torneio.Status != Pendente;

    // A CHAVE DESTE TORNEIO JÁ É PÚBLICA? — a irmã do predicado acima, do lado do TORNEIO.
    //
    // `Publicada` responde por PARTIDA e só sabe dizer "não está esperando aprovação" — o que é
    // verdade também num torneio com inscrições abertas, que não tem chave nenhuma. Quem
    // pergunta pela TELA precisa da outra metade: já existe chave sorteada E ela já saiu.
    //
    // A lista é a mesma que já decidia a aba "Chaves e Grupos" na view (e agora ela pergunta
    // aqui): "Mata-Mata" é histórico — nada no código grava esse status hoje —, e fica porque
    // torneio antigo de produção pode carregá-lo.
    public static bool ChavePublicada(Models.Torneio torneio) =>
        torneio.Status is "Fase de Grupos" or "Mata-Mata" or "Finalizado";

    // ── RECOLHER: o caminho de volta, `Fase de Grupos` → `Chaves em Aprovação` ──────────────
    //
    // 🗣️ Felipe, 10/09/2026: *"permita recolocar o torneio em fase fechada, ou já tem isso?"*
    //
    // Não tinha: depois da aprovação o status só andava pra frente. `DesfazerSorteio` fecha no
    // instante em que se aprova e APAGA grupos e jogos; `ReabrirInscricoes` recusa assim que
    // existe partida; `AlternarVisibilidade` some da listagem mas deixa quem já está inscrito
    // vendo a página. Faltava esconder MANTENDO o sorteio de pé.
    //
    // O encanamento já existia — `Publicada`, ali em cima, é o predicado único que a agenda/ICS,
    // a Home, o push de quadra atrasada e as abas consultam. Virar o status de volta re-esconde
    // tudo sozinho; o que faltava era só a transição.

    // A BOLA JÁ ROLOU NESTE JOGO? Une as duas leituras que já existiam, num lugar só: o
    // `DesfazerSorteio` pergunta `Status != "Agendada"` e o mural do torneio pergunta
    // `HorarioInicioReal != null` pra saber quem já entrou em quadra. Um jogo que começou sem
    // ninguém ter mexido no status passa lisa pela primeira e é pego pela segunda.
    public static readonly System.Linq.Expressions.Expression<Func<Models.Partida, bool>> JaSaiuDoPapel =
        p => p.Status != "Agendada" || p.HorarioInicioReal != null;

    // Devolve o motivo da recusa, ou null quando dá pra RECOLHER. Mesma forma de
    // `PortaDaInscricao.PorQueNaoPodeAbrir` e de `CancelamentoDoTorneio.MotivoParaNaoCancelar`:
    // quem chama mostra a frase, não inventa uma — a tela e o servidor dizem a mesma coisa.
    //
    // `jaSaiuDoPapel` vem de fora porque é consulta ao banco, pelo mesmo motivo que o `jaSorteou`
    // da porta da inscrição: propriedade calculada devolveria `false` calado em quem não trouxe a
    // navegação — e aqui isso esconderia um torneio com gente em quadra.
    public static string? PorQueNaoPodeRecolher(Models.Torneio torneio, bool jaSaiuDoPapel)
    {
        if (CancelamentoDoTorneio.EstaCancelado(torneio.Status))
            return "Este torneio está cancelado.";

        // ⚠️ O AMERICANO NUNCA PASSOU POR AQUI, então não há aprovação dele pra recolher:
        // `GerarRodadasAmericano` vai DIRETO pra "Fase de Grupos" — é a mesma verdade que
        // `PublicacaoDaChave.SaiPublicaNaHora` já conta na tela do sorteio, e é ela que responde
        // aqui pra não virar uma segunda régua sobre o mesmo fato.
        //
        // E não é purismo: em "Chaves em Aprovação" o painel oferece o "Desfazer e Sortear de
        // Novo" do formato Padrão, cujo `DesfazerSorteio` apaga GruposTorneio e Partidas sem
        // tratar a Dupla EFÊMERA do Americano individual (ver DesfazerRodadasAmericano, que
        // existe justamente porque as duplas se comportam diferente entre os dois formatos).
        if (PublicacaoDaChave.SaiPublicaNaHora(torneio))
            return "Neste formato a chave já sai pública no sorteio, sem passar por aprovação — "
                 + "não há aprovação pra recolher. Pra voltar atrás, use o \"Desfazer as rodadas\".";

        // "Fase de Grupos" é o status público do torneio em andamento (o mesmo que o Americano
        // usa). Fora dele não há chave publicada pra recolher: em "Chaves em Aprovação" ela já
        // está escondida, e em "Finalizado" o torneio acabou.
        if (torneio.Status != "Fase de Grupos")
            return "Este torneio não está com a chave publicada.";

        // ⚠️ A ÚNICA RECUSA, e é a que importa. Esconder um torneio EM ANDAMENTO não é
        // preferência do organizador — é jogador dentro do clube sem conseguir ver contra quem
        // joga e a que horas. Depois que a bola rolou, o caminho é corrigir com a chave no ar.
        if (jaSaiuDoPapel)
            return "Já tem jogo em andamento ou finalizado — recolher a chave agora esconderia "
                 + "um torneio em andamento de quem está jogando.";

        return null;
    }
}
