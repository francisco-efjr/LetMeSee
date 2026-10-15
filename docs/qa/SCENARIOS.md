# Matriz de preparação e espelhamento

Data: 2026-10-15. Escopo: Android/iOS, Windows/macOS, primeira utilização e ambiente preparado, pessoas com e sem experiência em TI. Esta matriz define critérios de aceitação e roteiro de validação. **Casos físicos abaixo estão pendentes de execução**, exceto quando uma evidência explicitamente registrar o contrário. Não interpretar os resultados de testes de domínio ou da prévia como teste de espelhamento.

Os métodos e fontes primárias estão em [METHODS.md](../architecture/METHODS.md). Estratégia: partições para plataforma e estado ADB, transições para preparação/início/parada, valores-limite para expiração e concorrência, e combinações dirigidas por risco. "Cada cenário possível" é um objetivo de cobertura contínua; a lista não pode provar todos os fabricantes, versões de sistema ou topologias de rede.

## Checklist antes de iniciar

| Item | Como verificar | Bloqueia quando | Recuperação apresentada |
| --- | --- | --- | --- |
| Ambiente correto | Sistema do computador + origem/transporte selecionados | Rota não executável no sistema atual | Escolher uma rota disponível neste computador. |
| Motor local | Arquivos e integridade para o motor escolhido | Ausente, alterado ou ilegível | Preparar apenas esse motor por ação explícita; nunca executar arquivo não verificado. |
| Aparelho Android | Nova consulta à lista ADB, estado e serial da rota escolhida | Zero, não autorizado, offline, não selecionado ou removido | Texto específico com próxima ação no telefone/cabo/driver. |
| Aparelho iOS | Instrução no telefone/QuickTime com confirmação explícita | Falta confirmação do passo exigido | Explicação curta e indicação do aplicativo que precisa de ação. |
| Cabo | Usuário confirma cabo de dados; descoberta confirma resultado | Rota USB sem aparelho disponível | Trocar porta/cabo; autorização nunca é presumida pela carga. |
| Rede para rota sem fio | Interface disponível + contexto privado/confiável confirmado + diagnóstico aplicável | Sem interface, perfil público ou condição obrigatória ausente | Rede particular ou rota USB, quando existente. |
| Descoberta AirPlay Windows | Bonjour da versão fixada + portas do receptor disponíveis | Serviço ou portas necessários indisponíveis | Preparação explícita ou diagnóstico; nenhuma mudança automática de segurança. |
| Permissões Apple | Passos nativos no dispositivo/Mac | Usuário não concluiu o passo exigido | Abrir ajustes/app e voltar para verificar. |
| Privacidade | Confirmação de que notificações e conteúdo visível foram revisados | Confirmação não foi feita | Lembrar que a tela inteira pode aparecer; não habilitar gravação. |
| Evidência atual | Contexto coincide com o aprovado + revalidação ao iniciar | Troca de rota/dispositivo/rede, suspensão ou prova vencida | Reabrir a parte relevante do checklist. |

Não há botão "ignorar todos os bloqueios". A experiência avançada exibe mais diagnóstico; não relaxa os critérios. Itens manuais devem ser rotulados como confirmação da pessoa. O checklist pode explicar um bloqueio sem alegar que inspecionou o telefone ou a rede remotamente.

## Caminhos principais

| ID | Cenário / pré-condição | Ação | Resultado observável esperado | Prioridade |
| --- | --- | --- | --- | --- |
| C01 | Windows limpo, Android USB, iniciante, nenhum motor | Selecionar ambiente e iniciar | Assistente mostra motor ausente e próximos passos em linguagem simples; não abre processo nem pede privilégios para espelhar. | Alta |
| C02 | Windows preparado, Android USB autorizado | Concluir preparação e iniciar | Inicia somente o serial aprovado; tela principal tem seletor de ambiente e Iniciar/Parar. | Alta |
| C03 | Mac limpo, Android USB, iniciante | Iniciar sem scrcpy disponível | Explica qual motor falta e onde prepará-lo; nenhum download/instalador automático. | Alta |
| C04 | Mac preparado, Android USB, pessoa de TI | Abrir detalhes, preparar, iniciar | Diagnóstico detalhado opcional; mesmas validações de C02. | Alta |
| C05 | Windows limpo, iPhone AirPlay | Abrir preparação | Mostra UxPlay/Bonjour/firewall como requisitos distintos; falha técnica impede início. | Alta |
| C06 | Windows preparado, iPhone AirPlay, rede particular | Preparar e iniciar receptor | Mostra nome/código de pareamento e instrução Central de Controle; estado diz aguardando dispositivo, sem afirmar imagem recebida. | Alta |
| C07 | Mac com iPhone e cabo de dados, sem Wi-Fi | Escolher QuickTime e iniciar | Abre/orienta QuickTime; informa seleção manual da fonte e que não é preciso gravar para visualizar. | Alta |
| C08 | Mac compatível e receptor AirPlay configurado | Selecionar AirPlay e iniciar | Orienta escolher o Mac no iPhone; não exige mesma Conta Apple como regra universal do AirPlay. | Alta |
| C09 | Mac com receptor nativo indisponível/desativado | Escolher AirPlay | Orienta compatibilidade/ajustes e oferece rota QuickTime; não declara disponibilidade só pelo macOS. | Alta |
| C10 | Usuário experiente troca para modo guiado | Reabrir preparação | Mantém seleção útil; instruções ficam simples sem converter falhas em sucesso. | Média |
| C11 | Usuário iniciante, ambiente já preparado | Reabrir app e iniciar | Reutiliza preferências, mas verifica novamente; não reaproveita silenciosamente consentimento de privacidade. | Alta |
| C12 | Linux ou outro host não suportado | Tentar iniciar rota Windows/Mac | Mensagem clara de ambiente não suportado; não executa motor incompatível. | Alta |

