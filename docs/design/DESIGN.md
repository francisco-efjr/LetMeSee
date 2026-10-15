# LetMeSee · Contrato de design

Produto: receptor AirPlay para Windows. Tarefa principal: iniciar deliberadamente uma sessão, localizar o nome no iPhone e encerrar com clareza.

## Origem e adaptação

- [Mo baile](https://github.com/francisco-efjr/mo-baile): separação interface/motor, tokens semânticos, estados por símbolo + texto + cor, escala de 4, fontes do sistema, foco e acessibilidade.
- Bundle da casa: `brain-of-agent/design-systems/mo-baile`, consultado em 2026-10-15. Tokens originais de espaço/forma e contrato de componentes serviram de estrutura.
- [Pulse / R6, referência enviada](https://steamcommunity.com/sharedfiles/filedetails/?id=1408618718): grafite esverdeado, laranja queimado, off-white e linguagem de radar. O GIF não é carregado do Steam pelo aplicativo; a animação é um radar original desenhado localmente.
- Anexo “Grainy Cardboard Overlays”: referência de granulação/material. A imagem comercial não foi incorporada ao produto; a textura é gerada com semente fixa e baixa opacidade.

## Tokens

Fonte de verdade nativa: `src/UI/DesignTokens.cs`. A documentação visual em `preview.html` espelha a paleta; não substitui o renderer WinForms.

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

Fontes: Segoe UI para interface, Consolas para rótulos técnicos. Grade de 4; raio de cartão 12; altura de controle de ação 40, ação principal 48. Textura só no hero. Conteúdo e campos são opacos.

No alto contraste os tokens usam SystemColors; não há textura nem animação. A animação de abertura dura 1,2 s, não fica em loop e observa a preferência nativa `SPI_GETCLIENTAREAANIMATION`, além da preferência local. Mudanças de alto contraste precisam de reinício.

## Componentes e estados

- `SurfacePanel`: agrupa conteúdo com raio e borda discreta. A variante Paper é exclusiva do hero, sem campos de formulário sobre textura.
- `ActionButton`: verbo + nome acessível, foco visível, navegação por Tab; ações nativas mantêm atalhos por `&`.
- `SonarPanel`: ilustração sem função de status. O indicador textual é a fonte de estado.
- Espelhamento: parado, verificando, processo ativo e erro. Nunca exibir “conectado” com base só no processo.
- Preferências: habilitadas apenas com receptor parado; salvar confirma apenas após sucesso da escrita.
- Diagnóstico: carregando e resultado/erro, sem executar reparos ou solicitar UAC na leitura.

## Prévia e limites

`preview.html` funciona localmente sem fontes, bibliotecas ou serviços externos. Os endereços e o estado ativo são marcados como ilustrativos. Preferências da prévia vivem só na memória da página; o app salva XML. A captura `preview.png` documenta o navegador, não o aplicativo Windows.

A leitura por Narrator, ordem de foco, corte de texto a 125/150/200% DPI e renderização nativa permanecem pendentes no Windows. O contraste das combinações de texto está registrado em `docs/qa/contrast.json`.
