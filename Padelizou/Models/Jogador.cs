using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Jogador")]
public partial class Jogador
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    // Como a pessoa é chamada na quadra. Opcional — muita gente do padel é conhecida só
    // pelo apelido, e ninguém acha "José Carlos" procurando por "Zeca".
    public string? Apelido { get; set; }

    // Quando o nome e o apelido foram trocados pela última vez. NULO = nunca trocou desde o
    // cadastro, e a primeira troca é livre (ver Services/TrocaDeNome): quem digitou errado na
    // pressa não pode ficar seis meses preso ao erro.
    //
    // A trava existe porque o nome é como as pessoas te acham: lista de inscritos, placar da
    // mesa, ranking, histórico de torneio. Trocar toda semana faz o parceiro de terça não
    // reconhecer quem entrou na dupla dele.
    public DateTime? NomeAlteradoEm { get; set; }
    public DateTime? ApelidoAlteradoEm { get; set; }

    public string Cpf { get; set; } = null!;

    // Recuperação de senha: token de uso único, com validade curta. Guardado em claro
    // porque só vale por uma hora e não abre nada sozinho — quem o tem ainda precisa
    // definir uma senha nova, e o token morre no ato.
    public string? TokenRecuperacao { get; set; }
    public DateTime? TokenRecuperacaoExpiraEm { get; set; }

    // ---- Exibição ----
    // Três formas, porque o contexto mudou o que é útil:

    // O nome completo com a caixa arrumada. Cada um digita como quer — na mesma lista
    // conviviam "ALAN DA SILVEIRA MACHADO" e "charls gustavio polese" — e a coluna `Nome`
    // continua guardando exatamente o que a pessoa escreveu. Ver Services/NomeBonito.
    [NotMapped]
    public string NomeNaTela => Padelizou.Services.NomeBonito.Formatar(Nome);

    // Lista, placar, check-in, chave, painel: **primeiro nome + último sobrenome (apelido)**.
    //
    // "Anderson Matteus Schwaab" vira "Anderson Schwaab"; com apelido "Deco", vira
    // "Anderson Schwaab (Deco)". O nome inteiro numa tabela de grupo empurra a linha pra três
    // alturas ou é cortado no meio, sobrando justo o nome do meio — que é o que menos
    // identifica alguém. Ver NomeBonito.ComApelido.
    //
    // Até 06/08/2026 quem tinha apelido aparecia SÓ pelo apelido, e isso escondia a pessoa:
    // "Zeca" pode ser três num mesmo torneio.
    [NotMapped]
    public string ComoChamar => Padelizou.Services.NomeBonito.ComApelido(Nome, Apelido);

    // Perfil e resultado de busca: o nome completo com o apelido ao lado, pra ligar
    // "quem eu procurei" com "quem eu conheço".
    [NotMapped]
    public string NomeComApelido => string.IsNullOrWhiteSpace(Apelido)
        ? NomeNaTela
        : $"{NomeNaTela} ({Padelizou.Services.NomeBonito.Formatar(Apelido)})";

    public string? Login { get; set; }

    // Quando a pessoa pediu a exclusão da conta (LGPD). A linha CONTINUA existindo, com os
    // dados pessoais raspados — apagar de verdade é impossível e seria errado:
    //   · "Pagamento.JogadorId" é ON DELETE CASCADE, e apagar a conta levaria junto o
    //     registro fiscal que o MEI obriga a guardar;
    //   · "Dupla.Jogador1Id" é NO ACTION, então o banco RECUSA apagar quem já jogou;
    //   · o resultado de um jogo é dado de quatro pessoas, não de uma.
    // A LGPD permite guardar o que é obrigação legal e o que toca direito de terceiro —
    // o que ela exige é que a pessoa deixe de ser identificável, e é isso que se faz.
    public DateTime? ExcluidoEm { get; set; }

    [NotMapped]
    public bool Excluido => ExcluidoEm != null;

    public string? Codigo { get; set; }

    public virtual ICollection<Dupla> DuplaJogador1s { get; set; } = new List<Dupla>();

    public virtual ICollection<Dupla> DuplaJogador2s { get; set; } = new List<Dupla>();
    public string? Celular { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }

    // ---- Endereço (opcional, preenchido pelo CEP) ----
    // O CEP existe aqui por causa da CIDADE: digitada à mão, ela vira "GRAVATAI", "Gravatai" e
    // "gravatai" — três cidades no filtro do ranking. Vindo do CEP, ela vem escrita de um jeito
    // só, e ainda por cima do jeito certo (ver Services/ConsultaDeCep).
    //
    // Só dígitos, como CPF e celular (Documentos.SomenteDigitosOuNulo). Nulo = não informou —
    // o campo é opcional de verdade e o resto do cadastro nunca depende dele.
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Bairro { get; set; }

    // Rua e bairro no perfil público. ⚠️ Nasce FALSO de propósito: endereço é dado pessoal, e
    // quem já tem conta não pediu pra publicar o seu — ligar por padrão publicaria a rua de
    // todo mundo sem ninguém ter tocado em nada.
    //
    // Não cobre cidade/UF: essas já aparecem hoje na busca e no ranking, e são o que faz o
    // filtro por região funcionar. O que este campo decide é o que é NOVO aqui.
    public bool EnderecoPublico { get; set; }
    public string? Email { get; set; }
    public string? SenhaHash { get; set; }

    // NUNCA ENTROU AQUI: a linha nasceu de um TERCEIRO (parceiro de dupla inscrito por CPF,
    // parceiro do Ranking liberado pelo painel) e a pessoa ainda não assumiu a conta. Sem
    // senha não há login, e sem login ela não tem como mexer em preferência nenhuma — nem
    // pra esconder o próprio telefone, que outra pessoa digitou por ela.
    //
    // É por isso que a régua de contato (Services/ContatoDoJogador) olha para cá: enquanto
    // for pré-cadastro, o contato fica fechado. Quem se cadastra com aquele CPF define a
    // senha, assume a conta (ver AuthController) e a partir daí manda na própria escolha.
    //
    // Conta EXCLUÍDA também fica sem SenhaHash (ver Services/ExclusaoDeConta) — por isso o
    // `!Excluido`: quem saiu não é gente esperando entrar, e quem decide por ela é `ExcluidoEm`.
    public bool EhPreCadastro => string.IsNullOrEmpty(SenhaHash) && !Excluido;

    public string? FotoPerfil { get; set; }
    public string? Instagram { get; set; }
    // CAMPO MORTO — não é o ranking. Nunca foi alimentado pelo sistema (os valores que
    // existem em produção vieram de SQL manual antigo). Os pontos reais são calculados a
    // partir das fases alcançadas: use IEstatisticasService.ObterPontosPorJogadorAsync ou
    // ObterResumoJogadorAsync. A coluna fica só pra não exigir migração destrutiva.
    [Obsolete("Use IEstatisticasService para pontos reais. Este campo não reflete o ranking.")]
    public int PontuacaoGlobal { get; set; }
    public bool IsProfessor { get; set; } // <- A nova Flag!

    // ---- Política de cancelamento do professor ----
    // Quantas horas antes o aluno precisa avisar pra não ser cobrado. 0 = sem prazo
    // (pode desmarcar até a última hora). Só faz sentido quando IsProfessor.
    public int HorasMinimasCancelamento { get; set; } = 24;

    // Cobra quem falta sem avisar (no-show) ou avisa fora do prazo?
    public bool CobraFaltaSemAviso { get; set; }

    // Texto livre que aparece pro aluno na hora de marcar e na página pública.
    public string? PoliticaCancelamentoTexto { get; set; }

    // O professor escolhe se a página pública dele aceita COMENTÁRIO nas avaliações.
    // A NOTA não se desliga — nota é o dado que protege o próximo aluno; o texto é vitrine,
    // e vitrine é do dono. Desligado: novos comentários não são aceitos e os antigos saem
    // de exibição (ficam guardados — religou, voltam).
    public bool DepoimentosDeAulaHabilitados { get; set; } = true;

    // ---- Página pública do professor ----
    public string? ApresentacaoProfessor { get; set; }   // "sobre mim", o que vende a aula
    public string? ExperienciaProfessor { get; set; }    // formação/tempo de quadra

    // "Esquerda" / "Direita" / "Ambos"
    public string? LadoQuadra { get; set; }

    // "Destro" / "Canhoto"
    public string? Lateralidade { get; set; }

    // O sexo com que a pessoa se identifica: "Masculino" ou "Feminino" (Felipe, 08/08/2026).
    //
    // Existe por UMA razão concreta: a Mista e a Casais são categorias de um homem e uma
    // mulher, e até aqui isso era honra — o sistema não tinha como saber, nem pra avisar.
    //
    // ⚠️ ANULÁVEL, e isso não é frouxidão: os 90 jogadores de produção nasceram antes do campo
    // existir, e todo PRÉ-CADASTRO (parceiro inscrito pelo CPF, sem conta) chega sem ninguém
    // pra responder. Não-anulável obrigaria a inventar um valor pra gente real — e inventar
    // sexo alheio é pior que não saber. Nulo quer dizer "ainda não informou", e é isso que a
    // tela pede. Ver Services/SexoDoJogador.
    public string? Sexo { get; set; }

    // Se true, visitantes só veem foto e nome no perfil público (Jogadores/Perfil)
    public bool PerfilPrivado { get; set; }
    public bool NotificarEmail { get; set; }
    public bool NotificarWhatsApp { get; set; }

    // Controla só o convite avulso pra jogo de grupo (Grupos.Convidar) — separado de
    // NotificarWhatsApp, que é o canal geral de aviso (ex: aula).
    public bool AceitaConvitesJogo { get; set; } = true;

    public virtual ICollection<JogadorCategoria> JogadorCategorias { get; set; } = new List<JogadorCategoria>();
    public virtual ICollection<JogadorClube> JogadorClubes { get; set; } = new List<JogadorClube>();
    public virtual ICollection<JogadorDiaHorario> JogadorDiasHorarios { get; set; } = new List<JogadorDiaHorario>();

    // Quais categorias aparecem na Minha Agenda unificada
    public bool AgendaMostrarJogosSemanais { get; set; } = true;
    public bool AgendaMostrarTorneios { get; set; } = true;
    public bool AgendaMostrarAulas { get; set; } = true;
    public bool AgendaMostrarAlunos { get; set; } = true;
    public bool AgendaMostrarMarcacoes { get; set; } = true;

    // Token opaco usado na URL do feed .ics de assinatura de agenda (sem exigir login)
    public Guid AgendaFeedToken { get; set; } = Guid.NewGuid();

    // Administrador raiz: só o dono do app (definido 1x direto no banco, nunca por tela).
    // Só quem tem essa flag pode gerenciar a lista de IsAdminGeral.
    public bool IsAdminRaiz { get; set; }

    // Administrador do sistema nomeado pelo raiz (por CPF/login) — hoje só gerencia donos de
    // clube, mas é a fundação pra outras telas administrativas futuras.
    public bool IsAdminGeral { get; set; }

    // ASSISTENTE DO SISTEMA: enxerga tudo, não muda nada.
    //
    // As duas flags acima significam ver E editar ao mesmo tempo — elas viram a credencial
    // única `IsAdmin`, que destrava desde o painel /Admin até o placar de qualquer torneio.
    // O assistente é a terceira resposta que faltava: alguém que acompanha a operação inteira
    // sem poder mexer nela.
    //
    // ⚠️ Ela é ADITIVA e só pra leitura: NÃO entra na credencial `IsAdmin`, justamente pra
    // que nenhum caminho de escrita se abra por acidente. Quem é assistente E organizador de
    // um torneio continua editando o torneio dele pelo direito de organizador — a flag não
    // tira poder de ninguém. Ver Services/PoderesNoSistema.
    public bool IsAssistente { get; set; }

    // PARCEIRO DO RANKING: vê UMA tela do painel — o relatório da parceria — e mais nada.
    //
    // ⚠️ Esta é a única flag do sistema dada a gente de FORA da casa (hoje, o pessoal do
    // Ranking Brasil). Por isso ela não é uma versão mais fraca do assistente: o assistente
    // enxerga o painel inteiro, incluindo o financeiro de todas as frentes, e nada disso é da
    // conta de um parceiro comercial. Ela não entra em `PodeOlharTudo` nem em `PodeEditarTudo`
    // — abre uma porta só, e essa porta é um GET sem formulário nenhum.
    //
    // Ver Services/PoderesNoSistema e Services/RelatorioDoRankingRs.
    public bool IsParceiroRanking { get; set; }

    // Pode CRIAR torneio. Nasce desligado: desde 07/08/2026 criar torneio é liberado pessoa a
    // pessoa, no painel admin (ver Services/PermissaoDeOrganizador).
    //
    // Por que não é aberto a todo mundo: cada torneio criado dispara push e e-mail pra base
    // inteira. Vinte torneios falsos não sujam uma lista — viram milhares de avisos, e o
    // jogador que recebe isso desinstala o app. A porta ficou estreita antes de o problema
    // aparecer, não depois.
    //
    // ⚠️ Isto NÃO é o mesmo que organizar um torneio específico: quem é adicionado como
    // co-organizador de um torneio alheio continua entrando por `TorneioOrganizador`, sem
    // precisar deste perfil. Aqui é só o direito de ABRIR um torneio novo.
    public bool IsOrganizadorTorneio { get; set; }

    // ── O PEDIDO DESSE PERFIL, FEITO DENTRO DO APP ──────────────────────────────────────
    //
    // Antes a recusa dizia "fale com a gente pelo WhatsApp". Funciona, e vaza gente: quem
    // topou criar um torneio às 23h de um domingo não vai abrir conversa com desconhecido —
    // fecha o app e não volta. O pedido de um toque transforma a recusa em fila, e a fila é
    // exatamente o que o Felipe quer ver.
    //
    // Sem tabela nova de propósito: é UM pedido por pessoa, sem histórico que alguém vá ler.
    // Uma tabela aqui seria estrutura a mais pra guardar no máximo duas datas.
    public DateTime? SolicitouOrganizadorEm { get; set; }

    // Por que quer — opcional, e o que faz a diferença na hora de decidir: "sou professor no
    // Batata Padel e faço interno todo mês" resolve o pedido em cinco segundos.
    public string? MotivoDoPedidoDeOrganizador { get; set; }

    // ⚠️ Recusa é DATA, não booleano: o admin precisa distinguir "nunca pediu" de "pedi e me
    // disseram não". E a pessoa recusada pode pedir de novo (o pedido novo limpa esta data) —
    // um "não" de hoje não é sentença perpétua; o clube dela pode crescer no mês que vem.
    public DateTime? PedidoDeOrganizadorRecusadoEm { get; set; }

    // Preferências de notificação por tipo de aviso (independentes do canal NotificarEmail/
    // NotificarWhatsApp, que definem COMO recebe; estas definem O QUE recebe).
    public bool NotificarTorneiosAbertos { get; set; } = true;
    public bool NotificarSeguidosTorneio { get; set; } = true;

    // Gate do Avisos ("Buscar Jogo") existente — antes só dependia de NotificarEmail/WhatsApp.
    public bool NotificarAvisoJogo { get; set; } = true;
    public bool NotificarJogoAula { get; set; } = true;
    public bool NotificarRaqueteLivre { get; set; } = true;

    // Default false de propósito (diferente das outras 5 flags acima, que são opt-out) — sem
    // cidade marcada em JogadorCidades não faz sentido notificar "da região".
    public bool NotificarHorarioVagoRegiao { get; set; }
    public virtual ICollection<JogadorCidade> JogadorCidades { get; set; } = new List<JogadorCidade>();

    // Lista de aulas onde ele é o PROFESSOR
    [InverseProperty("Professor")]
    public virtual ICollection<Aula> AulasDadas { get; set; } = new List<Aula>();

    // Lista de aulas onde ele é o ALUNO
    [InverseProperty("Aluno")]
    public virtual ICollection<Aula> AulasRecebidas { get; set; } = new List<Aula>();
    public int? TimeId { get; set; } // O "?" permite que ele fique sem time (null)
    public virtual Time? Time { get; set; }

    // ---- Recebimento pelo Padelizou (opt-in de quem organiza torneio ou dá aula) ----
    // Fica no Jogador porque tanto o dono do torneio (TorneioOrganizador com NivelAcesso
    // "Criador") quanto o professor de um JogoAula são Jogadores.

    // Desligado = cobra a inscrição por fora e o app não gera cobrança nenhuma.
    public bool ReceberPagamentoOnline { get; set; }

    // Wallet da conta Asaas dele — pra onde o split manda a fatia da inscrição. Pode ser
    // conta de pessoa física; não precisa de CNPJ.
    public string? AsaasWalletId { get; set; }

    // Escolha dele sobre quem paga a comissão do Padelizou:
    // "Somada"     -> soma ao valor do jogador, e ele recebe a inscrição cheia.
    // "Descontada" -> sai da fatia dele, e o jogador paga só o valor anunciado.
    // Nulo = usa o padrão de AsaasSettings.
    public string? ModoComissao { get; set; }

    // Nulo = cadastrado antes de 25/07/2026 (quando a coluna nasceu) — usado nas métricas
    // de uso do admin (cadastros por semana).
    public DateTime? CriadoEm { get; set; } = DateTime.Now;

    // ---- Plano do professor (Services/PlanoDoProfessor) ----
    // Nulo = ainda não escolheu. "Assinante" = mensalidade + taxa menor por aula.
    // "Avulso" = sem mensalidade, taxa cheia por aula.
    public string? PlanoProfessor { get; set; }

    // Início dos 15 dias de teste com condições de assinante. Marcado na primeira visita
    // ao painel do professor — não no cadastro, senão o relógio corre antes de ele saber.
    public DateTime? TesteProfessorInicio { get; set; }

    // Até quando a mensalidade de assinante está quitada. Nulo = nunca pagou.
    public DateTime? AssinaturaProfessorPagaAte { get; set; }

    // Último estágio de aviso do plano já enviado (ver Services/AvisosDoPlanoDoProfessor).
    // Guarda os DOIS mundos: a faixa 1x é o fim do teste, a 2x é o vencimento da assinatura.
    //
    // ⚠️ VOLTA PRA NULO a cada pagamento confirmado, e é isso que faz o ciclo seguinte avisar
    // de novo — sem o zeramento, o professor seria lembrado uma vez na vida.
    //
    // O nome ficou de quando só existia o aviso de assinatura; renomear a coluna custaria uma
    // migration só pra trocar palavra, e o comentário aqui é mais barato que o downtime.
    public int? UltimoLembreteDeAssinatura { get; set; }

    // ---- Padelímetro (Services/Padelimetro + RANKING.md) ----
    // O nível de habilidade, 0–1000. Nulo = nunca teve partida contada — o número NASCE
    // na primeira partida de torneio, com o valor de entrada da categoria em que ela foi
    // jogada. Quem escreve aqui é só o PadelimetroService (gancho do FinalizarPartida e
    // replay); o extrato de cada movimento vive em HistoricoDePadelimetro.
    public int? Padelimetro { get; set; }

    // Quantas partidas já contaram. Menos de 10 = "em calibração" (K dobrado, o número
    // anda mais rápido até se encontrar).
    public int JogosDePadelimetro { get; set; }

    // Quando o app foi aberto instalado (tela de início) pela primeira vez. Carimbado pelo
    // próprio navegador, que é o único que sabe: o servidor vê a mesma requisição instalado
    // ou não.
    //
    // Existe porque "instalou" e "aceitou notificação" são coisas diferentes, e os Primeiros
    // Passos confundiam as duas — quem instalava sem liberar aviso continuava sendo cobrado
    // pra instalar o que já tinha instalado.
    public DateTime? InstalouAppEm { get; set; }
}
