# Segurança e privacidade

## Modelo de uso

O LetMeSee oferece aplicativos nativos para Windows 10/11 x64 e macOS 13+. Android usa scrcpy por USB ou Wi-Fi já pareado. iPhone/iPad usam AirPlay no Windows e recursos AirPlay/QuickTime do sistema no Mac. As rotas Apple no Mac são orientações, sem controle ou detecção da sessão. O uso sem fio pressupõe uma rede privada confiável. No Windows, a interface roda com `asInvoker`; elevar o aplicativo inteiro não é necessário. Instalação em Program Files, configuração de firewall e instalação explícita de Bonjour podem usar UAC. O aplicativo macOS não solicita elevação.

## Preparação antes de transmitir

O checklist é obrigatório e separado da tela principal. Modo guiado ou técnico muda a explicação, sem dispensar requisitos. Resultados automáticos e confirmações do usuário são distintos; uma confirmação não sobrepõe motor alterado, dispositivo não autorizado, conflito conhecido de porta ou perfil de rede incompatível.

As escolhas podem ser lembradas; um checklist concluído não é persistido. A preparação expira em cinco minutos, rejeita relógio voltando no tempo e é invalidada por troca de ambiente, parada/erro, suspensão e mudanças de rede observadas. Uma nova consulta precede o início. Não é possível detectar toda troca de Wi-Fi ou provar a veracidade de uma confirmação manual; o app não chama isso de evidência automática.

No Windows, Bonjour/firewall que não puderam ser consultados exigem revisão manual explícita. Ausência ou conflito conhecido não aceita esse atalho. Rede Pública é bloqueada; políticas de domínio e redes gerenciadas não são alteradas pelo app. USB Android é a alternativa que dispensa rede.

## Fronteiras e controles

| Fronteira | Controle implementado |
| --- | --- |
| Distribuição → máquina | URL/versão/SHA-256 fixados em `config/dependencies.json`; sem URL por variável de ambiente |
| ZIP → disco | Staging único; rejeição de traversal, caminhos absolutos, colisões e links; limites de quantidade/tamanho; rollback |
| Runtime → processo | Manifesto SHA-256; bloqueio de arquivos alterados, ausentes, links ou executáveis não listados |
| Processo → bibliotecas | Caminhos locais de GStreamer, isolamento de plugins/cache e remoção de variáveis herdadas de debug/config |
| UxPlay → preferências globais | Arquivo de opções vazio por sessão; não herda gravação/debug de `.uxplayrc` |
| Rede → receptor | Início explícito; código de pareamento AirPlay e identidade randomizada por execução; desligamento manual/por tempo. Não promete novo PIN a cada reconexão |
| Firewall → Windows | Regras por executável/porta, `Private` + `LocalSubnet`; sem modificar categoria de rede; remoção na desinstalação |
| Encerramento → processos | Identidade por sessão, cancelamento de início e Windows Job Object com término de descendentes ao fechar |
| Preferências Windows → dados | XML local pequeno, DTD e resolver desativados, validação e substituição atômica |

Android no Windows usa pacote oficial scrcpy 5.0.1 com SHA-256 fixado e inventário completo, incluindo `scrcpy-server`. Ele fica separado do AirPlay, em pasta do usuário. Android no Mac usa ferramentas existentes aprovadas por caminho canônico e hash; isso identifica os arquivos e não certifica suas bibliotecas ou origem inteira. Alterações invalidam a aprovação.

Os adaptadores Android usam argumentos diretos, sem shell, `--no-control` e `--no-clipboard-autosync`. O servidor ADB é próprio, em loopback/porta efêmera; o app não executa `kill-server` global, `adb tcpip`, abertura da porta 5555 ou pareamento automático. O componente `scrcpy-server` é enviado ao aparelho para a sessão. ADB pode gerenciar chaves de depuração no perfil do usuário; encerrar a sessão não revoga a confiança RSA concedida no celular. Desative depuração e revogue autorizações que não precisar mais, especialmente em computador compartilhado.

A ação explícita Configurar descoberta no Windows verifica o manifesto e uma assinatura Apple válida antes de abrir o instalador Bonjour. Ela pede UAC e deixa o usuário concluir o instalador; não acontece no startup ou silenciosamente no checklist.

O nome padrão é `LetMeSee`. A opção `-nh` impede acrescentar hostname ao anúncio. IPs e interfaces são mostrados localmente; não são exportados por padrão. O código de pareamento não é persistido nas preferências. A reconexão de um cliente confiável pode seguir regras do protocolo; não há garantia testada de novo pedido a cada conexão. Saída do motor é drenada para um buffer limitado em memória e apagada ao encerrar. O aplicativo não solicita gravação de mídia.

## Limites importantes

- A base binária existente é UxPlay 1.73.6 com dependências nativas. Fixar o hash preserva integridade e rastreabilidade; **não equivale a auditoria de CVEs** ou build reproduzível de cada biblioteca.
- O manifesto local detecta corrupção e alterações em arquivos. Quem consegue substituir o runtime **e** seu manifesto também pode contornar esse controle. A instalação em Program Files oferece a fronteira de permissões do Windows; a cópia portátil depende das permissões da pasta escolhida.
- Ainda não há assinatura Authenticode configurada para o executável/instalador. Antes de distribuição pública, validar no Windows e preparar assinatura com certificado do mantenedor.
- O código AirPlay é curto, temporário e compatível com o protocolo. Não transforma uma rede pública em ambiente confiável. Não há detecção implementada de cliente conectado ou seleção da interface de escuta pela UI.
- A recepção real, UAC, remoção das regras, áudio e DPI precisam de validação Windows com iPhone/iPad. A prévia HTML não testa esses controles.
- A consulta de Bonjour informa presença/estado, não instala serviços. Instalação de MSI de terceiros nunca acontece silenciosamente.
- O alto contraste é lido ao iniciar. Reinicie o aplicativo após mudar essa preferência do Windows.

## Relatar problemas

Não publique PINs, gravações de tela, endereços privados ou dados pessoais em issues. Para falhas sem dados sensíveis, descreva versão, passos mínimos, comportamento esperado e resultado, com dados de exemplo. Para vulnerabilidades, use os recursos de relato privado do repositório caso estejam habilitados; não há um canal privado adicional configurado nesta entrega.
