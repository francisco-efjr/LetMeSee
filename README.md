# LetMeSee

Receptor AirPlay para espelhar iPhone e iPad em um computador **Windows 10/11 x64**. Interface nativa em C#/WinForms, sem conta e sem telemetria implementada pelo aplicativo.

![Prévia visual da interface](docs/design/preview.png)

> A imagem é uma prévia de design renderizada no navegador. A recepção real usa o aplicativo Windows e abre o vídeo em uma janela do UxPlay. Esta branch ainda precisa da validação nativa descrita em [COMO-TESTAR.md](COMO-TESTAR.md).

## Conectar

1. Instale uma versão verificada do LetMeSee. O aplicativo começa **desligado**.
2. Conecte o PC e o iPhone à mesma rede privada de confiança. No instalador, o acesso de firewall é opcional; também pode ser configurado em **Diagnóstico → Configurar firewall…**.
3. Clique em **Iniciar receptor**. O aplicativo confere os arquivos do motor antes de executá-lo.
4. No iPhone, abra **Central de Controle → Espelhar a Tela → LetMeSee** (ou o nome definido em Preferências).
5. Digite o código AirPlay mostrado no aplicativo. Um código novo é gerado para cada sessão do receptor; a exigência de código vem ativada por padrão.
6. Clique em **Parar receptor** ao terminar. Fechar a janela encerra o aplicativo por padrão. A permanência na bandeja precisa ser ativada em Preferências.

O estado **Pronto para receber** indica que o processo do motor está ativo. Ele não confirma que um iPhone já está conectado. A lista de endereços serve para conferência; não seleciona a interface de escuta.

## Funcionalidades

- Nome personalizável, sem acrescentar nome de usuário ou hostname ao anúncio.
- Código de conexão temporário, áudio opcional e encerramento automático após 15, 30 ou 60 minutos.
- Preferências locais por usuário, em `%LOCALAPPDATA%\LetMeSee\settings.xml`.
- Diagnóstico do runtime, interfaces IPv4 e serviço Bonjour sem administrador e sem enviar dados.
- Bandeja opcional com ações para abrir, parar e sair.
- Tokens de design inspirados na estrutura do Mo baile, cores do Pulse e textura local de papel. A abertura usa um radar original e respeita a configuração de animação do Windows.

## Compilar e testar no Windows

Requer .NET Framework 4.8 e Windows PowerShell 5.1. O projeto usa o compilador `csc.exe` já fornecido pelo Framework. Não há gerenciador de pacotes para a interface.

```powershell
.\build_ui.bat
powershell -NoProfile -File .\tests\run-core-tests.ps1
powershell -NoProfile -File .\tests\dependency_security.tests.ps1
```

Para preparar o runtime, faça a operação explícita abaixo. O script aceita somente a URL e o SHA-256 revisados em `config/dependencies.json`, extrai em staging e confere os arquivos antes de substituir `bin`:

```powershell
powershell -NoProfile -File .\scripts\download_deps.ps1
.\iniciar.bat
```

Não execute o script de download como administrador para uso portátil. Na instalação em `Program Files`, os arquivos são preparados durante o build e instalados pelo instalador. O aplicativo não baixa executáveis ao abrir ou iniciar a recepção.

O pipeline de pull request compila e testa a interface. O empacotamento do workflow prepara o runtime verificado, compila Inno Setup e gera SHA-256 dos artefatos. Não publica nem sobrescreve releases automaticamente.

## Estrutura

```text
src/
  Core/                 # Preferências e contrato dos argumentos do motor
  Infrastructure/       # Runtime, processo, rede e diagnóstico
  UI/                   # Tokens, componentes e radar
  LetMeSeeApp.cs         # Janela, navegação e apresentação dos estados
  app.manifest          # Execução sem elevação e DPI
config/                 # Dependência com versão, origem e hash fixados
scripts/                # Download explícito, diagnóstico e firewall
installer/              # Instalador offline e acesso de rede opt-in
tests/                  # Casos negativos de integridade, arquivo, entrada e processo
docs/                   # Arquitetura, design e evidências de validação
```

## Rede e segurança

As regras criadas por esta versão aceitam **somente perfil Privado, sub-rede local e executáveis específicos**. O app não altera a categoria da sua rede, não desativa o firewall e não instala Bonjour silenciosamente. Se o pacote depender do serviço Bonjour e ele estiver ausente, instale-o por uma fonte oficial e verifique novamente o diagnóstico.

Use **Diagnóstico** se o receptor não aparecer. Confirme a mesma rede, se o Wi-Fi permite comunicação entre clientes e se a VPN permite acesso local. Não é necessário desativar as proteções do Windows.

O SHA-256 impede usar um pacote diferente do que foi revisado. Não prova ausência de vulnerabilidades nem substitui assinatura do instalador. As limitações e o modelo de ameaças estão em [SECURITY.md](SECURITY.md); componentes de terceiros, em [THIRD_PARTY.md](THIRD_PARTY.md).

Veja [MELHORIAS.md](MELHORIAS.md), [COMO-TESTAR.md](COMO-TESTAR.md) e [o contrato de design](docs/design/DESIGN.md).
