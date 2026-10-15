# Melhorias · 2026-10-15

Branch local: `codex/pulse-workbench`. Base: `main` do repositório informado. O workspace estava vazio e sem commits; o código original foi obtido antes da implementação, sem alterações locais a preservar.

## Resultado

| ID | Antes | Implementado | Evidência |
| --- | --- | --- | --- |
| M01 · interface | Janela fixa compacta, estilos e execução em um arquivo | Navegação, tokens semânticos e componentes; paleta Pulse, papel granulado local, radar de abertura e acessibilidade do SO | `src/UI`, `docs/design/preview.png`, 8 pares de texto medidos |
| M02 · recepção | Nome anunciava usuário, sem preferências ou limite | Nome validado sem hostname, PIN por sessão ativado por padrão, mute, limite de sessão, bandeja opt-in e XML local | 7 grupos core portáveis passaram; flags conferidas no manual UxPlay 1.73.6 |
| M03 · processo | Download e espera na UI, riscos de callback/processo órfão | Verificação em worker, identidade por sessão, ambiente de plugins isolado e Job Object | Compilação completa passou; comportamento Windows do Job Object ainda pendente |
| M04 · dependências | ZIP sem hash, URL sobrescrevível, MSI autoexecutado | Pin real de SHA-256, ZIP validado antes de extrair, staging/rollback e manifesto por arquivo; nenhum MSI automático | 16 testes PS passaram; integração com pacote oficial passou |
| M05 · rede/instalação | Regras globais `profile=any`, Bonjour silencioso | Firewall por executável/portas, Private+LocalSubnet, opt-in, limpeza de legado e remoção; diagnóstico sem elevação | Revisão independente de scripts/Inno; UAC/firewall/instalação pendentes no Windows |
| M06 · entrega | CI sobrescrevia release fixa e ignorava falhas | CI de PR com testes/build, SHA de ações/compilador fixados, checksums e artifacts, sem publicar releases | YAML e scripts revisados; execução GitHub Actions pendente de envio da branch |

A aplicação segue C#/WinForms e .NET Framework 4.8. Não houve migração de stack ou adição de dependências do produto. O formato do Mo baile foi adaptado como separação de camadas, documentação de arquitetura/design e gates de qualidade. A revisão independente seguiu a separação autor/validador do Brain of Agent. O diff ultrapassa a referência de tamanho do modo melhoria porque as fronteiras de instalação e runtime exigiram serviços e validações próprios; o trabalho ficou nos seis grupos acima.

## Validação executada

- Compilação de **7 arquivos de produção**, com Roslyn disponível em PowerShell portátil oficial, linguagem C#5, referências oficiais .NET Framework 4.8, plataforma x64 e warnings como erros: passou. O primeiro build apontou API de animação incompatível; foi substituída pela chamada nativa suportada e a compilação passou. Isso não substitui a execução WinForms.
- Core em .NET portátil no macOS: **7 grupos passaram, 0 falharam**. O teste de XML excessivo descobriu uma exceção sem fallback; foi corrigida e coberta também para campos ausentes e versão desconhecida.
- PowerShell 7.6.7 portátil oficial no macOS: **16 casos de segurança passaram**; parse de todos os `.ps1`, pin/origem/hash, traversal/links/colisões/bomba ZIP, inventário de executáveis e promoção/rollback.
- Integração com o ZIP oficial: hash conferido, extração em staging e **608 arquivos incluindo manifesto**. Nenhum EXE, DLL ou MSI do runtime foi executado.
- `python3 tests/check-design.py`: **8 pares de texto ≥ 4,5:1** e paleta da prévia alinhada com os tokens nativos. Não é uma auditoria completa de acessibilidade.
- Sintaxe JavaScript da prévia, navegação, estado demonstrativo, alteração de nome e renderização no navegador: passaram. A prévia identifica dados/estado ilustrativos.
- `git diff --check`: passou. Revisor independente de core/scripts/instalador/CI não deixou achado material aberto no snapshot revisto.

## Pendente antes de distribuir

Build e testes no Windows PowerShell 5.1/csc do Framework; abrir WinForms e verificar teclado/Narrator/DPI; processo/Job Object e quoting via Win32; instalação/UAC/firewall/upgrade/desinstalação; AirPlay com iPhone/iPad, código, áudio e limite de sessão. O roteiro está em `COMO-TESTAR.md`.

O pacote legado não possui inventário/licenças completos de terceiros. Completar esses materiais, revisar versões/CVEs e assinar os artefatos antes de distribuição pública. Integridade foi endurecida; não há promessa de segurança absoluta ou de release pronta.

Nenhum push, PR, deploy ou release foi feito. O GitHub ainda contém a versão anterior até uma ação de envio autorizada.
