using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// AS TRÊS TELAS QUE MARCAM AULA USAM CALENDÁRIO E RELÓGIO, NUNCA A RODINHA.
//
// 🗣️ O motivo é do Felipe, e não é estético: "o motivo de marcar aula assim é para que o
// professor consiga saber que dia da semana que é". No Android o `datetime-local` abre a
// RODINHA de rolagem, onde a data é um número solto — sem calendário e sem dia da semana.
// `type="date"` abre o calendário do sistema e `type="time"` abre o relógio.
//
// ⚠️ É TESTE DE FONTE, e é escolha consciente: não há suíte de UI neste projeto, e uma dessas
// telas voltando pra rodinha não quebraria NADA — ela abre, a aula salva, e só o professor de
// pé na quadra descobre. A alternativa era não travar nada.
//
// ⚠️ E ele prende as TRÊS juntas de propósito: a de Adicionar foi convertida em 25/08/2026 e
// as outras duas ficaram pra trás no mesmo dia. Uma tela por vez é como elas se separam.
public class TelasDeAulaSemRodinhaDeDataTests
{
    private static string Fonte(params string[] caminho)
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(new[] { pasta, "Padelizou" }.Concat(caminho).ToArray());
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException($"{string.Join('/', caminho)} não encontrado a partir do bin.");
    }

    // ⚠️ OS COMENTÁRIOS SAEM ANTES DE QUALQUER BUSCA, e isto é o teste se defendendo de si
    // mesmo — descoberto rodando: as três telas EXPLICAM nos comentários por que o
    // `datetime-local` saiu, e o dia-da-semana.js explica por que não usa `new Date(valor)`.
    // Sem tirar comentário, este arquivo reprova exatamente a documentação que ele quer que
    // exista, e a saída mais fácil vira apagar o comentário — o pior conserto possível.
    //
    // Só linha que COMEÇA com `//`, mais os blocos Razor: cortar no `//` do meio da linha
    // levaria junto o `https://` de uma URL dentro de string.
    private static string SemComentarios(string fonte)
    {
        var semRazor = Regex.Replace(fonte, @"@\*.*?\*@", "", RegexOptions.Singleline);
        return Regex.Replace(semRazor, @"^[ \t]*//.*$", "", RegexOptions.Multiline);
    }

    public static TheoryData<string> AsTresTelas() => new()
    {
        "AdicionarManual.cshtml", "Editar.cshtml", "MinhaAgenda.cshtml",
    };

    private static string FonteDe(string tela) =>
        SemComentarios(Fonte("Views", "Aulas", tela));

    [Theory]
    [MemberData(nameof(AsTresTelas))]
    public void Nenhuma_delas_tem_campo_de_datetime_local(string tela)
    {
        Assert.DoesNotMatch(new Regex("type=\"datetime-local\""), FonteDe(tela));
    }

    [Theory]
    [MemberData(nameof(AsTresTelas))]
    public void Todas_elas_tem_calendario_e_relogio(string tela)
    {
        var fonte = FonteDe(tela);

        Assert.Matches(new Regex("type=\"date\""), fonte);
        Assert.Matches(new Regex("type=\"time\""), fonte);
    }

    // Os dois campos precisam chegar ao servidor com os nomes que as actions leem. Trocar um
    // `name` deixa o formulário postando pro nada, e o professor só vê "escolha a data e a
    // hora" — uma mensagem que aponta pra ele, quando o erro é da tela.
    [Theory]
    [MemberData(nameof(AsTresTelas))]
    public void Os_dois_campos_se_chamam_data_e_hora(string tela)
    {
        var fonte = FonteDe(tela);

        Assert.Matches(new Regex("type=\"date\"[^>]*name=\"data\"|name=\"data\"[^>]*type=\"date\""), fonte);
        Assert.Matches(new Regex("type=\"time\"[^>]*name=\"hora\"|name=\"hora\"[^>]*type=\"time\""), fonte);
    }

    // ⚠️ O dia da semana por extenso é O PEDIDO, não um detalhe: o calendário mostra o dia
    // enquanto está aberto, e depois de fechar sobra "18/09/2026" na tela.
    //
    // A régua é UMA só (wwwroot/js/dia-da-semana.js) e as três telas a pedem por atributo.
    // Copiar o JS pra dentro de cada uma é como duas passam a discordar — e discordariam
    // justamente no detalhe abaixo, que não dá erro nenhum quando está errado.
    [Theory]
    [MemberData(nameof(AsTresTelas))]
    public void Todas_elas_pedem_o_dia_da_semana_pela_regua_compartilhada(string tela)
    {
        Assert.Matches(new Regex("data-dia-da-semana"), FonteDe(tela));
    }

    [Fact]
    public void A_regua_do_dia_da_semana_monta_a_data_por_partes_e_nunca_com_new_Date_da_string()
    {
        var js = SemComentarios(Fonte("wwwroot", "js", "dia-da-semana.js"));

        // Por partes: ano, mês-1, dia.
        Assert.Matches(new Regex(@"new Date\(\s*\+?\w+\[0\]"), js);

        // E nunca a string crua dentro de new Date(...): ela é lida como UTC e volta o dia
        // ANTERIOR em todo fuso negativo — no Brasil inteiro. Terça vira segunda, calado.
        Assert.DoesNotMatch(new Regex(@"new Date\(\s*\w+(\.value|\.val\(\))?\s*\)"), js);
    }
}
