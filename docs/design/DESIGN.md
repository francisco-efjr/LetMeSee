# LetMeSee · Contrato de design

Produto: espelhamento de Android, iPhone e iPad no Windows ou macOS. Tarefa principal: preparar o ambiente, iniciar deliberadamente o método escolhido e entender onde encerrar.

## Contrato do fluxo

1. Abrir **Preparar seu ambiente** antes da primeira sessão. A pessoa escolhe aparelho, conexão e nível de explicação. O sistema operacional vem do aplicativo nativo.
2. Mostrar somente os requisitos do método. Resultados automáticos são somente leitura; confirmações sobre celular/rede/privacidade exigem ação da pessoa. Confirmar não substitui runtime íntegro, autorização Android ou outra condição local bloqueante.
3. Liberar **Concluir preparação** quando os requisitos estiverem satisfeitos. Não persistir o checklist concluído, PIN ou serial escolhido. As preferências de ambiente podem ser lembradas.
4. Mostrar exatamente duas ações na tela principal: **Ambiente** e **Começar a espelhar**. Informações de estado e instruções são passivas. A ação principal passa a **Parar espelhamento** para processos próprios ou **Concluir orientação** nas rotas Apple do Mac.
5. Revalidar antes de iniciar. Troca de contexto/aparelho, parada/erro, suspensão, mudança de rede aplicável ou prazo de cinco minutos exige preparação renovada. Durante uma sessão, Ambiente fica indisponível.

O nível guiado muda a explicação; não remove requisitos. Dispositivo ausente, não autorizado, offline ou ambíguo recebe orientação contextual. A seleção de um novo aparelho limpa as confirmações anteriores. Não escolher outro aparelho silenciosamente quando o selecionado desaparecer.

Diagnóstico, preferências e ações explícitas de preparação vivem no checklist, fora da tela principal. Downloads, UAC, instalador Bonjour e aprovação de ferramentas nunca acontecem apenas por abrir o aplicativo.

## Origem e adaptação

- [Mo baile](https://github.com/francisco-efjr/mo-baile): separação interface/motor, tokens semânticos, estados por símbolo + texto + cor, escala de 4, fontes do sistema, foco e acessibilidade.
- Bundle da casa: `brain-of-agent/design-systems/mo-baile`, consultado em 2026-10-15. Tokens originais de espaço/forma e contrato de componentes serviram de estrutura.
- [Pulse / R6, referência enviada](https://steamcommunity.com/sharedfiles/filedetails/?id=1408618718): grafite esverdeado, laranja queimado, off-white e linguagem de radar. O GIF não é carregado do Steam pelo aplicativo; a animação é um radar original desenhado localmente.
- Anexo “Grainy Cardboard Overlays”: referência de granulação/material. A imagem comercial não foi incorporada ao produto; a textura é gerada com semente fixa e baixa opacidade.

## Tokens

Fonte de verdade Windows: `src/UI/DesignTokens.cs`; adaptação SwiftUI: `Palette` em `apps/macos/Sources/LetMeSeeApp.swift`. A documentação visual em `preview.html` espelha a paleta; não substitui os renderers nativos.

| Papel | Token C# | Valor escuro |
| --- | --- | --- |
| Fundo | Window | #111717 |
| Conteúdo | Surface / Raised | #1A2120 / #222B29 |
| Texto | Text / Muted | #F5F1E5 / #B1BCB3 |
| Ação e foco | Accent / OnAccent | #EB8F5D / #1E1813 |
| Papel | Paper / Ink | #E9DEC5 / #262B23 |
| Detalhe no papel | Orange | #A6421D |
| Estado positivo | Success | #AAD399 |
| Erro | Danger | #FFA696 |

Windows usa Segoe UI para interface e Consolas para rótulos técnicos; macOS usa fontes e controles do sistema. Grade de 4; raio de cartão 12; altura de controle de ação 40, ação principal 48. A textura é decorativa e discreta; campos e resultados do checklist ficam em superfícies opacas.

No Windows em alto contraste, os tokens usam SystemColors; não há textura nem animação. O radar de abertura dura 1,2 s, não fica em loop e observa a preferência nativa `SPI_GETCLIENTAREAANIMATION`, além da preferência local. Mudanças de alto contraste precisam de reinício. O símbolo SwiftUI é estático. Suporte completo a ajustes de acessibilidade do macOS permanece no roteiro de validação.

## Componentes e estados

- `SurfacePanel`: agrupa conteúdo com raio e borda discreta. A variante Paper é exclusiva do hero, sem campos de formulário sobre textura.
- `ActionButton`: verbo + nome acessível, foco visível, navegação por Tab; ações nativas mantêm atalhos por `&`.
- `SonarPanel` / `PulseMark`: ilustração sem função de status. O indicador textual é a fonte de estado.
- Checklist: pronto, ação necessária, bloqueio e aviso, com texto e símbolo além de cor. Rolagem preserva o acesso às ações em janelas baixas.
- Espelhamento: preparação necessária, conferido, verificando, processo ativo/orientação e erro. Nunca exibir “conectado” com base só no processo.
- Apple no Mac: indicar que AirPlay e QuickTime pertencem ao sistema; concluir a orientação não encerra essas sessões. Mostrar onde a pessoa pode pará-las.
- Preferências: habilitadas apenas sem sessão ativa; salvar confirma apenas após sucesso da escrita.
- Diagnóstico: carregando e resultado/erro, sem executar reparos ou solicitar UAC na leitura. Falhas preservam orientação de recuperação, sem aceitar o requisito automaticamente.

## Prévia e limites

`preview.html` e `workflow.js` funcionam localmente sem fontes, bibliotecas ou serviços externos. O modo **Demonstração** fica visível. A prévia simula cenários e não detecta aparelhos, instala componentes ou inicia espelhamento. Preferências vivem só na memória da página; os apps usam seus armazenamentos locais. `checklist-preview.png` e `preview.png` documentam checklist e tela principal no navegador.

A leitura por Narrator/VoiceOver, ordem de foco, corte de texto a 125/150/200% DPI e renderização nativa permanecem pendentes. O contraste de oito combinações de texto Windows/prévia está registrado em `docs/qa/contrast.json`. Isso não comprova acessibilidade completa.
