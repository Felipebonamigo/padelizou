using Padelizou.Models;

namespace Padelizou.Services;

// Troca de horário entre dois jogos, decidida pelo organizador DEPOIS do sorteio.
//
// A grade automática acerta a conta (quadras, expediente, dias), mas não conhece a vida:
// a dupla que avisou que só chega às 10h, o jogo que interessa mais tarde pra ter público.
// Em vez de o organizador pedir "remarca pra mim" no WhatsApp, ele troca o jogo A com o
// jogo B — os dois horários continuam existindo, só mudam de dono, e a grade segue íntegra
// (nenhum buraco novo, nenhuma quadra em dobro).
//
// Troca-se HORÁRIO e QUADRA juntos: o par (hora, quadra) é o slot físico. Trocar só a hora
// deixaria dois jogos na mesma quadra na mesma hora.
public static class TrocaDeHorario
{
    // Null = pode trocar. Texto = o motivo, na língua de quem organiza.
    //
    // `sedes` é o torneio em mais de um clube: com ele, a troca recusa mandar a categoria que fica
    // em casa (a 3ª, a 4ª do Er) pro slot do local alugado — a grade automática não faria isso, e
    // a troca na mão também não. Sem `sedes` (chamador antigo, torneio de uma sede), nada muda.
    public static string? MotivoParaNaoTrocar(Partida? a, Partida? b, int torneioId, SedesDoTorneio? sedes = null)
    {
        if (a == null || b == null) return "Não encontrei um dos jogos.";

        // "O mesmo jogo" é a mesma INSTÂNCIA, ou o mesmo Id gravado. Jogo novo tem Id 0 — e o
        // reparo do sorteio (ReparoDaGrade, dentro do GerarChaves) troca jogos ANTES de gravar:
        // comparar só o Id dizia que todos eram o mesmo jogo, e o sorteio saía sem reparo nenhum
        // enquanto o Refazer, com os Ids, reparava (10/09/2026, ReparoDaGradeTests).
        if (ReferenceEquals(a, b) || (a.Id != 0 && a.Id == b.Id)) return "Escolha dois jogos diferentes.";
        if (a.TorneioId != torneioId || b.TorneioId != torneioId)
            return "Os dois jogos precisam ser deste torneio.";

        // Jogo em quadra ou já jogado tem placar acontecendo — o horário dele é história,
        // não agenda. Trocar aqui reescreveria o passado.
        if (a.Status != "Agendada") return $"O jogo {a.Codigo} já começou ou terminou — só se troca jogo agendado.";
        if (b.Status != "Agendada") return $"O jogo {b.Codigo} já começou ou terminou — só se troca jogo agendado.";

        if (a.HorarioPrevisto == null || b.HorarioPrevisto == null)
            return "Um dos jogos ainda está sem horário — não há o que trocar.";

        if (sedes != null && sedes.MaisDeUmClube)
            return NaoPodeIrPraVagaDe(a, b, sedes) ?? NaoPodeIrPraVagaDe(b, a, sedes);

        return null;
    }

    // `quem` pode tomar a vaga de `dono`? Mesmas duas réguas de GradeDeJogos.Encaixar: categoria
    // presa a um clube (`ClubeDaCategoria`) e categoria tirada do externo (`PodeIrPraSedeExtra`).
    // A régua mora na versão por LADO (abaixo), que serve ao jogo real e ao previsto; aqui só se
    // embrulha a partida.
    private static string? NaoPodeIrPraVagaDe(Partida quem, Partida dono, SedesDoTorneio sedes) =>
        NaoPodeIrPraVagaDe(
            new Lado(ReferenciaDoJogo.Real(quem.Id), quem, null),
            new Lado(ReferenciaDoJogo.Real(dono.Id), dono, null),
            sedes);

    // A troca em si: horário, quadra E CLUBE andam juntos — o slot físico é o trio. 🗣️ *"quando eu
    // trocar aqui, tem q cuidar para nao trocar o clube, por que o clube é pelo horario"* (Felipe,
    // 10/09/2026). No "por ordem" a quadra é nula e o clube carimbado é tudo que diz ONDE é o jogo;
    // trocar só hora e quadra deixava o jogo com a hora do Radar e o nome do Er Padel.
    //
    // ⚠️ A POSIÇÃO DENTRO DO HORÁRIO VAI JUNTO (10/09/2026, Services/OrdemNoHorario): quem toma o
    // slot do outro toma o lugar dele na linha, senão o jogo mudava de hora e reaparecia numa
    // posição que ninguém escolheu. E é isto que dá sentido à troca entre dois jogos do MESMO
    // horário — ali hora, quadra e clube são iguais dos dois lados, e a posição é a única coisa
    // que existe pra trocar. 🗣️ *"no mesmo horario, ele nao esta trocando a ordem na linha"*.
    public static void Trocar(Partida a, Partida b)
    {
        (a.HorarioPrevisto, b.HorarioPrevisto) = (b.HorarioPrevisto, a.HorarioPrevisto);
        (a.NomeQuadra, b.NomeQuadra) = (b.NomeQuadra, a.NomeQuadra);
        (a.ClubeId, b.ClubeId) = (b.ClubeId, a.ClubeId);
        (a.OrdemNoHorario, b.OrdemNoHorario) = (b.OrdemNoHorario, a.OrdemNoHorario);
    }

