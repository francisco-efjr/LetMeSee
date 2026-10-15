# ADR-001 · Evoluir o receptor nativo Windows

Status: implementado localmente; validação nativa pendente.

## Decisão

Preservar C#/WinForms, .NET Framework 4.8, UxPlay e Inno Setup. O Mo baile inspira a separação entre interface e motor e o contrato de design; não determina a linguagem, a plataforma ou a estética do LetMeSee.

`Core` valida preferências e argumentos. `Infrastructure` verifica os arquivos, controla o processo e inspeciona rede/Bonjour. `UI` concentra tokens e componentes. `MainForm` traduz ações e estados sem fazer download, parsear logs para inventar uma conexão ou executar shell com nome fornecido pelo usuário.

O receiver começa parado. O início verifica integridade em worker, gera um PIN criptográfico por sessão (quando ativado) e cria um processo com argumentos validados. O estado Running prova apenas que o processo existe. A janela do vídeo é controlada pelo UxPlay. O Stop tem espera limitada e um Job Object cobre descendentes e queda da interface.

Preferências ficam fora da instalação, por usuário. O diagnóstico faz leitura. Instalação de dependências e alteração do firewall são operações explícitas, separadas da recepção.

## Consequências

Sem runtime web, NuGet ou dependência gráfica nova. Componentes e core podem ser compilados pelo Framework do Windows. A UI não é executável neste Mac; compilação, UX nativa e AirPlay são gates obrigatórios antes de distribuir. A prévia HTML é documentação visual, não outra implementação do receiver.
