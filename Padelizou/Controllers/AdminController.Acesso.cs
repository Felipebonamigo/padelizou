using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // A tela /Admin/Acesso: "não consigo entrar", respondido em uma busca.
    //
    // Nasceu de um caso real (18/08/2026): chegou um CPF no WhatsApp, a pessoa não sabia login,
    // e-mail nem senha, e a única forma de responder era abrir SSH e rodar SELECT no banco de
    // produção. Nenhuma tela do painel mostrava contato — a busca de /Admin/Organizadores acha
    // pelo CPF, mas imprime só o nome.
    //
    // ⚠️ TRÊS AÇÕES, E O E-MAIL NÃO É UMA DELAS (21/08/2026).
    //
    // A tela nasceu SÓ-LEITURA de propósito, e a regra que a mantinha assim continua de pé pro
    // e-mail: trocar o e-mail de uma conta é ENTREGAR a conta, calado — quem controla o e-mail
    // pede quantos "esqueci minha senha" quiser, pra sempre, e o dono nunca fica sabendo. Esse
    // campo segue sem formulário nenhum aqui.
    //
    // O que a tela passou a fazer é trocar o LOGIN, definir uma SENHA nova e DERRUBAR os logins
    // abertos — e as três são o oposto de um desvio silencioso:
    //
    //   · o login não é segredo — é o apelido com que a pessoa se identifica na entrada, e
    //     trocá-lo não abre porta nenhuma (a senha continua sendo a mesma);
    //   · a senha nova é BARULHENTA: no instante em que é definida, a antiga para de funcionar,
    //     e o dono descobre no primeiro login;
    //   · derrubar não dá acesso a ninguém — só TIRA, e de todo mundo ao mesmo tempo.
    //
    // De quebra, a senha aqui é a única saída do beco sem saída que a própria tela descreve —
    // tem senha, não tem e-mail, e "esqueci minha senha" não tem pra onde mandar link.
    //
    // A premissa do assistente do sistema (vê o painel inteiro e não muda nada) continua valendo
    // de graça: a trava é o VERBO HTTP, e ObterJogadorAdminAsync só devolve o assistente em GET.
    public partial class AdminController
    {
        // Quantas pessoas a busca por nome devolve antes de virar uma lista que ninguém lê.
        private const int LimiteDaBuscaDeAcesso = 10;

        [HttpGet]
        public async Task<IActionResult> Acesso(string? busca, int? jogadorId)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            // Quem só OLHA não vê os formulários. A recusa de verdade continua sendo o POST
            // (ObterJogadorAdminAsync devolve o assistente apenas em GET) — isto aqui é pra não
            // oferecer ao assistente um botão que só serve pra dizer não. Sai do BANCO, e não da
            // claim: o crachá no cookie só é reescrito quando a pessoa entra de novo.
            ViewBag.PodeEditar = PoderesNoSistema.PodeEditarTudo(admin);

            var vm = new AcessoDoJogadorVM
            {
                Busca = busca,
                Procurou = !string.IsNullOrWhiteSpace(busca) || jogadorId != null,
            };

            // Veio da lista de homônimos: a pessoa foi escolhida a dedo. Procurar de novo pelo
            // termo casaria com os mesmos e devolveria a lista, num laço sem saída.
            if (jogadorId is int escolhido)
            {
                vm.Achado = await _context.Jogadores.FindAsync(escolhido);
                return View(vm);
            }

            if (string.IsNullOrWhiteSpace(busca)) return View(vm);

            // A MESMA régua da tela de teste de aviso: login, e-mail, nome, apelido ou CPF
            // completo — o que o admin tiver na mão. Uma busca própria aqui divergiria da outra
            // em um mês, e "o CPF acha lá e não acha aqui" é um defeito mudo.
            var achados = await BuscaJogador.ParaAcaoAdministrativaAsync(
                _context, busca, LimiteDaBuscaDeAcesso);

            if (achados.Count == 1) vm.Achado = achados[0];
            else vm.Candidatos = achados;

            return View(vm);
        }

        // ── AS TRÊS AÇÕES ─────────────────────────────────────────────────────────────────
        //
        // Voltar pra ficha da pessoa, e não pra busca: o admin está no meio de um atendimento,
        // e cair na tela vazia obrigaria a procurar de novo pra conferir o que acabou de fazer.
        // Vai por `jogadorId` porque o termo que ele digitou pode ter sido o LOGIN — o antigo,
        // que a troca acabou de apagar, e que não acharia mais ninguém.
        private IActionResult VoltarParaFicha(int jogadorId, string? erro = null)
        {
            if (erro != null) TempData["Erro"] = erro;
            return RedirectToAction(nameof(Acesso), new { jogadorId });
        }

        // TROCAR O LOGIN de outra pessoa.
        //
        // O login é o identificador que ela digita na entrada e que os organizadores usam pra
        // achá-la. Ele nasce no cadastro e, até aqui, era PERMANENTE: nenhuma tela do sistema
        // gravava `Jogador.Login` depois — nem a de Editar Perfil. Quem escolheu às pressas, ou
        // digitou o nome do time em vez do próprio nome, ficava com aquilo pra sempre.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrocarLoginDoJogador(int jogadorId, string? login)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var alvo = await _context.Jogadores.FindAsync(jogadorId);
            if (alvo == null) return NotFound();

            // Conta excluída (LGPD) é anonimizada: nome, CPF, e-mail e login saíram a pedido
            // dela (ver ExclusaoDeConta.Anonimizar). Escrever um login aqui seria repor um
            // identificador em quem pediu pra deixar de ser identificável.
            if (alvo.Excluido)
                return VoltarParaFicha(jogadorId, "Essa conta foi excluída a pedido da pessoa (LGPD). "
                    + "Os identificadores dela foram apagados e não voltam.");

            login = (login ?? "").Trim();

            // A MESMA régua do cadastro, e não uma cópia parecida: mínimo de 4 e máximo de 30
            // (a coluna é varchar(30), e o Postgres RECUSA o que passa disso — não corta).
            if (IdentidadeJogador.ValidarLogin(login) is { } problema)
                return VoltarParaFicha(jogadorId, problema);

            // ⚠️ Contra os DOIS campos, e-mail e login (ver IdentidadeJogador): a entrada aceita
            // um ou outro na mesma consulta, então um login igual ao e-mail de outra pessoa
            // deixaria as duas contas ambíguas — e a vítima sem entrar e sem recuperar a senha.
            // Sem esta linha, o índice único do banco ainda seguraria metade dos casos, e a
            // outra metade (login = e-mail alheio) passaria direto.
            if (await IdentidadeJogador.EmUsoAsync(_context, login, alvo.Id))
                return VoltarParaFicha(jogadorId, $"Já existe outra conta atendendo por \"{login}\" "
                    + "(como login ou como e-mail). Escolha outro.");

            var anterior = string.IsNullOrWhiteSpace(alvo.Login) ? "(nenhum)" : alvo.Login;
            alvo.Login = login;
            await _context.SaveChangesAsync();

            // O login ANTIGO vai na confirmação de propósito: é o que o admin precisa pra
            // explicar a mudança pra pessoa — e pra desfazer, se trocou a conta errada.
            TempData["Sucesso"] = $"Login de {NomeBonito.Formatar(alvo.Nome)}: {anterior} → {login}. "
                + "A senha continua a mesma, e o e-mail e o CPF também servem pra entrar.";
            return VoltarParaFicha(jogadorId);
        }

        // DEFINIR UMA SENHA NOVA pra outra pessoa.
        //
        // É a saída do beco sem saída que esta tela descreve desde que nasceu: tem senha, não
        // tem e-mail, e "esqueci minha senha" não tem destinatário. Até aqui a única saída era
        // gravar um e-mail na conta alheia — que é justamente o caminho SILENCIOSO que a tela
        // se recusa a abrir. Definir uma senha temporária é o caminho barulhento: a senha antiga
        // morre na hora, e o dono descobre no primeiro login.
        //
        // A senha não é "consultada" nem "recuperada" — nem aqui, nem em lugar nenhum. O banco
        // guarda só o hash (PBKDF2 com sal, via IPasswordHasher). O que existe é definir uma nova.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefinirSenhaDoJogador(
            int jogadorId, string? senha, [FromServices] IPasswordHasher<Jogador> hasher)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return Forbid();

            var alvo = await _context.Jogadores.FindAsync(jogadorId);
            if (alvo == null) return NotFound();

            // ⚠️ ESTA É A TRAVA QUE IMPEDE ESCALAR PODER, e ela não é hipotética: definir a senha
            // de uma conta é ENTRAR nela. Sem esta linha, um administrador NOMEADO definiria a
            // senha do administrador RAIZ e entraria como ele — e o raiz é quem vê o dinheiro
            // (ver PoderesNoSistema.PodeVerDinheiro), justamente o que o nomeado não pode ver.
            // O mesmo vale pro assistente: só-leitura vira escrita total pela conta do outro.
            //
            // O raiz não cai aqui, e a própria conta também não: ninguém escala pra si mesmo.
            if (!admin.IsAdminRaiz && alvo.Id != admin.Id && PoderesNoSistema.PodeOlharTudo(alvo))
                return VoltarParaFicha(jogadorId,
                    "Essa conta administra o Padelizou. Só o administrador raiz define a senha dela.");

            if (alvo.Excluido)
                return VoltarParaFicha(jogadorId, "Essa conta foi excluída a pedido da pessoa (LGPD). "
                    + "Não há acesso a devolver: se ela quiser voltar, é conta nova.");

            // ⚠️ PRÉ-CADASTRO NÃO, e isso é o contrário de uma limitação: a conta existe porque
            // um ORGANIZADOR digitou o CPF dela numa inscrição — ela nunca esteve aqui, nunca
            // escolheu login e NUNCA ACEITOU OS TERMOS (o registro do aceite nasce no cadastro,
            // ver Jogador.TermosAceitosEm). Dar uma senha aqui fabricaria uma conta de gente que
            // não pediu conta nenhuma, e a LGPD põe em nós o ônus de provar o consentimento.
            // O caminho dela é o Cadastro com o mesmo CPF, que reivindica esta linha e traz o
            // histórico junto — é o que a própria tela já manda responder.
            if (alvo.EhPreCadastro)
                return VoltarParaFicha(jogadorId, "Essa conta nunca teve senha (pré-cadastro). "
                    + "O caminho dela é se cadastrar com o mesmo CPF — aí ela escolhe a própria "
                    + "senha e o histórico vem junto.");

            // A MESMA régua mínima da redefinição e do cadastro (ver RecuperacaoSenha).
            if (RecuperacaoSenha.ProblemaComASenha(senha) is { } problema)
                return VoltarParaFicha(jogadorId, problema);

            alvo.SenhaHash = hasher.HashPassword(alvo, senha!);

            // Mata qualquer link de "esqueci minha senha" que estivesse de pé nessa conta: se o
            // atendimento chegou até aqui, aquele link ou não chegou ou não era pra ela.
            //
            RecuperacaoSenha.Consumir(alvo);

            // ⚠️ E DERRUBA OS LOGINS JÁ ABERTOS. Andam JUNTOS de propósito: definir uma senha
            // nova sem derrubar o que está aberto fecharia a porta da frente sem tirar ninguém
            // de dentro — e o caso que traz alguém a esta tela é justamente esse, a conta que
            // outra pessoa está usando agora. Ver Services/SessoesAbertas.
            SessoesAbertas.Derrubar(alvo);
            await _context.SaveChangesAsync();

            // Avisa a pessoa que a senha DELA mudou, e por quem. É o que sustenta a decisão de
            // deixar isto ser feito daqui: a troca só é aceitável enquanto for barulhenta. Falha
            // de push não derruba a operação — ela já foi gravada, e a pessoa está no WhatsApp.
            try
            {
                await _pushNotificationService.EnviarParaJogadorAsync(alvo.Id,
                    "Sua senha do Padelizou foi alterada",
                    "O suporte definiu uma senha nova pra sua conta. Se não foi você que pediu, "
                    + "fale com a gente agora.",
                    Url.Action("AlterarSenha", "Auth"));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Falha ao avisar o jogador {JogadorId} da senha definida pelo admin.", alvo.Id);
            }

            _logger?.LogWarning("Admin {AdminId} definiu a senha do jogador {JogadorId}.", admin.Id, alvo.Id);

            TempData["Sucesso"] = $"Senha de {NomeBonito.Formatar(alvo.Nome)} definida, e os "
                + "aparelhos que estavam logados nessa conta foram desconectados. "
                + "Mande a senha pra ela e peça que troque em Perfil → Alterar senha: "
                + "senha que passou por terceiro deixou de ser secreta.";
            return VoltarParaFicha(jogadorId);
        }

        // DERRUBAR OS LOGINS ABERTOS, sem mexer na senha.
        //
        // É a metade de "definir uma senha nova" que às vezes é a única metade necessária: a
        // pessoa esqueceu a conta aberta no computador do clube, emprestou o celular, vendeu o
        // aparelho. A senha dela continua boa — o que sobra é uma sessão na rua, e um cookie
        // deste site vale até 90 dias renovando-se sozinho.
        //
        // ⚠️ Separado do botão de senha DE PROPÓSITO: obrigar o admin a trocar a senha pra
        // fechar uma sessão faria toda visita a esta tela terminar numa senha nova pra ditar no
        // WhatsApp — e a pessoa perderia o acesso que ela ainda tinha, por causa de um problema
        // que não era a senha.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DerrubarAcessoDoJogador(int jogadorId)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return Forbid();

            var alvo = await _context.Jogadores.FindAsync(jogadorId);
            if (alvo == null) return NotFound();

            // A MESMA trava de escalada do definir-senha, pelo motivo espelhado: derrubar não
            // dá acesso a ninguém, mas TIRA — e tirar o acesso de quem administra o sistema é
            // decisão de quem manda no sistema, não de um administrador nomeado.
            if (!admin.IsAdminRaiz && alvo.Id != admin.Id && PoderesNoSistema.PodeOlharTudo(alvo))
                return VoltarParaFicha(jogadorId,
                    "Essa conta administra o Padelizou. Só o administrador raiz derruba o acesso dela.");

            SessoesAbertas.Derrubar(alvo);
            await _context.SaveChangesAsync();

            _logger?.LogWarning("Admin {AdminId} derrubou as sessões do jogador {JogadorId}.", admin.Id, alvo.Id);

            TempData["Sucesso"] = $"Os logins abertos de {NomeBonito.Formatar(alvo.Nome)} caíram — "
                + "todos, em qualquer aparelho. A senha dela NÃO mudou: é com ela mesma que ela "
                + "entra de novo. Se a suspeita é de que alguém sabe a senha, defina uma nova aqui do lado.";
            return VoltarParaFicha(jogadorId);
        }
    }
}
