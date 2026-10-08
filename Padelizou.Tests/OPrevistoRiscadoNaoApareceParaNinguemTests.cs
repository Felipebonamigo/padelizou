using Xunit;

namespace Padelizou.Tests;

// O "PREVISTO 10/10 19:00" RISCADO SAIU DA TELA — PRA TODO MUNDO.
//
// 🗣️ Felipe, 07/10/2026, com o print de dois jogos do NATA PADEL TOUR (19:50 com "previsto 19:00"
// riscado embaixo, e 20:40 com "previsto 11/10 10:40"): *"não precisa ter esse previsto para o
// público, nem pra mim"*.
//
// A linha tinha nascido de um pedido dele mesmo (14/09/2026, depois do 2ª Etapa do ER: "temos que
// seguir a grade prevista, por que o usuário se baseia"). Ele a desfez: o horário que vale é o do
// card, e uma segunda hora riscada embaixo polui mais do que informa.
//
// ⚠️ SÓ A TELA SAIU. `Partida.HorarioDoSorteio` continua sendo gravado no nascimento do jogo
// (DbPadelContext.CarimbarOHorarioDoSorteio) — os testes dele seguem valendo, e apagar a coluna
// seria migration, que ninguém pediu. Se um dia a promessa voltar a aparecer, o dado está lá.
//
// ⚠️ É TESTE DE FONTE, e a varredura é de TODAS as views e do JS (não só do card): "pro público e
// pra mim" são telas diferentes — a lista de jogos, o card, a arte, o "Meus jogos" —, e a
// pergunta que importa é se ALGUMA delas lê o campo. Uma só que sobrar traz a linha de volta.
public class OPrevistoRiscadoNaoApareceParaNinguemTests
{
    private static string RaizDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            if (Directory.Exists(Path.Combine(pasta, "Padelizou", "Views"))) return Path.Combine(pasta, "Padelizou");
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Pasta Padelizou/ não encontrada a partir do bin.");
    }

    private static IEnumerable<string> TelasEScripts()
    {
        var raiz = RaizDoProjeto();
        foreach (var arquivo in Directory.EnumerateFiles(Path.Combine(raiz, "Views"), "*.cshtml", SearchOption.AllDirectories))
            yield return arquivo;

        // Só o JS NOSSO: `wwwroot/lib` é biblioteca de terceiro e não tem por que citar o campo.
        foreach (var arquivo in Directory.EnumerateFiles(Path.Combine(raiz, "wwwroot", "js"), "*.js", SearchOption.TopDirectoryOnly))
            yield return arquivo;
    }

    [Fact]
    public void Nenhuma_tela_nem_script_le_o_horario_que_o_sorteio_prometeu()
    {
        var quemLe = TelasEScripts()
            .Where(arquivo => File.ReadAllText(arquivo).Contains("HorarioDoSorteio", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(quemLe.Count == 0,
            "Ainda leem Partida.HorarioDoSorteio (a linha riscada \"previsto dd/MM HH:mm\"): "
            + string.Join(", ", quemLe));
    }

    [Fact]
    public void O_card_do_jogo_nao_tem_mais_a_etiqueta_do_previsto()
    {
        var card = File.ReadAllText(Path.Combine(RaizDoProjeto(), "Views", "Torneios", "_JogoEmLinha.cshtml"));

        Assert.DoesNotContain("pdz-jl-prometido", card);
        Assert.DoesNotContain("Horário do sorteio", card);
    }

    [Fact]
    public void O_horario_de_agora_continua_no_card()
    {
        // O avesso: tirar o riscado não pode levar junto a hora que vale. É esta que o jogador lê.
        var card = File.ReadAllText(Path.Combine(RaizDoProjeto(), "Views", "Torneios", "_JogoEmLinha.cshtml"));

        Assert.Contains("jogo.HorarioPrevisto is DateTime h", card);
        Assert.Contains("h.ToString(\"HH:mm\")", card);
    }
}
