# ADR-002 · Preparação adaptativa e adaptadores por plataforma

Status: implementado em desenvolvimento; testes físicos pendentes.

## Decisão

Separar **preparação** e **espelhamento**. O checklist identifica requisitos por sistema, família do dispositivo e método; só deixa concluir quando não há bloqueios ou confirmações pendentes. A tela de espelhamento tem dois controles: Ambiente e Começar/Parar. Nas rotas Apple nativas, a segunda ação conclui a orientação, sem alegar encerrar um serviço do sistema.

Preservar WinForms/.NET Framework no Windows e criar SwiftUI no macOS, usando a toolchain já disponível. Os domínios de checklist são testáveis sem hardware em ambas as plataformas. A documentação de métodos e cenários é o contrato comum; as provas de capacidade dependem da plataforma e não são artificialmente tratadas como iguais.

| Plataforma | Adaptador | Prova automática | Confirmação ou continuação manual |
| --- | --- | --- | --- |
| Windows/iOS | UxPlay fixado | Integridade, rede/perfil, portas, serviço e regras quando consultáveis | Rede do celular, privacidade, Central de Controle e pareamento |
| Windows/Android | scrcpy fixado + ADB privado | Inventário completo, serial/estado/transporte e processo próprio | Depuração/RSA e pareamento Wi-Fi anterior |
| macOS/Android | scrcpy/adb existentes aprovados | Hashes, versões, dispositivos e processo próprio | Procedência dos arquivos, depuração/RSA, pareamento anterior |
| macOS/iOS sem fio | Receptor AirPlay | Recursos locais consultáveis; sem API pública confiável para sessão | Suporte do Mac, Receptor habilitado, rede e continuação no iPhone |
| macOS/iOS cabo | QuickTime | Presença do aplicativo | Cabo/confiança, escolher a fonte e parar a prévia no QuickTime |

## Fronteiras

- Resultado conhecido inválido não vira pronto ao marcar uma caixa.
- Sistema configurado evita downloads, mas não dispensa nova verificação.
- Modo técnico não remove validação; modo guiado não esconde um bloqueio.
- Aprovações expiram e não são restauradas de disco como concluídas. A checagem é repetida antes de iniciar.
- Parar/cancelar invalida a intenção assíncrona para não iniciar depois de o usuário cancelar.
- Probes ADB privados são encerrados antes de iniciar outro adaptador; nunca há `kill-server` global.
- Motor Android fica fora de `bin` AirPlay para não quebrar seu inventário nem misturar políticas de confiança.
- Não existe “auto” que instale dependências, habilite depuração, abra porta remota ou altere política corporativa silenciosamente.

## Consequências

O fluxo final é simples porque a complexidade fica antes dele, com ajuda direcionada. AirPlay e QuickTime mantêm etapas humanas inevitáveis; elas são explicadas, não simuladas. USB permite Android sem rede e iPhone no Mac sem depender de AirPlay. iPhone por USB no Windows e espelhamento automático de uma sessão Apple não são promessas deste incremento.

O checklist cobre as jornadas documentadas em `docs/qa/SCENARIOS.md`, sem afirmar cobrir toda combinação futura de hardware, driver, versão, DRM ou política corporativa.
