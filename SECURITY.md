# Segurança e privacidade

## Modelo de uso

O LetMeSee recebe áudio e vídeo de dispositivos AirPlay na rede local. O uso previsto é Windows 10/11 x64 em rede privada confiável. A interface roda com `asInvoker`; elevar o aplicativo inteiro não é necessário. Somente instalação em Program Files e configuração de firewall usam UAC.

## Fronteiras e controles

| Fronteira | Controle implementado |
| --- | --- |
| Distribuição → máquina | URL/versão/SHA-256 fixados em `config/dependencies.json`; sem URL por variável de ambiente |
| ZIP → disco | Staging único; rejeição de traversal, caminhos absolutos, colisões e links; limites de quantidade/tamanho; rollback |
| Runtime → processo | Manifesto SHA-256; bloqueio de arquivos alterados, ausentes, links ou executáveis não listados |
| Processo → bibliotecas | Caminhos locais de GStreamer, isolamento de plugins/cache e remoção de variáveis herdadas de debug/config |
| UxPlay → preferências globais | Arquivo de opções vazio por sessão; não herda gravação/debug de `.uxplayrc` |
| Rede → receptor | Início explícito, código AirPlay temporário ativado por padrão e desligamento manual/por tempo |
| Firewall → Windows | Regras por executável/porta, `Private` + `LocalSubnet`; sem modificar categoria de rede; remoção na desinstalação |
| Encerramento → processos | Identidade por sessão, cancelamento de início e Windows Job Object com término de descendentes ao fechar |
| Preferências → dados | XML local pequeno, DTD e resolver desativados, validação e substituição atômica |

O nome padrão é `LetMeSee`. A opção `-nh` impede acrescentar hostname ao anúncio. IPs e interfaces são mostrados localmente; não são exportados por padrão. O PIN não é persistido nas preferências. Saída do motor é drenada para um buffer limitado em memória e apagada ao encerrar. O aplicativo não solicita gravação de mídia.

## Limites importantes

- A base binária existente é UxPlay 1.73.6 com dependências nativas. Fixar o hash preserva integridade e rastreabilidade; **não equivale a auditoria de CVEs** ou build reproduzível de cada biblioteca.
- O manifesto local detecta corrupção e alterações em arquivos. Quem consegue substituir o runtime **e** seu manifesto também pode contornar esse controle. A instalação em Program Files oferece a fronteira de permissões do Windows; a cópia portátil depende das permissões da pasta escolhida.
- Ainda não há assinatura Authenticode configurada para o executável/instalador. Antes de distribuição pública, validar no Windows e preparar assinatura com certificado do mantenedor.
- O código AirPlay é curto, temporário e compatível com o protocolo. Não transforma uma rede pública em ambiente confiável. Não há detecção implementada de cliente conectado ou promessa de bloqueio por interface na lista da UI.
- A recepção real, UAC, remoção das regras, áudio e DPI precisam de validação Windows com iPhone/iPad. A prévia HTML não testa esses controles.
- A consulta de Bonjour informa presença/estado, não instala serviços. Instalação de MSI de terceiros nunca acontece silenciosamente.
- O alto contraste é lido ao iniciar. Reinicie o aplicativo após mudar essa preferência do Windows.

## Relatar problemas

Não publique PINs, gravações de tela, endereços privados ou dados pessoais em issues. Para falhas sem dados sensíveis, descreva versão, passos mínimos, comportamento esperado e resultado, com dados de exemplo. Para vulnerabilidades, use os recursos de relato privado do repositório caso estejam habilitados; não há um canal privado adicional configurado nesta entrega.