## Android e USB: negativos isolados

| ID | Dado / falha introduzida | Resultado esperado | Prioridade |
| --- | --- | --- | --- |
| A01 | Cabo apenas de carga, restante preparado | Sem dispositivo ADB: orientar cabo de dados, depuração e driver como possibilidades; não afirmar causa certa. | Alta |
| A02 | Um serial com estado `unauthorized` | Início bloqueado; pedir desbloqueio e autorização RSA no aparelho. Marcar checkbox não supera o bloqueio. | Alta |
| A03 | Um serial com estado `offline` | Início bloqueado com reconexão/verificação; não fazer `kill-server` global automaticamente. | Alta |
| A04 | Zero dispositivos | Início bloqueado e nova verificação disponível. | Alta |
| A05 | Dois Androids autorizados na mesma rota | Escolha obrigatória; nunca usar o primeiro item implicitamente. | Alta |
| A06 | Um Android USB + um Android por rede | A rota USB só lista/usa USB, a de rede só rede; serial passado explicitamente. | Alta |
| A07 | Apenas emulador ou lista ADB malformada | Não tratar como celular físico autorizado da rota; nenhuma execução com serial vazio. | Alta |
| A08 | Aparelho removido depois do checklist | Revalidação antes de iniciar invalida a preparação e explica a remoção. | Alta |
| A09 | Usuário revoga autorização no telefone | Nova consulta não passa; orienta reautorizar sem apagar confiança de outros dispositivos automaticamente. | Alta |
| A10 | Android 10, áudio solicitado | Vídeo pode continuar; interface explica áudio indisponível nessa versão. | Média |
| A11 | Android 11 bloqueado ao iniciar | Explica desbloqueio para áudio ou continuidade sem áudio; não promete captura. | Média |
| A12 | Android 12+ com áudio, app protegido | Distingue restrição do conteúdo de falha geral; não sugere bypass de captura/DRM. | Alta |
| A13 | Windows sem driver OEM e sem administrador | Orienta driver oficial/admin da organização; não modifica INF nem eleva o app inteiro. | Alta |
| A14 | Nome do dispositivo com acento/aspas; serial inválido | Exibe nome como texto e rejeita serial inválido; nenhum comando de shell é montado a partir do nome. | Alta |

## Rede, permissões e conteúdo

| ID | Dado / falha introduzida | Resultado esperado | Prioridade |
| --- | --- | --- | --- |
| N01 | Android Wi-Fi ainda não pareado | Não habilita ADB TCP/IP nem procura IPs; instrui pareamento oficial ou USB. | Alta |
| N02 | Android já pareado, endpoint ausente/offline | Preparação bloqueada até nova enumeração autorizada; não usa IP anterior como prova. | Alta |
| N03 | Wi-Fi público no Windows | Não habilita regra de firewall pública; orienta rede privada confiável/USB quando houver. | Alta |
| N04 | Wi-Fi convidado com isolamento entre clientes | Não declara sucesso por ambos usarem o mesmo SSID; orienta rede que permita comunicação local. | Alta |
| N05 | VPN ou computador corporativo bloqueia descoberta | Diagnóstico acionável; não desliga VPN, firewall, antivírus ou gerenciamento. | Alta |
| N06 | Computador via Ethernet e celular Wi-Fi na mesma LAN | Não bloqueia apenas por não ser Wi-Fi no computador; solicita/verifica comunicação local aplicável. | Média |
| N07 | Sem internet, motores já preparados | Android USB e QuickTime USB continuam disponíveis; AirPlay depende da LAN, não da internet. | Alta |
| N08 | Windows, Bonjour ausente/parado | Receptor bloqueado com preparação explícita; nenhum MSI é executado automaticamente. | Alta |
| N09 | Uma das portas TCP/UDP do receptor ocupada | Início bloqueado com diagnóstico; não mata outro programa nem troca porta sem aplicar política consistente. | Alta |
| N10 | iPhone não confia no Mac por USB | Orienta desbloquear/confiar no aparelho, sem enviar código/senha ao LetMeSee. | Alta |
| N11 | MDM impede AirPlay/captura/depuração | Mostra limitação e necessidade de administrador; não tenta contornar política. | Alta |
| N12 | Apple TV/serviço com DRM; Android `FLAG_SECURE` | Tela preta/restrição descrita como limite do conteúdo; não classificada automaticamente como conexão interrompida. | Alta |
| N13 | Cliente AirPlay anteriormente pareado | Testar Stop/Start e reconexão sem código; confirmar efeito do ID novo. Sem teste físico, não prometer autenticação por sessão. | Alta |
| N14 | Dois iPhones tentam a mesma execução UxPlay | Segundo não derruba o primeiro por política automática de tomada; controle fica explícito. | Alta |

