# Como testar

## Neste Mac: prévia visual e verificações portáveis

Abra `docs/design/preview.html` no navegador. A prévia também está servida nesta sessão em `http://127.0.0.1:8765/preview.html`. Ela não executa AirPlay, não consulta a rede real, não configura firewall e não persiste preferências em disco.

```sh
python3 tests/check-design.py
```

Na sessão de implementação, os seguintes harnesses temporários foram executados, sem instalar SDK ou dependências do produto:

```sh
/tmp/letmesee-powershell/runtime/pwsh -NoProfile -File tests/dependency_security.tests.ps1
/tmp/letmesee-powershell/runtime/pwsh -NoLogo -NoProfile -File /tmp/letmesee-core-qa.ps1
/tmp/letmesee-powershell/runtime/pwsh -NoProfile -File /tmp/compile-letmesee.ps1
```

Resultados: 16 casos PS, 7 grupos core e compilação de todos os arquivos C#5 para .NET Framework 4.8 passaram. Os harnesses `/tmp` dependem das ferramentas temporárias desta sessão. A execução abaixo no Windows é o caminho mantido pelo projeto e pelo CI.

## Windows 10/11 x64: build e testes obrigatórios

Pré-requisitos: .NET Framework 4.8 e PowerShell 5.1. Faça o checkout da branch de implementação, em uma pasta sem runtime antigo não verificado.

```powershell
.\build_ui.bat
powershell -NoProfile -File .\tests\run-core-tests.ps1
powershell -NoProfile -File .\tests\dependency_security.tests.ps1
```

Esperado: compilação sem warnings, 9 grupos core sem falhas (incluindo testes Win32 de argumentos e término de descendentes) e 16 casos PS sem falhas. O teste de subprocessos usa um executável de teste local sem rede, não o UxPlay real.

## Runtime e aplicativo

```powershell
powershell -NoProfile -File .\scripts\download_deps.ps1
.\iniciar.bat
```

O download é explícito, fixa o hash e não executa Bonjour. Não execute o download portátil elevado. Esperado: `bin/runtime-files.sha256` criado e app aberto **desligado**, nome `LetMeSee`, PIN ativado e bandeja ao fechar desativada.

1. Em Diagnóstico, verificar motor, interfaces IPv4 e Bonjour. Motor ausente/alterado deve mostrar uma ação útil, sem download automático. Ao iniciar com DLL alterada, o processo não pode ser criado.
2. Em Preferências, salvar um nome de 48 caracteres, mute e limite; reabrir o app e conferir persistência. Nome vazio, controles, aspas, barra invertida ou início com hífen devem ser recusados. Não salvar PIN.
3. Iniciar e parar rapidamente, inclusive durante a verificação de arquivos; não deixar processos órfãos nem um callback antigo desligar uma sessão nova.
4. Iniciar, tentar abrir segunda instância, fechar, verificar o término do UxPlay e descendentes. Ativar opção de bandeja: fechar deve manter o ícone; Sair deve encerrar.
5. Conferir teclado, foco, textos longos e DPI 100/125/150/200%. Ativar alto contraste antes de abrir. Desativar animações no Windows: o radar deve aparecer estático. Testar Narrator; não presumir conformidade a partir de uma captura.

## Firewall e instalador (máquina de teste)

Empacotamento: o workflow `Verify and build LetMeSee for Windows` prepara runtime/compilador por hash e gera instalador, ZIP e SHA256SUMS. Não publica release. Também é possível executar o script Inno Setup após preparar os arquivos.

1. Instalar sem marcar acesso de rede. Confirmar ausência de regras próprias e remoção das regras amplas da versão anterior; app não deve abrir elevado.
2. Solicitar Configurar firewall na UI. Cancelar UAC não deve declarar sucesso. Aceitar UAC deve criar somente regras de entrada `Private`, `LocalSubnet`, vinculadas ao executável/portas.
3. Conferir com PowerShell administrativo:

```powershell
Get-NetFirewallRule -Group LetMeSee | Format-Table Name,Enabled,Profile,Direction,Action
Get-NetFirewallRule -Group LetMeSee | Get-NetFirewallAddressFilter
Get-NetFirewallRule -Group LetMeSee | Get-NetFirewallApplicationFilter
```

4. Repetir configuração; regras não podem se multiplicar. A porta TCP 7001 precisa estar incluída, além de 7000/7100, conforme o modo legado do UxPlay.
5. Testar upgrade da versão anterior com regras amplas, instalação com task de rede desmarcada e desinstalação; nenhuma regra antiga do próprio LetMeSee deve permanecer. Não remover regras de outros aplicativos.
6. Confirmar que Bonjour MSI não foi iniciado e que cancelamentos/falhas são visíveis. Desinstalar deve remover regras e arquivos instalados, preservando preferências locais por usuário.

## iPhone/iPad real

1. Na mesma rede privada, iniciar o receptor e localizar nome sem sufixo de usuário/hostname.
2. Tentar código incorreto: acesso recusado. Usar o código mostrado: espelhar tela e testar áudio. Parar/reiniciar deve gerar novo código; a confirmação de comportamento depende do dispositivo/protocolo.
3. Ativar mute e reiniciar sessão; vídeo deve continuar, áudio ficar silenciado. Testar limite de 15 minutos no receptor (o teste unitário não substitui essa espera real).
4. Ao parar, o serviço deve deixar de aceitar novos espelhamentos. Conferir comportamento em rede Pública sem alterar categoria nem desativar firewall.
5. Conferir que `Pronto para receber` não vira indicação fictícia de iPhone conectado e que a UI orienta que o vídeo abre em outra janela.

## Estado da evidência

A compilação cruzada, core portátil, testes PS portáteis, contraste e prévia foram executados. Os cenários Windows, instalador, UAC, firewall real, APIs Win32 de processo e recepção física **ainda não foram executados**. Registre ambiente, comando, esperado e observado ao completá-los. Não distribuir como versão validada até esses gates e o inventário de terceiros serem concluídos.
