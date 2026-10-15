# Componentes e referências de terceiros

## Runtime existente

O pacote mantido pelo projeto foi baixado e seu SHA-256 calculado em 2026-10-15. A fixação não atesta ausência de vulnerabilidades nem substitui a revisão do conteúdo.

- Origem: [LetMeSee v1.0.0 · LetMeSee-bin.zip](https://github.com/francisco-efjr/LetMeSee/releases/tag/v1.0.0).
- Asset GitHub: `555307335`; tamanho observado: `128990344` bytes.
- SHA-256: `ac27a5c26b95d941302a53999727c28154e867c90aed8e0e8339f9270ee3f212`.
- UxPlay 1.73.6: versão identificada no executável. [Projeto e fontes](https://github.com/FDH2/UxPlay/tree/v1.73.6), [manual dessa versão](https://github.com/FDH2/UxPlay/blob/v1.73.6/uxplay.1). O upstream informa GPL v3 ou posterior e partes sob LGPL/MIT; consulte seus arquivos de licença para os termos completos.
- GStreamer e plugins: presentes em `bin/` e `lib/gstreamer-1.0/`. A versão exata de todas as DLLs e suas licenças **não foram inventariadas** nesta entrega. [Origem do projeto](https://gstreamer.freedesktop.org/).
- Bonjour: MSI presente em `third_party/Bonjour64.msi` no pacote legado. Esta versão do LetMeSee não o executa nem o instala automaticamente. [Origem Apple](https://support.apple.com/bonjour).

O ZIP legado não contém `LICENSE`, `COPYING` ou um inventário completo de licenças. **A distribuição pública do instalador depende de completar o inventário, avisos e fontes correspondentes**, além dos testes nativos. O pipeline desta branch não publica releases. Não se deve interpretar este documento como uma licença concedida pelos autores dos componentes.

## Identidade visual

A paleta é inspirada na [referência de Pulse fornecida pelo usuário](https://steamcommunity.com/sharedfiles/filedetails/?id=1408618718). A textura e o radar da interface foram desenhados em código; a arte GIF do Steam e o anexo comercial não são distribuídos no produto. O ícone anterior do projeto foi preservado.

O [Mo baile](https://github.com/francisco-efjr/mo-baile) e o [Brain of Agent](https://github.com/francisco-efjr/brain-of-agent) serviram de referência de estrutura, tokens e processo. Não foram incorporados como bibliotecas executadas pelo app.