## Estado, acessibilidade e recuperação

| ID | Sequência | Resultado esperado | Prioridade |
| --- | --- | --- | --- |
| S01 | Aprovar USB → mudar para AirPlay | Confirmações específicas da rota anterior deixam de autorizar início. | Alta |
| S02 | Aprovar aparelho A → escolher B | Exige preparação atual de B; Start recebe serial de B. | Alta |
| S03 | Aprovar → suspender/retomar computador | Não reinicia sozinho; nova preparação/revalidação antes de iniciar. | Alta |
| S04 | Aprovar rede → trocar rede | Consentimento e diagnóstico de rede anteriores não bastam para início. | Alta |
| S05 | Aprovar → aguardar além da validade definida | Preparação expira de forma explícita e verificável; nunca restaura via relógio anterior salvo. | Alta |
| S06 | Duplo clique em Iniciar | No máximo um motor pertencente à sessão; UI indica operação em andamento. | Alta |
| S07 | Iniciar → Parar durante verificação de integridade | Nenhum motor inicia depois de Parar; retorno tardio não publica estado ativo. | Alta |
| S08 | Motor encerra ou cabo cai durante uso | Estado acionável de interrupção; não mantém indicação falsa de conectado; nova tentativa disponível. | Alta |
| S09 | Parar/fechar app durante motor próprio | Encerra apenas processos pertencentes ao app; nenhuma janela órfã ou gravação residual. | Alta |
| S10 | Parar rota QuickTime/AirPlay nativo | Instrui encerrar no app/iPhone responsável; não afirma que interrompeu sem comprovação. | Alta |
| S11 | Tab/Shift+Tab/Enter/Escape sem mouse | Ordem de foco lógica, rótulos completos e recuperação do foco após assistente. | Alta |
| S12 | VoiceOver/Narrator, zoom/DPI alto, contraste | Estado não depende só de cor; rótulos e falhas legíveis; seletor e ação principal acessíveis. | Alta |
| S13 | Preferências ausentes, antigas ou corrompidas | Recupera defaults seguros e pede preparação atual; não trava ou inicia automaticamente. | Alta |
| S14 | Reabrir app depois de encerrar sessão | PIN, serial consentido e confirmação de privacidade não constituem aprovação persistida. | Alta |
| S15 | Dependência alterada após primeira preparação | Verificação antes da execução falha; nenhum executável alterado inicia. | Alta |

## Evidência mínima e critério de distribuição

Registrar para cada execução: ID, data, commit, versão do LetMeSee/motor, modelo/versão do celular, SO/arquitetura do computador, perfil de rede, papel da pessoa (guiado/avançado), observado, resultado e evidência local sem conteúdo privado. Anotar primeiro erro; uma repetição com sucesso não apaga a falha inicial.

Antes de distribuir, executar ao menos C01–C09 em hardware real e máquina/usuário sem configuração anterior, mais A02–A09, N03–N05, N08–N13 e S01–S10. Validar instalação, atualização e remoção com conta padrão. Assinatura/notarização, teste de firewall/UAC, VoiceOver/Narrator e iPhone/Android físicos continuam gates separados. Não declarar cobertura física por CI verde.

Em revisão independente, questionar três falsos positivos frequentes: processo vivo não significa imagem recebida; checkbox não demonstra permissão do telefone; internet ou interface ativa não demonstra comunicação entre dispositivos. O gate bloqueia quando falta uma condição que o app realmente exige e rotula com honestidade o que só a pessoa pode observar.
