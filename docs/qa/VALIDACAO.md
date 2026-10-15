# Evidência de validação · 2026-10-15

Ambiente: macOS arm64, workspace local. Não havia .NET SDK/Framework, Mono ou PowerShell no PATH. Um PowerShell oficial Microsoft 7.6.7 foi obtido apenas em `/tmp`, sem instalação no sistema. SHA-256 do arquivo de ferramenta conferido: `50dde99bb066adacbb9a748182d09eb9beff25119dc3b12e85c84b6b6b1c5da9`.

## Executado

| Verificação | Resultado observado | Limite |
| --- | --- | --- |
| Roslyn + referências Microsoft .NET Framework 4.8 1.0.3 | `PASS: GUI compiled with C#5, .NET Framework 4.8 reference assemblies, x64, warnings as errors; 7 source files` | Compilação cruzada, não executa a UI nem embute recursos via build_ui.bat |
| Core via Roslyn/.NET portátil | `Passed: 7; Failed: 0`, exit 0 | Parsing Win32 e 2 grupos de processo condicionais Windows não executados |
| `tests/dependency_security.tests.ps1` | `Security checks passed: 16`, exit 0 | Executado em PS7/macOS; PS5.1/Windows ainda pendente |
| Pacote runtime oficial | SHA-256 conferido; validação ZIP e manifesto: 608 arquivos incluindo manifesto | Nenhum binário do pacote foi executado |
| `python3 tests/check-design.py` | `PASS: 8 text contrast pairs >= 4.5:1; preview colors match native tokens` | Tema escuro e pares de texto listados, sem alegação de conformidade completa |
| JavaScript inline da prévia | `node --check`: exit 0 | Somente sintaxe |
| Navegador local | Navegação 3 seções; ligar/parar demonstração; alterar nome; recusar `-d`; verificar diagnóstico ilustrativo; captura final | Prévia HTML não executa receiver |
| `git diff --check` | exit 0 | Higiene de diff |

## Bugs encontrados pela execução e corrigidos

1. Primeira compilação da UI: API `SystemInformation.IsClientAreaAnimationEnabled` ausente no .NET Framework 4.8. Corrigida para consulta nativa `SPI_GETCLIENTAREAANIMATION`. Nova compilação passou.
2. Primeiro teste do core: XML com mais de 16 KiB lançava `InvalidDataException` fora do catch. O fallback seguro foi corrigido, e os testes de arquivo excessivo, campos obrigatórios e versão desconhecida passaram.

## Revisão independente

Outro agente fez revisão somente leitura do core, interface, scripts, instalador, CI e testes. Encontrou problemas de integração (retorno de Save, Timer ambíguo, falha ao parar, callback antigo), rollback, feedback de firewall e regras legadas. As correções foram verificadas por nova leitura e testes disponíveis. Essa revisão é opinião de modelo independente da autoria, não auditoria externa de segurança.

## Não executado

`build_ui.bat` pelo compilador do Windows; execução WinForms; Windows Job Object e parsing nativo; Windows PowerShell 5.1; Inno Setup, UAC, firewall real, upgrade/desinstalação; AirPlay físico, áudio e leitor de tela/DPI. O pipeline foi preparado, mas não foi acionado nesta sessão. Não há release publicada por este trabalho.

Roteiro mantido: `COMO-TESTAR.md`. Gates de distribuição: teste Windows/dispositivo e inventário completo de terceiros conforme `THIRD_PARTY.md`.