    // ═══ A TROCA COM UMA ELIMINATÓRIA QUE AINDA NÃO NASCEU (10/09/2026) ═══
    //
    // 🗣️ *"permita também trocar de horário as eliminatórias, não apenas as de chave"*. Um lado
    // da troca pode ser um jogo PREVISTO (ProximasFasesDaChave.JogoQueVem): ele não tem linha no
    // banco, então o slot que ele recebe vira uma RESERVA (Models/ReservaDeHorario), e é o robô
    // que a transforma em jogo quando a rodada nascer.

    // Um lado da troca: o jogo real OU o previsto, reduzido ao slot dele.
    public sealed record Lado(ReferenciaDoJogo Referencia, Partida? Real, ProximasFasesDaChave.JogoQueVem? Previsto)
    {
        public bool Existe => Real != null || Previsto != null;
        public DateTime? Horario => Real != null ? Real.HorarioPrevisto : Previsto?.Horario;
        public string? Quadra => Real != null ? Real.NomeQuadra : Previsto?.Quadra;
        public int? CategoriaId => Real != null ? Real.CategoriaId : Previsto?.CategoriaId;
        public string? NomeDaCategoria => Real != null ? Real.Categoria?.Nome : Previsto?.Categoria;

        // Como a mensagem chama o jogo: o real pelo código, como sempre; o previsto pela fase
        // numerada, que é como a tela o mostra ("4ª Masculina · Final").
        public string Rotulo => Real != null
            ? $"o jogo {Real.Codigo}"
            : Previsto != null ? $"{Previsto.Categoria} · {Previsto.FaseNumerada}" : "o jogo";

        // O clube do SLOT: o carimbo do jogo real (Partida.ClubeId, que o "por ordem" guarda depois
        // de apagar a quadra) ou, sem carimbo, o clube da quadra — do previsto só a quadra existe.
        public int? ClubeDaVaga(SedesDoTorneio sedes) =>
            (Real?.ClubeId) ?? sedes.ClubeDaQuadra(Quadra);
    }

    // Null = pode trocar. Texto = o motivo. As regras do jogo real são as mesmas de sempre (acima);
    // o previsto só precisa existir na prévia de agora e ter hora. Com `sedes`, a régua do clube
    // vale igual: a Final prevista da 3ª não recebe, por troca, o horário de um jogo no Radar.
    public static string? MotivoParaNaoTrocar(Lado a, Lado b, int torneioId, SedesDoTorneio? sedes = null)
    {
        if (!a.Existe || !b.Existe)
            return "Não encontrei um dos jogos — a prévia pode ter mudado desde que a página abriu. Recarregue e tente de novo.";
        if (a.Referencia == b.Referencia) return "Escolha dois jogos diferentes.";

        foreach (var real in new[] { a.Real, b.Real })
        {
            if (real == null) continue;
            if (real.TorneioId != torneioId) return "Os dois jogos precisam ser deste torneio.";
            if (real.Status != "Agendada") return $"O jogo {real.Codigo} já começou ou terminou — só se troca jogo agendado.";
        }

        if (a.Horario == null || b.Horario == null)
            return "Um dos jogos ainda está sem horário — não há o que trocar.";

        if (sedes != null && sedes.MaisDeUmClube)
            return NaoPodeIrPraVagaDe(a, b, sedes) ?? NaoPodeIrPraVagaDe(b, a, sedes);

        return null;
    }

    // `quem` pode tomar a vaga de `dono`? Mesmas duas réguas de GradeDeJogos.Encaixar: categoria
    // presa a um clube (`ClubeDaCategoria`) e categoria tirada do externo (`PodeIrPraSedeExtra`).
    private static string? NaoPodeIrPraVagaDe(Lado quem, Lado dono, SedesDoTorneio sedes)
    {
        if (quem.CategoriaId is not int categoriaId) return null;
        if (dono.ClubeDaVaga(sedes) is not int clube) return null;

        bool presa = sedes.ClubeDaCategoria(categoriaId) is int fixo
            ? fixo != clube
            : !sedes.PodeIrPraSedeExtra(categoriaId) && sedes.EhSedeExtra(clube);
        if (!presa) return null;

        var categoria = quem.NomeDaCategoria ?? "essa categoria";
        var nomeDoClube = sedes.NomeDoClube(clube) ?? "outro clube";
        var quemEh = quem.Real != null ? $"O jogo {quem.Real.Codigo}" : $"O jogo previsto {quem.Rotulo}";
        return $"{quemEh} é da {categoria}, que não joga no {nomeDoClube} — e o horário "
             + $"{dono.Horario:dd/MM HH:mm} de {dono.Rotulo} é lá. O clube é do horário, não do jogo.";
    }
}
