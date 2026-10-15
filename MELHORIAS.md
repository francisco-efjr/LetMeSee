# Melhorias · 2026-10-15

## Incremento 1.2.0 · preparação e múltiplos ambientes

O fluxo agora começa com um checklist contextual. Depois de concluir os requisitos, a tela principal tem somente **Ambiente** e **Começar a espelhar**; durante a execução, a segunda ação encerra a sessão própria ou conclui a orientação Apple.

| Área | Implementado | Evidência e limite |
| --- | --- | --- |
| Cenários | Android/iOS, Windows/macOS, USB/Wi-Fi, sistema novo/pronto, instruções guiadas/técnicas e múltiplos aparelhos | Matriz com 55 critérios em `docs/qa/SCENARIOS.md`; hardware pendente |
| Checklist | Diagnósticos automáticos separados de confirmações manuais; nenhuma caixa ignora motor ausente ou dispositivo não autorizado | 15 grupos C# e 57 verificações do domínio Swift passaram |
| Validade | Nova conferência ao iniciar; troca de aparelho/ambiente, parada/erro, suspensão, mudança de rede aplicável e prazo de cinco minutos invalidam a preparação | Regressões de contexto, expiração e seleção A→B; detalhes em `docs/qa/VALIDACAO.md` |
| Android | scrcpy somente visualização, clipboard desativado, serial explícito e servidor ADB privado em loopback | Pacote oficial 5.0.1 fixado/verificado no Windows; ferramentas locais aprovadas por identidade no Mac; sem sessão física executada |
| Apple | UxPlay no Windows; orientação de Receptor AirPlay e QuickTime no Mac | Nenhuma alegação de conexão baseada apenas em processo ou orientação |
| macOS | App SwiftUI nativo, domínio/infraestrutura testáveis e build local 1.2.0 | Build arm64/macOS 13+ passou; janela nativa sem inspeção automatizada e sem assinatura de distribuição |
| Instalação | Android preparado por ação explícita; Bonjour exige integridade, assinatura Apple válida e instalador visível | 16 testes de segurança e 4 de pacote Android passaram em PS7/macOS; UAC/PS5.1 pendentes |
| Interface | Duas ações principais, papel granulado e paleta Pulse; prévia marcada como demonstração | 20 testes Node, 64 combinações simuladas, 8 pares de contraste e inspeção no navegador |
| CI | Suítes Windows e macOS, compilação e artefatos para revisão, sem publicar release | Configuração local revisada; execução remota pendente de envio |

A produção Windows compila em C#5/.NET Framework 4.8/x64 com warnings como erros (**14 arquivos**). Os **7 grupos core** anteriores continuam passando. A prévia e os testes de domínio não substituem espelhamento físico, execução Windows, avaliação de acessibilidade ou teste de instalação. Veja `COMO-TESTAR.md` e `docs/qa/VALIDACAO.md` para comandos e limites.

## Histórico · primeira etapa

Branch local: `codex/pulse-workbench`. Base: `main` do repositório informado. O workspace estava vazio e sem commits; o código original foi obtido antes da implementação, sem alterações locais a preservar.

### Resultado da primeira etapa

| ID | Antes | Implementado | Evidência |
| --- | --- | --- | --- |
| M01 · interface | Janela fixa compacta, estilos e execução em um arquivo | Navegação, tokens semânticos e componentes; paleta Pulse, papel granulado local, radar de abertura e acessibilidade do SO | `src/UI`, `docs/design/preview.png`, 8 pares de texto medidos |
| M02 · recepção | Nome anunciava usuário, sem preferências ou limite | Nome validado sem hostname, PIN por sessão ativado por padrão, mute, limite de sessão, bandeja opt-in e XML local | 7 grupos core portáveis passaram; flags conferidas no manual UxPlay 1.73.6 |
| M03 · processo | Download e espera na UI, riscos de callback/processo órfão | Verificação em worker, identidade por sessão, ambiente de plugins isolado e Job Object | Compilação completa passou; comportamento Windows do Job Object ainda pendente |
| M04 · dependências | ZIP sem hash, URL sobrescrevível, MSI autoexecutado | Pin real de SHA-256, ZIP validado antes de extrair, staging/rollback e manifesto por arquivo; nenhum MSI automático | 16 testes PS passaram; integração com pacote oficial passou |
| M05 · rede/instalação | Regras globais `profile=any`, Bonjour silencioso | Firewall por executável/portas, Private+LocalSubnet, opt-in, limpeza de legado e remoção; diagnóstico sem elevação | Revisão independente de scripts/Inno; UAC/firewall/instalação pendentes no Windows |
| M06 · entrega | CI sobrescrevia release fixa e ignorava falhas | CI de PR com testes/build, SHA de ações/compilador fixados, checksums e artifacts, sem publicar releases | YAML e scripts revisados; execução GitHub Actions pendente de envio da branch |

A aplicação segue C#/WinForms e .NET Framework 4.8. Não houve migração de stack ou adição de dependências do produto. O formato do Mo baile foi adaptado como separação de camadas, documentação de arquitetura/design e gates de qualidade. A revisão independente seguiu a separação autor/validador do Brain of Agent. O diff ultrapassa a referência de tamanho do modo melhoria porque as fronteiras de instalação e runtime exigiram serviços e validações próprios; o trabalho ficou nos seis grupos acima.

### Validação da primeira etapa

- Compilação de **7 arquivos de produção**, com Roslyn disponível em PowerShell portátil oficial, linguagem C#5, referências oficiais .NET Framework 4.8, plataforma x64 e warnings como erros: passou. O primeiro build apontou API de animação incompatível; foi substituída pela chamada nativa suportada e a compilação passou. Isso não substitui a execução WinForms.
- Core em .NET portátil no macOS: **7 grupos passaram, 0 falharam**. O teste de XML excessivo descobriu uma exceção sem fallback; foi corrigida e coberta também para campos ausentes e versão desconhecida.
- PowerShell 7.6.7 portátil oficial no macOS: **16 casos de segurança passaram**; parse de todos os `.ps1`, pin/origem/hash, traversal/links/colisões/bomba ZIP, inventário de executáveis e promoção/rollback.
- Integração com o ZIP oficial: hash conferido, extração em staging e **608 arquivos incluindo manifesto**. Nenhum EXE, DLL ou MSI do runtime foi executado.
- `python3 tests/check-design.py`: **8 pares de texto ≥ 4,5:1** e paleta da prévia alinhada com os tokens nativos. Não é uma auditoria completa de acessibilidade.
- Sintaxe JavaScript da prévia, navegação, estado demonstrativo, alteração de nome e renderização no navegador: passaram. A prévia identifica dados/estado ilustrativos.
- `git diff --check`: passou. Revisor independente de core/scripts/instalador/CI não deixou achado material aberto no snapshot revisto.

## Pendente antes de distribuir

Build e testes no Windows PowerShell 5.1/csc do Framework; abrir WinForms/SwiftUI e verificar teclado/leitores de tela/DPI; processo/Job Object e quoting via Win32; instalação/UAC/firewall/upgrade/desinstalação; Android USB/Wi-Fi e AirPlay/QuickTime com aparelhos físicos, código, áudio e encerramento. O roteiro está em `COMO-TESTAR.md`.

O pacote legado não possui inventário/licenças completos de terceiros. Completar esses materiais, revisar versões/CVEs e assinar os artefatos antes de distribuição pública. Integridade foi endurecida; não há promessa de segurança absoluta ou de release pronta.

Nenhum push, PR, deploy ou release foi feito. O GitHub ainda contém a versão anterior até uma ação de envio autorizada.
