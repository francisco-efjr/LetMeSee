# Métodos de espelhamento e fronteiras de suporte

Decisão de produto e pesquisa: 2026-10-15. As rotas abaixo orientam a implementação; não são evidência de uma conexão real. O estado executado de cada cenário fica em [SCENARIOS.md](../qa/SCENARIOS.md).

## Matriz de decisão

| Origem → computador | Rota preferida | Alternativa | Condição que muda a decisão |
| --- | --- | --- | --- |
| Android → Windows | scrcpy por USB | scrcpy com dispositivo ADB já pareado na rede | USB evita depender da rede; Windows pode precisar do driver do fabricante. |
| Android → macOS | scrcpy por USB | scrcpy com dispositivo ADB já pareado na rede | macOS normalmente dispensa driver OEM; scrcpy/ADB ainda precisam estar disponíveis e verificados. |
| iPhone/iPad → Windows | AirPlay com o receptor UxPlay do LetMeSee | Sem alternativa USB implementada nesta versão | Requer rede local que permita descoberta e tráfego entre os dois dispositivos. |
| iPhone/iPad → macOS | AirPlay com o Receptor AirPlay nativo do macOS | QuickTime com cabo de dados | AirPlay evita o cabo em Mac compatível; USB atende o caso sem rede ou receptor nativo disponível. |

