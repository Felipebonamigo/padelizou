using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Na lista de inscritos do "Interno Los Corneteiros" conviviam "ALAN DA SILVEIRA MACHADO" e
// "charls gustavio polese" — cada um digita como quer, e junto numa lista fica feio. A caixa
// é arrumada só na EXIBIÇÃO: o nome é dado da pessoa e a coluna continua com o que ela
// escreveu.
public class NomeBonitoTests
{
    [Theory]
    [InlineData("ALAN DA SILVEIRA MACHADO", "Alan da Silveira Machado")]
    [InlineData("charls gustavio polese", "Charls Gustavio Polese")]
    [InlineData("Mateus muller figueiredo", "Mateus Muller Figueiredo")]
    [InlineData("HENDERSON TAKAHAMA", "Henderson Takahama")]
    [InlineData("Alexandre Medina", "Alexandre Medina")]
    public void Nome_torto_vira_nome_de_lista(string digitado, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Theory]
    [InlineData("JOAO DA SILVA", "Joao da Silva")]
    [InlineData("maria dos santos", "Maria dos Santos")]
    [InlineData("PEDRO DE OLIVEIRA E SOUZA", "Pedro de Oliveira e Souza")]
    [InlineData("ana das neves", "Ana das Neves")]
    public void Particula_no_meio_do_nome_fica_minuscula(string digitado, string esperado)
    {
        // "Alan da Silveira", não "Alan Da Silveira".
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Fact]
    public void Particula_no_COMECO_continua_maiuscula()
    {
        // Quem se chama "Del Rey" perderia a maiúscula do próprio primeiro nome.
        Assert.Equal("Del Rey Consultoria", NomeBonito.Formatar("DEL REY CONSULTORIA"));
        Assert.Equal("Da Silva", NomeBonito.Formatar("da silva"));
    }

    [Theory]
    [InlineData("McDonald")]
    [InlineData("DiCaprio")]
    [InlineData("MacLeod")]
    [InlineData("O'Brien")]
    public void Quem_tem_maiuscula_no_meio_escreveu_assim_de_proposito(string nome)
    {
        // O rolo compressor viraria "Mcdonald" — estragando justamente o nome de quem se deu
        // ao trabalho de digitar certo.
        Assert.Equal(nome, NomeBonito.Formatar(nome));
    }

