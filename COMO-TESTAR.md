# Como testar · preparação e tela de duas ações

## Verificações sem aparelho

```sh
node --test tests/preview-workflow.test.js
python3 tests/check-design.py
zsh apps/macos/test.sh
zsh apps/macos/build.sh
```

No Windows 10/11 x64 com .NET Framework 4.8 e PowerShell 5.1:

```powershell
.\build_ui.bat
powershell -NoProfile -File .\tests\run-core-tests.ps1
powershell -NoProfile -File .\tests\run-preparation-tests.ps1
powershell -NoProfile -File .\tests\dependency_security.tests.ps1
powershell -NoProfile -File .\tests\android-package.tests.ps1
powershell -NoProfile -File .\tests\windows-package.tests.ps1
```

Esperado no Windows: 9 grupos core, 16 grupos de preparação (incluindo fixture de processos Windows), 17 casos de segurança/rollback e 4 verificações do pacote Android, além da suíte de empacotamento portátil. Os testes usam arquivos/processos fictícios locais; não precisam de telefone ou de um UxPlay real.

Para gerar o mesmo pacote público, compile e execute `powershell -NoProfile -File .\scripts\package_windows.ps1 -Version 1.2.0`. O ZIP e seu checksum ficam em `dist/`. A lista de arquivos é explícita: motores nativos e instaladores de terceiros não são incluídos. `installer/setup.iss` continua disponível para revisão, mas o instalador completo não é produzido/publicado por esse fluxo enquanto o inventário de distribuição estiver pendente.

No Mac desta implementação, passaram os 7 grupos core portáveis e 15 de preparação; os grupos Win32 ficaram SKIP. A compilação C#5 foi conferida com Roslyn e referências oficiais .NET Framework 4.8, x64 e warnings como erros. Passaram 57 verificações do domínio Swift, a suíte de infraestrutura e o build SwiftUI arm64. Isso não é execução do instalador Windows nem prova de transmissão física.

## Prévia interativa

Abra `docs/design/preview.html` por um servidor local que sirva também `workflow.js`:

```sh
python3 -m http.server 8765 --bind 127.0.0.1 --directory docs/design
```

Na prévia, os controles de laboratório são apenas simulação. Eles não existem na tela principal do produto e não acessam aparelhos reais.

1. Sistema novo: marque as confirmações. Concluir deve permanecer bloqueado enquanto motor/dispositivo estiverem ausentes.
2. Sistema configurado: confirme os cuidados e conclua. A tela seguinte deve mostrar somente Ambiente e Começar a espelhar.
3. Android sem autorização: confirmar RSA na prévia não pode sobrepor o resultado automático não autorizado.
4. Vários Androids: nenhum deve ser escolhido silenciosamente. Confirme A, troque para B; autorização/privacidade precisam ser confirmadas novamente.
5. Troque host, aparelho, conexão ou cenário: evidências e confirmações devem ser invalidadas. Modo técnico só muda explicação.
6. iPhone + macOS + cabo: após começar, a orientação deve pedir a fonte no QuickTime e mostrar Concluir orientação. Não afirmar conexão ou desligamento do serviço Apple.
7. Motor alterado, permissão negada, porta ocupada e falta de rede: mostrar a causa aplicável; USB pode funcionar sem rede.
8. Preparação vencida/suspensão/erro: não reiniciar automaticamente. O módulo tem testes de tempo negativo, cinco minutos e nova observação antes de Start.

As capturas em `docs/design/checklist-preview.png` e `preview.png` são do navegador, não screenshots de WinForms/SwiftUI.

## Windows: sistema limpo

1. Abrir o app: o checklist deve aparecer; a tela simples não pode iniciar processo sem sua conclusão.
2. Android: Preparar Android deve pedir uma ação explícita, baixar o scrcpy oficial fixado, conferir hash e gerar inventário em `%LOCALAPPDATA%\LetMeSee\engines\android`. Nenhum adb/scrcpy deve rodar durante o download.
3. iOS: Preparar AirPlay usa o runtime fixado. Em Program Files ele deve vir do instalador; corrupção/ausência deve orientar reinstalação se a pasta não for gravável.
4. Descoberta: Configurar descoberta pede consentimento e UAC. MSI sem assinatura Apple válida, ausente ou alterado deve ser bloqueado. Instalador Apple completo deixa aceite/cancelamento com o usuário; reinicialização solicitada não é tratada como pronto.
5. Firewall: Cancelar UAC não deve aprovar o checklist. A configuração cria somente regras por programa/portas, Private e LocalSubnet. Não altera categoria da rede ou política corporativa.
6. Sem administrador: continuar com requisitos já instalados ou usar Android USB quando viável; não contornar restrições de instalação, driver ou MDM.