Android usa um motor diferente do AirPlay; instalar um não habilita o outro. O seletor de ambiente contém origem, computador e transporte. O app identifica o computador onde está executando e impede tentar iniciar um motor do outro sistema operacional. [scrcpy oficial](https://github.com/Genymobile/scrcpy), [drivers Android](https://developer.android.com/studio/run/oem-usb), [AirPlay Apple](https://support.apple.com/en-us/102661), [QuickTime Apple](https://support.apple.com/guide/quicktime-player/record-a-movie-qtp356b55534/mac).

## Checklist e tela de uso

O checklist prepara uma rota concreta. Não há aprovação universal "meu computador está configurado". O contexto da aprovação inclui ambiente, transporte e dispositivo escolhido; as confirmações ficam apenas na sessão atual. O app oferece explicações simples por padrão e detalhes opcionais para quem trabalha com TI. Os critérios de segurança e a verificação técnica são iguais para ambos.

Um item automático tem resultado pronto, pendente ou bloqueado, com uma ação concreta. Um item que exige observar o telefone diz explicitamente "confirme no dispositivo". Confirmação manual não converte motor ausente, hash inválido ou dispositivo não autorizado em sucesso. A disponibilidade da internet é independente da rede local: o espelhamento não deve exigir internet depois de preparar as dependências.

Depois da preparação, a superfície principal contém o seletor de ambiente e a ação Iniciar/Parar. Preferências, instalação e diagnóstico permanecem no assistente de preparação. Se uma condição obrigatória mudar, Iniciar abre a preparação correspondente; o app revalida o motor e a conexão imediatamente antes de executar.

## Android: USB e rede

O scrcpy suporta Android 5.0 ou superior; sua função é mostrar a tela, com controle opcional. O LetMeSee deve manter controle, gravação e microfone desativados por padrão. USB debugging e a autorização RSA no telefone são exigências distintas. Os drivers Windows vêm do fabricante, sem alterar arquivos de driver ou recomendar sites genéricos. [requisitos scrcpy](https://github.com/Genymobile/scrcpy#prerequisites), [autorização RSA](https://developer.android.com/studio/run/device#RSA), [drivers OEM](https://developer.android.com/studio/run/oem-usb).

Áudio é opcional: Android 10 ou anterior continua com vídeo; Android 11 precisa estar desbloqueado ao iniciar a captura; Android 12 ou superior não tem essa exigência específica. Uma falha de áudio deve oferecer continuidade sem áudio. Apps podem restringir a própria captura. [áudio scrcpy](https://github.com/Genymobile/scrcpy/blob/master/doc/audio.md). Janelas com `FLAG_SECURE` também podem impedir exibição em uma saída não segura; isso é proteção do conteúdo, sem promessa de contorno. [contrato Android](https://developer.android.com/reference/android/view/WindowManager.LayoutParams#FLAG_SECURE).

A descoberta consulta a lista ADB e escolhe um serial explicitamente. Sem dispositivo, não se presume defeito do cabo: orientam-se cabo de dados, desbloqueio, depuração e driver. `unauthorized` exige ação no telefone; `offline` exige reconectar e verificar novamente; vários aparelhos exigem escolha. A enumeração não comprova imagem recebida. [estados ADB](https://developer.android.com/tools/adb#devicestatus), [seleção scrcpy](https://github.com/Genymobile/scrcpy/blob/master/doc/connection.md#selection).

A rota sem fio inicial atende aparelhos que já aparecem como ADB autorizados. Não habilita silenciosamente `adb tcpip 5555`, não varre a rede, não guarda código de pareamento e não escolhe a primeira conexão disponível. Android 11+ permite pareamento com código/QR, mas implementar esse assistente é uma evolução própria; não confundir endereço/porta de pareamento com endereço/porta da conexão. ADB pode perder autorização ou mudar endereço. O uso de uma rede confiável deve ser confirmado de novo quando o contexto mudar. [conexão sem fio Android](https://developer.android.com/tools/adb#connect-to-a-device-over-wi-fi), [conexão scrcpy](https://github.com/Genymobile/scrcpy/blob/master/doc/connection.md#tcpip-wireless).

## iOS no Windows: AirPlay

O runtime fixado do LetMeSee contém UxPlay 1.73.6. Essa versão usa Bonjour/DNS-SD e GStreamer no Windows. Não aplicar ao pacote atual a novidade experimental de mDNS interno do upstream 1.74. Serviço Bonjour presente, porta disponível e interface de rede ativa são sinais locais; não comprovam descoberta a partir do iPhone. Conteúdo com DRM, como vídeo protegido do app Apple TV, não é suportado pelo UxPlay. [README da versão 1.73.6](https://github.com/FDH2/UxPlay/blob/v1.73.6/README.md).

O receptor fica parado até uma ação explícita. A política de firewall do LetMeSee limita-se ao executável, à sub-rede local, ao perfil privado e às portas configuradas. Não desligar firewall, VPN, proteção do Windows nem políticas corporativas. Se a organização bloquear descoberta ou tráfego local, informar a limitação e procurar o administrador.

O PIN do UxPlay com `-pin` pertence ao protocolo de pareamento: clientes podem reconhecer uma identidade já pareada. Um código novo na interface, sozinho, não prova autenticação por sessão. O LetMeSee usa identidade aleatória do receptor com `-m` ao executar para reduzir reutilização de confiança. Não promete exigir PIN a cada reconexão na mesma execução. A alternativa upstream `-pw` sem senha exige código a cada conexão, mas precisaria de captura confiável do código e não faz parte deste incremento. Não persistir PIN ou registro de clientes. [manual UxPlay 1.73.6](https://github.com/FDH2/UxPlay/blob/v1.73.6/uxplay.1).

## iOS no Mac: integração com apps do sistema

No QuickTime, o assistente orienta conectar o dispositivo, abrir Nova Gravação de Filme e escolher o iPhone/iPad conectado. O usuário apenas visualiza; gravar e salvar são ações adicionais deliberadas. Abrir QuickTime não comprova que a fonte foi escolhida. Cabo deve transportar dados, e o dispositivo pode pedir desbloqueio e confiança no computador. [QuickTime](https://support.apple.com/guide/quicktime-player/record-a-movie-qtp356b55534/mac), [reconhecimento USB e confiança](https://support.apple.com/en-us/108643).

AirPlay to Mac requer receptor habilitado. A compatibilidade nominal é macOS Monterey 12+ em iMac Pro, Mac mini 2020+ e demais Macs 2018+; dispositivos mais antigos podem ter suporte reduzido, conforme os ajustes. O LetMeSee deve orientar e abrir os ajustes adequados, sem alterar permissões por conta própria. O usuário escolhe o Mac na Central de Controle do iPhone. Uma confirmação manual do ajuste continua sendo uma confirmação, não inspeção automática do sistema. [compatibilidade e Receptor AirPlay](https://support.apple.com/en-us/108046#airplay), [procedimento no iPhone](https://support.apple.com/en-us/102661).

O app Espelhamento do iPhone da Apple é uma rota distinta e não é fallback automático: requer macOS 15+ com Apple silicon ou T2, iOS 18+, mesma Conta Apple com 2FA, Bluetooth/Wi-Fi, iPhone próximo e bloqueado; existem restrições regionais e de recursos simultâneos. Não é exigência para usar QuickTime ou AirPlay. [requisitos atuais da Apple](https://support.apple.com/en-us/120421).

## Estados, dados e encerramento

`Preparado` significa checklist satisfeito para aquele contexto. `Iniciando` significa verificação/execução em andamento. `Motor ativo` significa processo sob controle do LetMeSee, nunca prova de imagem recebida. Nas rotas Apple externas, mostrar "Continue no QuickTime" ou "Selecione este Mac no iPhone". O app não deve declarar que encerrou um espelhamento controlado pelo sistema sem observar esse encerramento.

Interrupção, suspensão, troca de ambiente, dispositivo ou rede invalidam a preparação afetada. Não iniciar automaticamente ao retomar o computador. Uma desconexão deve ser recuperável com orientação e nova tentativa. Stop encerra somente os processos pertencentes ao LetMeSee; não mata servidores ADB de outros programas nem apps externos inteiros.

Os detalhes do dispositivo e o código são dados temporários. Diagnóstico não inclui conteúdo da tela, segredos ou comandos arbitrários. O app não instala APK, não grava conteúdo e não envia telemetria como parte de preparar ou iniciar.

## Decisões céticas e limites

**Inferido:** USB como padrão Android reduz pré-condições de rede. Para iPhone no Mac, o padrão AirPlay usa o receptor do sistema quando compatível; QuickTime é a alternativa explícita para quem dispõe de cabo e precisa evitar a rede. Não construir agora servidor próprio de vídeo, conta/nuvem, troca automática entre motores, bypass de DRM, driver genérico ou pareamento Wi-Fi por varredura. Cada uma dessas extensões amplia a superfície de segurança e a matriz de falhas sem ser necessária para o fluxo solicitado.

**Não verificado:** interoperabilidade com aparelhos físicos, instalação Windows limpa, computadores gerenciados e autorização de rede macOS. Build, testes de domínio e prévia visual não substituem esses gates. Ver [matriz executável de cenários](../qa/SCENARIOS.md).