    [Theory]
    [InlineData("ana-maria souza", "Ana-Maria Souza")]
    [InlineData("d'avila", "D'Avila")]
    [InlineData("JOSE M. DA COSTA", "Jose M. da Costa")]
    public void Hifen_apostrofo_e_ponto_comecam_pedaco_novo(string digitado, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Theory]
    [InlineData("  ALAN   DA   SILVA  ", "Alan da Silva")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Espaco_sobrando_e_vazio_nao_quebram(string? digitado, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Fact]
    public void Acento_sobrevive()
    {
        Assert.Equal("Otávio Wunsch Júnior", NomeBonito.Formatar("OTÁVIO WUNSCH JÚNIOR"));
        Assert.Equal("Eric Hübner", NomeBonito.Formatar("eric hübner"));
    }

    // ── O que a tela realmente usa ────────────────────────────────────────────────────────

    [Fact]
    public void O_que_esta_GRAVADO_nao_muda()
    {
        // O ponto da decisão: arrumar aparência não pode reescrever dado da pessoa.
        var jogador = new Jogador { Nome = "ALAN DA SILVEIRA MACHADO", Cpf = "11144477735" };

        Assert.Equal("Alan da Silveira Machado", jogador.NomeNaTela);
        Assert.Equal("ALAN DA SILVEIRA MACHADO", jogador.Nome);
    }

    [Fact]
    public void Apelido_tambem_sai_arrumado()
    {
        var jogador = new Jogador { Nome = "LUCAS ALMEIDA", Cpf = "11144477735", Apelido = "FOKA" };

        Assert.Equal("Lucas Almeida (Foka)", jogador.ComoChamar);
        Assert.Equal("Lucas Almeida (Foka)", jogador.NomeComApelido);
    }

    [Fact]
    public void A_dupla_mostra_os_dois_arrumados()
    {
        var dupla = new Dupla
        {
            Codigo = "D1",
            Jogador1 = new Jogador { Nome = "ALAN DA SILVEIRA MACHADO", Cpf = "11144477735" },
            Jogador2 = new Jogador { Nome = "henderson takahama", Cpf = "22255588846" },
        };

        Assert.Equal("Alan da Silveira Machado & Henderson Takahama", dupla.NomeDeExibicao);
    }

    [Fact]
    public void Time_continua_escrito_como_foi_cadastrado()
    {
        // Nome de time é marca, não nome de pessoa: "ST Led" e "Target.it" perderiam a
        // identidade se passassem pela mesma régua.
        var dupla = new Dupla { Codigo = "T1", NomeTime = "ST Led" };

        Assert.Equal("ST Led", dupla.NomeDeExibicao);
    }

    // ---- Nome curto: primeiro e último ----
    // Numa tabela de grupo ou numa vaga de chaveamento, nome de três palavras ocupa três
    // alturas ou é cortado no meio — e o pedaço que sobra é o nome do meio, justamente o
    // que menos identifica alguém.

    [Theory]
    [InlineData("Anderson Matteus Schwaab", "Anderson Schwaab")]
    [InlineData("Frederico Siqueira de Paula Vargas", "Frederico Vargas")]
    [InlineData("ALAN DA SILVEIRA MACHADO", "Alan Machado")]
    [InlineData("Helcio Reisdorfer Correa de Barros", "Helcio Barros")]
    [InlineData("charls gustavio polese", "Charls Polese")]
    public void Nome_comprido_fica_com_o_primeiro_e_o_ultimo(string nome, string esperado)
        => Assert.Equal(esperado, NomeBonito.Curto(nome));

    [Theory]
    [InlineData("Marcos Coelho", "Marcos Coelho")]
    [InlineData("Neni", "Neni")]
    public void Nome_de_uma_ou_duas_palavras_fica_como_esta(string nome, string esperado)
        => Assert.Equal(esperado, NomeBonito.Curto(nome));

    [Theory]
    [InlineData("Otávio Wunsch Junior", "Otávio Wunsch")]
    [InlineData("Paulo Cesar Filho", "Paulo Cesar")]
    [InlineData("Joao Silva Neto", "Joao Silva")]
    public void Sufixo_de_geracao_viaja_colado_no_sobrenome(string nome, string esperado)
    {
        // "Otávio Junior" não identifica ninguém — quem identifica é "Otávio Wunsch".
        Assert.Equal(esperado, NomeBonito.Curto(nome));
    }

    [Fact]
    public void Particula_solta_no_fim_nao_vira_sobrenome()
    {
        // Nome digitado torto ("Ana Paula de") não pode virar "Ana de".
        Assert.Equal("Ana Paula", NomeBonito.Curto("Ana Paula de"));
    }

    [Fact]
    public void Como_chamar_usa_o_nome_curto_e_o_apelido_vem_JUNTO()
    {
        var semApelido = new Jogador { Nome = "Anderson Matteus Schwaab", Cpf = "1" };
        var comApelido = new Jogador { Nome = "Geovani Batista", Apelido = "Neni", Cpf = "2" };

        Assert.Equal("Anderson Schwaab", semApelido.ComoChamar);

        // Até 06/08/2026 isto devolvia só "Neni". O apelido escondia a pessoa: numa chave,
        // "Neni" pode ser três gente e quem vê de fora não sabe de quem se trata.
        Assert.Equal("Geovani Batista (Neni)", comApelido.ComoChamar);

        // O nome COMPLETO segue existindo pra onde ele importa (perfil, financeiro) — e é ele
        // que vai pro Ranking RS, onde o casamento é por nome inteiro.
        Assert.Equal("Anderson Matteus Schwaab", semApelido.NomeNaTela);
    }

    // ── Nome curto + apelido: a regra que vale em TODAS as telas ──────────────────────────

    [Theory]
    [InlineData("José Carlos da Silva", "Zeca", "José Silva (Zeca)")]
    [InlineData("Anderson Matteus Schwaab", "Deco", "Anderson Schwaab (Deco)")]
    [InlineData("Marcos Coelho", null, "Marcos Coelho")]
    [InlineData("Marcos Coelho", "", "Marcos Coelho")]
    [InlineData("Marcos Coelho", "   ", "Marcos Coelho")]
    // O sufixo de geração viaja colado no sobrenome: "Otávio Junior" não identifica ninguém.
    [InlineData("Otávio Wunsch Junior", "Tavinho", "Otávio Wunsch (Tavinho)")]
    // Partícula não é sobrenome.
    [InlineData("Frederico Siqueira de Paula Vargas", null, "Frederico Vargas")]
    // Caixa torta no apelido também se arruma.
    [InlineData("charls gustavio polese", "CHARLINHO", "Charls Polese (Charlinho)")]
    public void Nome_curto_com_apelido_entre_parenteses(string nome, string? apelido, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.ComApelido(nome, apelido));
    }

