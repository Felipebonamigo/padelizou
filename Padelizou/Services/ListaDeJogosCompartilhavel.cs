using Padelizou.Models;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Services;

// "COMPARTILHAR ESTA LISTA" — a aba Jogos, do jeito que está filtrada, pronta pra sair do site
// (10/09/2026).
//
// 🗣️ Felipe, num print da aba Jogos do 2ª Etapa ER PADEL TOUR filtrada por "Los Corneteiros":
// *"no final da lista, criar um botão 'compartilhar lista' para o usuario poder mandar no grupo
// d whats dele, a lista selecionada, ou até uma imagem para compartilhar na rede social"*, e
// *"Algo que fique bom para compartilhar no insta tambem"*.
//
// Dois destinos, dois formatos, UMA lista:
//   · TEXTO pro WhatsApp (TextoDaLista) — o grupo do time, o privado;
//   · ARTE 1080×1350 pro story e pro feed (CartaoDosJogos) — o Instagram.
//
// ⚠️ A LISTA É A DA TELA, LETRA POR LETRA. Ela não consulta o banco: recebe a fila que a aba já
// montou (OrdemNoHorario, depois de todos os filtros de TorneiosController.CarregarViewBagJogosAsync)
// e só traduz cada linha pras palavras que a tela usa — CategoriaNaTela.Curto na categoria,
// LugarDoJogo.Etiqueta no lugar, o "parceiro" da vaga em aberto. Uma segunda consulta aqui
// seria a segunda régua de "quais jogos", e o grupo do WhatsApp receberia uma grade diferente da
// que a pessoa estava olhando quando apertou o botão. É também o que faz o portão da chave em
// aprovação valer aqui de graça: a fila chega vazia pra quem não organiza.

// Uma linha da lista. Os lados vêm em DUAS formas porque os dois destinos têm larguras
// diferentes: o texto tem espaço pra "Pedro Kirchner / Carlos Morais" (e no Americano o
// primeiro nome não identifica ninguém — ver _JogoEmLinha); a arte não, e usa o compacto dos
// cards, "Pedro / Carlos" (NomeDaDupla.Compacto, que existe por causa do card da chave).
public sealed record JogoDaLista(
    DateTime? Horario,
    string Categoria,
    string Fase,
    string? Lugar,
    string Lado1,
    string Lado2,
    string Lado1Curto,
    string Lado2Curto,
    bool Previa);

public static class ListaDeJogos
{
    // A vaga de parceiro em aberto, com a MESMA palavra da linha da tela: "Pedro / ?" parecia
    // dado corrompido, e "Pedro" sozinho parece jogo de simples.
    public const string SemParceiro = "parceiro";

    /// <summary>
    /// Traduz a fila da aba Jogos pras linhas que o texto e a arte leem. <paramref name="comPrevias"/>
    /// é a pergunta que a tela faz — 🗣️ *"se vale apenas os jogos ja marcados ou se os possiveis
    /// tambem (por que o mata mata nao ta definido)"*: com false, a eliminatória que ainda não
    /// nasceu (JogoQueVem) fica de fora.
    /// </summary>
    public static List<JogoDaLista> Montar(
        IEnumerable<OrdemNoHorario.Linha> fila, SedesDoTorneio? sedes, bool comPrevias)
    {
        var lista = new List<JogoDaLista>();

        foreach (var linha in fila)
        {
            if (linha.Jogo is Partida jogo)
            {
                lista.Add(new JogoDaLista(
                    Horario: jogo.HorarioPrevisto,
                    Categoria: CategoriaNaTela.Curto(jogo.Categoria?.Nome),
                    Fase: jogo.Fase,
                    Lugar: LugarDoJogo.Etiqueta(sedes, jogo.NomeQuadra, jogo.CategoriaId, jogo.ClubeId),
                    Lado1: ComoATela(jogo.Dupla1),
                    Lado2: ComoATela(jogo.Dupla2),
                    Lado1Curto: ComoOsCards(jogo.Dupla1),
                    Lado2Curto: ComoOsCards(jogo.Dupla2),
                    Previa: false));
            }
            else if (comPrevias && linha.Previsto is JogoQueVem previa)
            {
                lista.Add(new JogoDaLista(
                    Horario: previa.Horario,
                    Categoria: CategoriaNaTela.Curto(previa.Categoria),
                    // Numerada ("Quartas de Final 2"), como a linha da prévia: é assim que os
                    // outros jogos citam este.
                    Fase: previa.FaseNumerada,
                    Lugar: LugarDoJogo.Etiqueta(sedes, previa.Quadra, previa.CategoriaId, previa.ClubeId),
                    Lado1: previa.Lado1.Rotulo,
                    Lado2: previa.Lado2.Rotulo,
                    Lado1Curto: previa.Lado1.Rotulo,
                    Lado2Curto: previa.Lado2.Rotulo,
                    Previa: true));
            }
        }

        return lista;
    }

