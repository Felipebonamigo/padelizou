using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Categoria")]
public partial class Categoria
{
    public int Id { get; set; }

    public int TorneioId { get; set; }

    public string Nome { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public virtual ICollection<Dupla> Duplas { get; set; } = new List<Dupla>();

    public virtual ICollection<Partida> Partidas { get; set; } = new List<Partida>();
    public virtual ICollection<GrupoTorneio> GruposTorneio { get; set; } = new List<GrupoTorneio>();

    public virtual Torneio Torneio { get; set; } = null!;

    // Vagas máximas nesta categoria. Null = sem limite. Quem se inscrever depois de
    // atingido vai pra lista de espera (Dupla.EmListaDeEspera).
    public int? LimiteDuplas { get; set; }

    // ---- EM QUE CLUBE ESTA CATEGORIA JOGA (21/08/2026) ----
    //
    // Existe pro torneio que acontece em MAIS DE UM CLUBE. É assim que esses torneios se
    // organizam na prática: cada categoria fica inteira num clube, pra ninguém passar o dia
    // indo e voltando. Nulo — o caso de todo torneio de uma sede só — quer dizer "joga em
    // qualquer quadra do torneio", que é exatamente como sempre funcionou.
    //
    // ⚠️ Preenchida, ela é uma trava DURA na grade: a categoria não recebe quadra de outro
    // clube nem quando o horário lota. É o OPOSTO da `QuadraDaCategoria` (a quadra PREFERIDA),
    // que cede a quadra quando aperta — ver o degrau 3 de Services/PreferenciaDeQuadra. As
    // duas não podiam compartilhar mecanismo: preferir a central e não conseguir custa uma
    // quadra pior; "preferir" o clube certo e não conseguir manda o jogador pro outro lado da
    // cidade no meio do torneio.
    //
    // ⚠️ Categoria apontando pra clube SEM QUADRA no torneio não trava nada: a grade trata
    // como "sem sede" e marca em qualquer quadra. É de propósito — o organizador ainda apaga
    // quadra depois do sorteio sem trava nenhuma (TorneiosController.Criacao, ação Editar), e
    // o preço de uma sede que evaporou não pode ser a categoria inteira ficar sem horário.
    // Ver Services/SedesDoTorneio.
    public int? ClubeId { get; set; }
    public virtual Clube? Clube { get; set; }

    // Qual categoria do Ranking RS esta aqui representa (100 a 116 — ver
    // Services/CategoriaDoRankingRs). Null = esta categoria NÃO é validada contra o ranking,
    // e esse é o padrão.
    //
    // Existe como campo, e não como palpite calculado na hora, porque o nome no Padelizou é
    // texto livre: "6ª Categoria" não diz o sexo, e a API do ranking NÃO confere sexo — ela
    // responderia APROVADO pra um homem numa categoria feminina. O palpite do de-para serve
    // de sugestão na tela; quem confirma é o organizador.
    public int? RankingRsCategoriaId { get; set; }

    // ---- Divisão em grupos do TORNEIO AMERICANO ----
    //
    // Escolhida pelo organizador na hora do sorteio, quando ele já sabe quantos se inscreveram
    // (ver Services/DivisaoDoAmericano). 1 = sem divisão, todos num grupo só — que é o padrão
    // e o que os torneios anteriores a 06/08/2026 têm.
    //
    // Com mais de um grupo, `PassamPorGrupo` diz quantos de cada grupo vão pro GRUPO FINAL,
    // que é quem decide o título. Esse número não é fixo: é o menor que faz o grupo final
    // fechar "cada um com cada um" — com 3 grupos, passar 2 daria 6 finalistas, e 6 não fecha.
    public int GruposAmericano { get; set; } = 1;
    public int PassamPorGrupo { get; set; }

    // ---- Categoria de TIMES ----
    // Aqui quem disputa são times, não duplas: o organizador define a estrutura (quantos
    // times, quantos grupos, quantos classificam por grupo) e cadastra os times pelo nome —
    // jogador não se inscreve. Cada time vira uma Dupla com NomeTime preenchido, e daí o
    // motor inteiro (grupos, partidas, classificação, mata-mata, grade de horários) funciona
    // igual ao de duplas. Regras da estrutura em Services/CategoriaDeTimes.
    public bool DeTimes { get; set; }

    // A estrutura prometida pelo organizador. QuantidadeTimes é o alvo (a tela avisa quando
    // faltam times); QuantidadeGrupos manda no sorteio; ClassificadosPorGrupo manda no
    // mata-mata. Nulos nas categorias comuns.
    public int? QuantidadeTimes { get; set; }
    public int? QuantidadeGrupos { get; set; }
    public int? ClassificadosPorGrupo { get; set; }

    // ---- Categoria de CHAVE DIRETA ----
    // Mata-mata puro: sem fase de grupos, quem perde está fora na primeira derrota. Nasceu
    // pro torneio querer uma chave PARALELA às categorias — duplas remontadas misturando
    // gente de 4ª, 5ª e 6ª, disputando um mata-mata à parte no mesmo fim de semana.
    //
    // Duas consequências que o resto do código precisa respeitar:
    //  • o sorteio não cria grupo nenhum aqui (Services/ChaveamentoMataMata.MontarChaveDireta
    //    monta a primeira rodada direto, com bye pra quem sobra do quadro);
    //  • a mesma PESSOA fica inscrita duas vezes no torneio (na categoria dela e aqui), o que
    //    obriga a grade a evitar conflito por jogador — ver Services/GradeDeJogos.Encaixar.
    // Quem cadastra as duplas é o organizador, como na de times: jogador não se inscreve.
    public bool ChaveDireta { get; set; }

    // ---- ELIMINATÓRIA NO SÁBADO À NOITE (08/09/2026) ----
    //
    // 🗣️ Pedido do Felipe: "colocar por categoria, se vai ter jogos de eliminatórias no sábado
    // a noite ainda ou não. por exemplo, a 5a categoria feminina nao pode ter jogo sabado a
    // noite, ai passaria para domingo de manha".
    //
    // TRUE = como sempre foi, e é por isso que é o padrão: toda categoria que já existe no
    // banco continua podendo jogar a noite inteira de sábado. Desligada, a grade não marca
    // eliminatória DESTA categoria das 18h de sábado em diante, e o que sobra cai sozinho na
    // abertura do domingo.
    //
    // ⚠️ SÓ A ELIMINATÓRIA (decisão do Felipe): os jogos de GRUPO continuam entrando no sábado
    // à noite normalmente. A régua e o recorte por fase moram em Services/EliminatoriaNoSabado
    // e em GradeDeJogos.Encaixar.
    public bool EliminatoriaNoSabadoANoite { get; set; } = true;

    // ---- TRANSBORDO PRA SEDE EXTRA (08/09/2026) ----
    //
    // 🗣️ Pedido do Felipe: o torneio que cresceu aluga um local externo, e "vai ter q por [...]
    // quais categorias" jogam lá.
    //
    // ⚠️ NÃO É O `ClubeId` ACIMA, e a diferença é o assunto: `ClubeId` PRENDE a categoria
    // inteira num clube (o jeito do Dez E Batata — ninguém passa o dia indo e voltando). Esta
    // aqui é MOLE e é o jeito do Er: a sede principal enche, e o que não coube vai pro externo.
    // Categoria com `ClubeId` preenchido já está presa e não olha pra este campo.
    //
    // ⚠️ NASCE `true` — E ISSO NÃO É DESCUIDO. Antes desta coluna, categoria sem clube fixo
    // podia ser marcada em QUALQUER quadra do torneio. Nascer `false` prenderia, em silêncio,
    // todas as categorias soltas de todo torneio de duas sedes que já está no banco. O que é
    // NOVO pra todo mundo é só a preferência: a grade enche a sede principal primeiro (ver
    // Services/GradeDeJogos.Encaixar), que é o que se quer quando a outra é alugada por hora.
    public bool PodeJogarNaSedeExtra { get; set; } = true;
}
