# LetMeSee para macOS

Aplicativo nativo SwiftUI, macOS 13 ou posterior, sem runtime web ou dependências Swift externas. O checklist abre antes da tela de espelhamento. Depois da preparação, a janela tem duas ações: **Ambiente** e **Começar a espelhar**. Durante scrcpy, a segunda ação para o processo; nas rotas Apple, conclui a orientação.

## Compilar localmente

Use o Xcode/Command Line Tools já instalado. O script não instala SDK, Homebrew, executáveis ou certificados.

```sh
zsh apps/macos/test.sh
zsh apps/macos/build.sh
open apps/macos/build/LetMeSee.app
```

O build tem a arquitetura do Mac atual e não tem assinatura de distribuição ou notarização. A versão mínima do sistema não comprova compatibilidade do hardware com AirPlay Receiver. Não desative o Gatekeeper para distribuir o aplicativo; assinatura, notarização e testes com hardware são gates de distribuição pendentes.

## Rotas e limites reais

| Aparelho | Recomendado | Alternativa | O que o app comprova |
| --- | --- | --- | --- |
| Android | USB com scrcpy e adb instalados por você | Android 11+ sem fio já pareado, visível ao servidor privado | Identidade dos arquivos aprovados, versões, dispositivo selecionado/autorizado e processo scrcpy. Não comprova quadros recebidos. |
| iPhone / iPad | AirPlay do macOS em Mac compatível | USB com prévia do QuickTime | Disponibilidade local do QuickTime. AirPlay, rede, cabo e autorizações são confirmações do usuário. Não detecta sessão Apple. |

No Android, o app procura ferramentas em `/opt/homebrew/bin` e `/usr/local/bin`, ou permite selecionar os arquivos instalados. Antes da primeira execução, mostra caminho canônico e SHA-256 de **adb, scrcpy e scrcpy-server**. É preciso confiar explicitamente nesses arquivos; um hash local não certifica a origem. Mudanças invalidam a aprovação. O arquivo de servidor precisa pertencer à mesma instalação do scrcpy. O `scrcpy --version` precisa indicar versão 2 ou posterior; o áudio depende também da versão do Android e suporte do dispositivo.

O servidor adb é próprio, em primeiro plano, ligado apenas a `127.0.0.1` e a uma porta efêmera. Não consulta, substitui ou encerra o servidor global. Outros programas podem estar usando o USB; nesse caso, feche-os e verifique novamente. Não há `adb tcpip`, porta 5555, pareamento automático ou escolha automática entre vários celulares. O scrcpy usa `--no-control` e `--no-clipboard-autosync`, sem áudio por padrão, com caminho explícito para o servidor Android aprovado. O ambiente dos subprocessos aceita somente chaves necessárias e não herda overrides `ADB_*`, `SCRCPY_*` ou `DYLD_*`.

No AirPlay, o botão de ajustes abre o painel de Continuidade do macOS. Se a URL do painel não existir na versão local, procure **Receptor AirPlay** nos Ajustes. Confirme suporte, ativação, acesso e a mesma rede privada. Começar exibe a etapa no iPhone; a sessão é iniciada e encerrada pelo iPhone/macOS. O app não gera PIN, detecta conexão ou desliga o receptor do sistema.

No QuickTime, Começar abre o aplicativo. Selecione **Arquivo → Nova Gravação de Filme → menu ao lado de Gravar → iPhone**. A prévia dispensa gravação. O LetMeSee não seleciona a fonte ou grava automaticamente. Para parar, feche a janela da prévia no QuickTime.

## Preparação e privacidade

As preferências ficam em `~/Library/Application Support/LetMeSee/settings.json`, com permissão de arquivo `0600`. São persistidos dispositivo, conexão, estilo de instrução, opção de áudio e identidades dos arquivos aprovados. Não são salvos PIN, IP do telefone, serial escolhido, checklist ou logs de sessão. A preparação expira em 5 minutos e é invalidada por mudança de ambiente, rede observada pelo sistema, suspensão/retomada, nova verificação e fim da sessão. Não é possível detectar toda mudança de Wi-Fi/isolamento nem validar uma confirmação manual; confira o contexto antes de transmitir.

Antes de cada início Android, o app recalcula os hashes e consulta novamente o estado e a seleção do dispositivo. Ao fechar, encerra somente o scrcpy e o adb que iniciou. Para rotas Apple, fechar o LetMeSee não encerra a sessão do sistema; a interface informa onde pará-la.

Não há download, telemetria, gravação, elevação, alteração de firewall, login remoto ou mudança de Gatekeeper implementados neste app. O scrcpy envia seu componente aprovado ao Android para realizar o espelhamento. Revogue a depuração/autorização no Android depois de usar computadores compartilhados.

## Validação e distribuição

Os testes CLI cobrem a matriz de dispositivo/conexão/experiência, bloqueios de prontidão, USB versus Wi-Fi, seleção explícita, estados RSA/offline, seriais malformados/duplicados/emuladores, argumentos sem shell, integridade, ambiente e timeout. O build local prova compilação. Hardware real Android/iPhone, interrupções de rede, Intel, áudio, QuickTime e AirPlay exigem validação manual antes de distribuir. O app não inclui ferramentas Android, suas bibliotecas ou um pacote notarizado.

Referências oficiais consultadas: [AirPlay para Mac](https://support.apple.com/en-us/108046#airplay), [prévia do iPhone no QuickTime](https://support.apple.com/guide/quicktime-player/record-a-movie-qtp356b55534/mac), [cabo e confiança do dispositivo](https://support.apple.com/en-us/108643), [scrcpy](https://github.com/Genymobile/scrcpy).