    // A dupla como a linha da aba escreve (_JogoEmLinha.NomeDaDupla): time pelo nome do time,
    // gente por nome e sobrenome (NomeBonito.Curto, a régua do chip e da classificação).
    private static string ComoATela(Dupla? dupla)
    {
        if (dupla == null) return ChaveParaCard.VagaEmAberto;
        if (dupla.EhTime) return dupla.NomeTime ?? "Time";

        var primeiro = dupla.Jogador1 == null ? "?" : NomeBonito.Curto(dupla.Jogador1.Nome);
        var segundo = dupla.Jogador2 == null ? SemParceiro : NomeBonito.Curto(dupla.Jogador2.Nome);
        return $"{primeiro} / {segundo}";
    }

    // A dupla como os cards escrevem: só o primeiro nome (ou o apelido) de cada um.
    private static string ComoOsCards(Dupla? dupla)
    {
        if (dupla == null) return ChaveParaCard.VagaEmAberto;

        var compacto = NomeDaDupla.CompactoNa(dupla);
        if (compacto.Length == 0) return ChaveParaCard.VagaEmAberto;

        // O compacto de uma dupla sem parceiro é só o primeiro nome — e aqui a vaga tem que
        // aparecer, pela mesma razão da tela.
        return !dupla.EhTime && dupla.Jogador2 == null ? $"{compacto} / {SemParceiro}" : compacto;
    }
}

// O TEXTO QUE VAI PRO WHATSAPP.
//
// Curto de propósito e sem emoji (ícone rende diferente em cada aparelho — é a regra da aba).
// O negrito é o do WhatsApp (*asteriscos*): funciona no app e vira asterisco inofensivo em
// qualquer outro lugar onde o texto for colado.
//
// ⚠️ ESTE ARQUIVO NÃO MANDA NADA — mesma régua de ConviteProTorneio: é a pessoa que abre o
// WhatsApp dela com o texto no campo e escolhe o grupo.
public static class TextoDaLista
{
    // Torneio por ordem de liberação: a hora não existe, e inventar uma seria mentir. É a
    // palavra da linha da tela.
    public const string PorOrdem = "por ordem";

    public static string Quando(JogoDaLista jogo) =>
        jogo.Horario is DateTime h ? h.ToString("HH:mm") : PorOrdem;

    // "sex 11/09" — o mesmo abreviado da linha (DiaDaSemana), sem depender do ICU do servidor.
    public static string Dia(DateTime? dia) =>
        dia is DateTime d ? $"{DiaDaSemana.Curto(d)} {d:dd/MM}" : "sem data";

    public static string Montar(string torneio, string? recorte, IReadOnlyList<JogoDaLista> jogos, string link)
    {
        var linhas = new List<string>
        {
            string.IsNullOrWhiteSpace(recorte) ? $"*{torneio}*" : $"*{torneio}* — {recorte}",
        };

        // O dia aparece UMA vez, como cabeçalho, e não repetido em cada linha — 88 linhas com
        // "11/09" na frente é o que fazia o modal do ⇄ ficar "muito poluído" (10/09/2026).
        //
        // ⚠️ E CADA JOGO VEM SEPARADO POR UMA LINHA EM BRANCO (11/09/2026). 🗣️ Felipe:
        // *"adicione o espaço de uma linha nos textos, por jogo, para nao ficar amontoado"*. Sem
        // ela, doze jogos viravam 24 linhas coladas no grupo e achar o seu exigia contar de dois
        // em dois. A linha em branco vem ANTES de cada jogo (e não depois), que é o que garante
        // que ela nunca dobre com a do cabeçalho do dia nem com a do link no fim.
        DateTime? diaAberto = DateTime.MinValue;
        foreach (var jogo in jogos)
        {
            var diaDoJogo = jogo.Horario?.Date;
            if (diaDoJogo != diaAberto)
            {
                linhas.Add("");
                linhas.Add($"*{Dia(diaDoJogo)}*");
                diaAberto = diaDoJogo;
            }

            linhas.Add("");
            linhas.Add(Contexto(jogo));
            linhas.Add($"{jogo.Lado1} x {jogo.Lado2}");
        }

        linhas.Add("");
        linhas.Add($"Lista completa e atualizada: {link}");

        return string.Join("\n", linhas);
    }