## Windows: sistema configurado

1. Nenhum download ou MSI automático. Conferir runtime, portas, rede e serviço existentes.
2. Porta ocupada, Bonjour ausente/parado e firewall conhecido inválido bloqueiam. Evidência indisponível exige revisão manual explícita e nunca aparece como verificação automática concluída.
3. Rede Pública ou de domínio não deve ser apresentada como AirPlay liberado por regras Private. Usar rede Privada autorizada ou consultar TI. Wi-Fi convidado/VPN/isolamento podem falhar mesmo na mesma rede.
4. Fechar/parar durante a verificação não pode iniciar depois. Trocar aparelho não pode reaproveitar consentimentos; um serial que desapareceu não pode ser substituído por outro automaticamente.
5. Repetir configuração e testar upgrade/desinstalação. Confirmar remoção das regras próprias legadas amplas, sem remover regras de outros aplicativos.

Para conferir regras com uma conta administrativa:

```powershell
Get-NetFirewallRule -Group LetMeSee | Format-Table Name,Enabled,Profile,Direction,Action
Get-NetFirewallRule -Group LetMeSee | Get-NetFirewallAddressFilter
Get-NetFirewallRule -Group LetMeSee | Get-NetFirewallApplicationFilter
```

## Android físico · Windows e Mac

- Cabo de dados, outra porta, Android desbloqueado; estados sem dispositivo/RSA negado/offline e drivers OEM quando necessários.
- Dois aparelhos e emulador simultâneo: escolher o aparelho físico correto; não escolher o primeiro e não trocar alvo após desconexão.
- Android 11+ sem fio: pareamento anterior, descoberta no servidor privado e rede permitida. Se não descoberto, orientar USB; não abrir 5555.
- Iniciar com arquivo alterado, servidor Android alterado e overrides de ambiente: bloquear ou usar somente caminhos aprovados.
- Android Studio/ADB de terceiros em paralelo: LetMeSee não deve matar o servidor global. Se houver disputa por USB, mostrar diagnóstico em vez de assumir posse.
- Parar, fechar a janela, desconectar, suspender e chegar ao limite de tempo: encerrar somente processos próprios e invalidar preparação.
- Vídeo, áudio por versão/dispositivo, app protegido/DRM e revogação de depuração após uso em computador compartilhado.

## iPhone/iPad físico

Windows: mesma rede privada, descoberta Bonjour, receptor com nome configurado, código de pareamento, áudio, parada e nova execução. Tentar cliente já pareado após Stop/Start; o teste físico determina quando o protocolo pede código. Processo ativo não pode ser rotulado como cliente conectado.

Mac/AirPlay: confirmar compatibilidade, Receptor ativado e acesso permitido. Iniciar a sessão no iPhone e encerrá-la pelo sistema; LetMeSee não deve simular um PIN próprio ou dizer que desligou o receptor.

Mac/cabo: cabo de dados, confiança no aparelho, QuickTime → Nova Gravação de Filme → selecionar iPhone. Confirmar que não grava automaticamente; parar na janela QuickTime.

## Acessibilidade e ambiente

Testar teclado/foco/Narrator/VoiceOver, janela pequena, nome longo, DPI 100/125/150/200%, alto contraste e movimento reduzido. A captura de navegador prova apenas a prévia. O controle da janela macOS pela ferramenta CUA não foi autorizado nesta sessão; não houve tentativa alternativa de contornar essa restrição.

A matriz completa está em `docs/qa/SCENARIOS.md`. Os casos físicos, Win32, UAC, instalador, firewall real, Intel, áudio e leitores de tela continuam pendentes. Não distribuir antes dos gates e do inventário de terceiros em `THIRD_PARTY.md`.
