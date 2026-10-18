# Evidência de validação · incremento 1.2.0 · 2026-10-15

## Preparação da pré-release · 2026-10-18

A `main` do GitHub contém o merge `b291b06`. Os builds desse merge falharam: o Windows encontrou um handle de ZIP aberto no fixture e o compilador do runner macOS rejeitou capturas concorrentes de `self`. Essas falhas foram tratadas no preparo da release, com descarte de recursos/carga explícita das assemblies ZIP e capturas fracas explícitas nas tarefas Swift.

O pipeline Windows agora produz um portátil somente do aplicativo, sem redistribuir o runtime legado com inventário pendente. Há testes próprios de composição, exclusão de componentes, inventário e checksum desse pacote. A publicação é uma ação separada, depois de conferir que os workflows Windows/macOS passaram **no commit usado na tag**. Os resultados da execução remota constam nos checks desse commit e nas notas da release; os números abaixo são o histórico local de 15/10, não resultados presumidos do novo CI.

## Histórico da implementação local · 2026-10-15

Ambiente de execução: macOS arm64 com Xcode 26.2/Swift 6.2.3 e Node. PowerShell oficial 7.6.7 foi usado apenas em `/tmp`, sem instalação de SDK ou dependência do produto. O build Windows foi conferido por compilação cruzada com referências oficiais .NET Framework 4.8.

| Verificação executada | Resultado observado | Limite |
| --- | --- | --- |
| Produção C#/C#5/net48/x64/warnings como erros | 14 arquivos compilaram | Não executa WinForms/Win32 nem o linker do csc Windows |
| Core portátil | 7 grupos passaram, 0 falharam | 2 grupos Windows e parsing Win32 pendentes |
| Matriz de preparação C# | 15 grupos passaram, 0 falharam | Fixture privada ADB/Job Object exige Windows, ficou SKIP |
| Segurança PowerShell | 16 casos passaram | PS7/macOS; PS5.1/Windows pendente |
| Pacote Android | 4 verificações passaram | Arquivos sintéticos, sem executar motor |
| Pacote oficial scrcpy 5.0.1 | SHA-256 publicado/API/arquivo iguais; 16 arquivos inventariados | Nenhum EXE/DLL/MSI do pacote executado |
| Domínio Swift | 57 verificações passaram | Fixtures puras, sem celular |
| Infraestrutura Swift | Hash/aprovação/arquivo/symlink/ambiente/argumentos literais/timeout passaram | Não iniciou scrcpy real ou aprovou ferramentas do usuário |
| App SwiftUI | Build arm64/macOS 13+ passou, versão 1.2.0/build12 | Sem assinatura de distribuição ou notarização |
| Domínio da prévia | 20 testes Node passaram, incluindo matriz de 64 combinações simuladas | Simulação não é teste físico |
| Contraste | 8 pares de texto ≥4,5:1 e cores alinhadas | Não é auditoria completa de acessibilidade |
| Navegador CUA | Confirmar não sobrepõe motor ausente; configuração pronta conclui; tela simples tem exatamente 2 controles; troca A→B limpa consentimentos; iPhone/macOS/cabo orienta QuickTime sem alegar sessão | Somente demonstração HTML |
| Higiene | `git diff --check` passou | Não substitui validação funcional |

Comandos mantidos pelo projeto estão em `COMO-TESTAR.md`. Harnesses temporários usados nesta sessão: `/tmp/compile-letmesee.ps1`, `/tmp/letmesee-core-qa.ps1`, `/tmp/letmesee-preparation-qa.ps1` e `/tmp/letmesee-android-package-verify.ps1`.

## Bugs e lacunas encontrados e corrigidos

- Cancelamento pela bandeja durante preflight podia ser seguido por início tardio: intenção monotônica invalida o callback.
- Consulta Android e espelhamento podiam manter dois servidores ADB privados: probes são encerrados antes do adaptador assumir a sessão.
- Trocar A por B mantinha consentimentos: ambas as UIs limpam o aceite contextual; regressão Swift adicionada.
- Perfil DomainAuthenticated não corresponde a regra Private: AirPlay é bloqueado com orientação de política, sem alterar a categoria da rede.
- PIN UxPlay não é garantia de prompt a cada reconexão: identidade randomizada e documentação de pareamento corrigida.
- Confirmação/contexto antigo após rede/suspensão/relógio regressivo: invalidação e nova verificação antes do início.
- Prévia cortava botões em janela baixa e chip de simulação tinha texto escuro: regra de altura ajustada e par de cores corrigido. Captura final mostra as duas ações no viewport.
- Testes externos do módulo da prévia não estavam no CI: comandos de sintaxe e teste Node foram incluídos.

## Revisão independente

Outro agente revisou somente leitura métodos, C#/Swift, scripts e integração. Os achados foram encaminhados aos autores e corrigidos. Trata-se de revisão de modelo separada da autoria, não certificação externa.

## Não executado / acesso não concedido

A ferramenta de controle do aplicativo macOS retornou que o uso de LetMeSee não foi aprovado. Não repetimos a tentativa nem usamos outra tecnologia de automação para contornar a restrição. O app nativo foi compilado/testado no nível disponível; sua janela e os fluxos físicos permanecem pendentes.

Também não executados: build_ui.bat no Windows, APIs Win32 de processo/quoting, PowerShell 5.1, Inno Setup/UAC/Bonjour/firewall real, upgrade/desinstalação, AirPlay/QuickTime/Android físicos, áudio, Intel, DPI e leitores de tela. CI preparado, ainda sem push ou execução remota. Nenhum release foi publicado.