    // "18:00 · 6ª Masculina · Grupo D · Er Padel" — as etiquetas da linha, na ordem da linha.
    // A prévia diz que é prévia: sem o aviso, "1º Grupo A x 2º Grupo B" parece que o sistema
    // não sabe quem joga.
    public static string Contexto(JogoDaLista jogo)
    {
        var partes = new List<string> { Quando(jogo), jogo.Categoria, jogo.Fase };
        if (!string.IsNullOrWhiteSpace(jogo.Lugar)) partes.Add(jogo.Lugar);
        if (jogo.Previa) partes.Add("prévia");
        return string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}

// A MESMA LISTA EM COLUNAS — o arquivo que abre na planilha.
//
// 🗣️ Felipe, 11/09/2026: *"crie tambem uma opção de importar planilha se quiserem"*. Quem quer
// ORGANIZAR a grade (ordenar por quadra, imprimir pro balcão, mandar a escala pro clube) não
// quer texto nem arte — quer as colunas, na ferramenta dele.
//
// ⚠️ CSV, E NÃO XLSX, e isso é o degrau 3 da escada deste projeto: o formato nativo abre no
// Excel, no Google Sheets e no Numbers sem UMA dependência nova. Um pacote de planilha (ClosedXML
// e parentes) entraria no publish, no backup e na lista de coisas que envelhecem, pra entregar o
// mesmo conteúdo com negrito.
//
// ⚠️ E ELE SAI DA MESMA `JogoDaLista` das outras duas saídas. Uma consulta própria aqui seria a
// terceira régua de "quais jogos", e a planilha diria uma coisa enquanto a arte ao lado dela
// dissesse outra.
public static class PlanilhaDaLista
{
    // ⚠️ PONTO-E-VÍRGULA, e não vírgula. O Excel em português abre CSV pelo separador de lista
    // do Windows, que aqui é ";" — com vírgula a planilha inteira cai numa coluna só, e quem
    // abriu não tem como saber por quê. O Google Sheets detecta os dois.
    public const string Separador = ";";

    // CRLF: é o que a RFC 4180 manda e o que o Excel espera dentro de campo com quebra.
    private const string FimDeLinha = "\r\n";

    private static readonly string[] Colunas =
        { "Data", "Hora", "Categoria", "Fase", "Local", "Dupla 1", "Dupla 2", "Situação" };

    public static string Montar(IReadOnlyList<JogoDaLista> jogos)
    {
        var texto = new System.Text.StringBuilder();
        texto.Append(string.Join(Separador, Colunas)).Append(FimDeLinha);

        foreach (var jogo in jogos)
        {
            texto.Append(string.Join(Separador, new[]
            {
                // Data e hora em colunas SEPARADAS: é o que deixa ordenar e filtrar do outro
                // lado. Sem horário (torneio por ordem) a data fica vazia e a hora diz a palavra
                // da tela — duas colunas vazias pareceriam dado perdido.
                Campo(jogo.Horario?.ToString("dd/MM/yyyy")),
                Campo(TextoDaLista.Quando(jogo)),
                Campo(jogo.Categoria),
                Campo(jogo.Fase),
                Campo(jogo.Lugar),
                // O nome COMPLETO, como no texto: a planilha tem largura de coluna, o card não.
                Campo(jogo.Lado1),
                Campo(jogo.Lado2),
                // A prévia é dita aqui também — uma coluna "1º Grupo A" sem aviso vira uma dupla
                // inventada assim que alguém filtrar a planilha.
                Campo(jogo.Previa ? "Prévia" : "Marcado"),
            })).Append(FimDeLinha);
        }

        return texto.ToString();
    }

    // ⚠️ COM BOM, e é ele que faz o arquivo abrir certo. Sem os três bytes, o Excel lê como
    // Latin-1 e "6ª Masculina" chega como "6Âª Masculina" na tela de quem abriu — o defeito
    // clássico de CSV em português, que ninguém consegue explicar olhando o arquivo.
    public static byte[] EmBytes(string csv) =>
        System.Text.Encoding.UTF8.GetPreamble()
            .Concat(new System.Text.UTF8Encoding(false).GetBytes(csv))
            .ToArray();

    // RFC 4180: campo com o separador, com aspas ou com quebra de linha vai entre aspas, e as
    // aspas de dentro dobram. O que não tem nada disso sai cru — planilha onde todo campo tem
    // aspas é ilegível pra quem abre o arquivo no bloco de notas.
    private static string Campo(string? valor)
    {
        var texto = valor ?? "";
        if (!texto.Contains(Separador) && !texto.Contains('"')
            && !texto.Contains('\n') && !texto.Contains('\r'))
            return texto;

        return $"\"{texto.Replace("\"", "\"\"")}\"";
    }
}

// A FRASE QUE DIZ QUAIS FILTROS ESTAVAM LIGADOS — "Los Corneteiros · 3ª Feminina · Er Padel".
//
// Vai no título do texto e no subtítulo da arte, porque a lista compartilhada é um RECORTE e
// quem recebe precisa saber qual: "os jogos" sem dizer de quem faz o grupo perguntar "e os
// outros?". Nula quando nenhum filtro está ligado — aí a lista é o torneio inteiro.
public static class RecorteDaLista
{
    public static string? Descrever(
        bool meusJogos, string? time, IEnumerable<string> categorias, string? clube, string? quadra, string? fase)
    {
        var partes = new List<string>();

        if (meusJogos) partes.Add("meus jogos");
        if (!string.IsNullOrWhiteSpace(time)) partes.Add(time.Trim());
        partes.AddRange(categorias
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => CategoriaNaTela.Curto(c)));
        if (!string.IsNullOrWhiteSpace(clube)) partes.Add(clube.Trim());
        if (!string.IsNullOrWhiteSpace(quadra)) partes.Add(quadra.Trim());
        if (!string.IsNullOrWhiteSpace(fase))
            partes.Add(fase.Trim() == FasesTorneio.FaseDeGrupos ? "fase de grupos" : fase.Trim());

        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }
}