    [Fact]
    public void Apelido_igual_ao_nome_curto_nao_vira_repeticao()
    {
        // Acontece com quem preenche o apelido repetindo o nome "pra garantir" — e
        // "Marcos Coelho (Marcos Coelho)" é o tipo de coisa que faz a tela parecer quebrada.
        Assert.Equal("Marcos Coelho", NomeBonito.ComApelido("Marcos Coelho", "marcos coelho"));
    }

    // ─────────────── APELIDO QUE NÃO ACRESCENTA NADA ───────────────
    //
    // 11/09/2026 — 🗣️ Felipe, no print do ranking de palpiteiros: *"aqui tem o mesmo problema,
    // talvez tenhamos q rever isso no sistema inteiro"*. 🕳️ Metade da tabela quebrava em duas
    // linhas por causa do parêntese — e o parêntese não dizia nada novo: "Paulo Pujol (Pujol)",
    // "Bruna Vargas (Bru)", "Bibiana Bottin (Bibi)", "Caroline Tedesco (Carol Tedesco)".
    //
    // A regra de 06/08/2026 ("apelido não identifica ninguém de fora da turma") continua de pé:
    // o que sai é só o apelido que JÁ ESTÁ no nome, e nada mais.

    [Theory]
    // Apelido igual a uma palavra do nome.
    [InlineData("Paulo Ricardo Pujol", "Pujol", "Paulo Pujol")]
    [InlineData("Alexandre Medina", "Medina", "Alexandre Medina")]
    // Apelido que é o começo de uma palavra do nome.
    [InlineData("Bruna Vargas", "Bru", "Bruna Vargas")]
    [InlineData("Bibiana Bottin", "Bibi", "Bibiana Bottin")]
    [InlineData("Guilherme Drachler Bagesteiro", "Bages", "Guilherme Bagesteiro")]
    // Apelido de duas palavras, todas cobertas.
    [InlineData("Caroline Tedesco", "Carol Tedesco", "Caroline Tedesco")]
    // ⚠️ O nome do MEIO some da abreviação mas continua valendo aqui: "zenker" está no cadastro,
    // então "(Ana Zenker)" não traz letra nova pra tela.
    [InlineData("ana zenker pasinato", "Ana Zenker", "Ana Pasinato")]
    // Acento não pode fazer diferença: quem digita o apelido raramente acentua.
    [InlineData("Laís Rodrigues", "Lais", "Laís Rodrigues")]
    public void Apelido_que_ja_esta_no_nome_nao_vira_parentese(string nome, string apelido, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.ComApelido(nome, apelido));
    }

    [Theory]
    // ⚠️ O CONTRÁRIO É O QUE IMPORTA: apelido de verdade não pode sumir. "Juju" não é começo de
    // "Juliano" (J-u-l ≠ J-u-j), e é assim que a quadra chama a pessoa.
    [InlineData("Juliano Bender", "Juju", "Juliano Bender (Juju)")]
    [InlineData("José Carlos da Silva", "Zeca", "José Silva (Zeca)")]
    [InlineData("Otávio Wunsch Junior", "Tavinho", "Otávio Wunsch (Tavinho)")]
    // "Charls" é mais CURTO que "Charlinho": o nome não começa com o apelido, então fica.
    [InlineData("charls gustavio polese", "CHARLINHO", "Charls Polese (Charlinho)")]
    public void Apelido_que_a_quadra_usa_CONTINUA_aparecendo(string nome, string apelido, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.ComApelido(nome, apelido));
    }

    // ─────────────── O PERFIL É ONDE O NOME COMPLETO MORA ───────────────
    //
    // 11/09/2026 — 🗣️ Felipe: *"e ai so quando abrir o perfil vera o nome completo?"*. É isso
    // mesmo — e a pergunta descobriu o buraco: o perfil imprimia `jogador.Nome` CRU, então era
    // justamente ali, na página feita pra mostrar o nome inteiro, que "JOAO EGIDIO FERREIRA DA
    // ROCHA" aparecia gritando.
    //
    // ⚠️ Teste de FONTE: a suíte não renderiza Razor. O que se trava aqui é que nenhuma linha do
    // perfil escreve nome de PESSOA sem passar pelo NomeBonito — o completo continua completo,
    // só a caixa se arruma.

    [Fact]
    public void O_perfil_nao_escreve_nome_de_pessoa_sem_passar_pelo_NomeBonito()
    {
        var linhas = File.ReadAllLines(CaminhoDoPerfil());
        var cruas = new List<string>();

        for (var i = 0; i < linhas.Length; i++)
        {
            // `\.Nome\b` não casa com `.NomeNaTela` nem `.NomeComApelido`: depois de "Nome"
            // vem letra, e ali não há fronteira de palavra. Sobra só o acesso cru.
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    linhas[i], @"\b(jogador|Autor|Oponente|Parceiro)\.Nome\b")) continue;

            // Passar o nome cru PRA DENTRO do NomeBonito é o jeito certo — é o que a linha do
            // "para falar com ..." já fazia antes de tudo isto.
            if (linhas[i].Contains("NomeBonito.", StringComparison.Ordinal)) continue;

            cruas.Add($"linha {i + 1}: {linhas[i].Trim()}");
        }

        Assert.True(cruas.Count == 0,
            "Nome de pessoa escrito cru no perfil:\n" + string.Join("\n", cruas));
    }

    [Fact]
    public void O_apelido_no_perfil_tambem_passa_pelo_NomeBonito()
    {
        // 🗣️ "mas o apelido redundante fora, aparece se eu for no perfil da pessoa?" — aparece,
        // e é pra aparecer: ali ele é o CAMPO do cadastro, não enfeite do nome. 🕳️ Só que saía
        // cru: quem digitou "PUJOL" via "PUJOL" gritado logo embaixo de um nome com a caixa já
        // arrumada, desencontrado na mesma linha de código.
        var fonte = File.ReadAllText(CaminhoDoPerfil());

        Assert.DoesNotContain("\"@Model.jogador.Apelido\"", fonte);
        Assert.Contains("NomeBonito.Formatar(Model.jogador.Apelido)", fonte);
    }

    private static string CaminhoDoPerfil()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views", "Jogadores", "Perfil.cshtml");
            if (File.Exists(alvo)) return alvo;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Não achei o Perfil.cshtml subindo a partir de " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Nome_de_uma_palavra_so_nao_quebra()
    {
        // Cadastro antigo, de antes de o sobrenome virar obrigatório. Ainda tem que aparecer.
        Assert.Equal("Arthur", NomeBonito.ComApelido("Arthur", null));
        Assert.Equal("Arthur (Tutu)", NomeBonito.ComApelido("Arthur", "Tutu"));
    }
}
