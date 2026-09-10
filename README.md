# LetMeSee

Receptor de espelhamento de tela iOS (AirPlay) para estacoes de trabalho Windows.

Permite transmitir audio e video de iPhones e iPads diretamente para o computador em tempo real, sem a necessidade de instalar aplicativos ou perfis adicionais no dispositivo movel.

---

## Como Instalar e Usar

### Opcao 1: Instalador Oficial do Windows (Recomendado)

A forma mais simples e pratica para a equipe:

1. Acesse a pagina de [Releases do LetMeSee](https://github.com/francisco-efjr/LetMeSee/releases).
2. Baixe o instalador **`LetMeSee-Setup.exe`**.
3. Execute o instalador ("Avancar > Avancar > Concluir").
   * O instalador cuida de tudo: cria as regras no Windows Firewall, instala o servico Bonjour e adiciona o icone na Area de Trabalho.
4. Abra o **LetMeSee** pelo atalho na Area de Trabalho.

---

### Opcao 2: Execucao Portatil via Git

Para desenvolvedores ou estacoes que preferem clonar o projeto:

```bash
git clone https://github.com/francisco-efjr/LetMeSee.git
cd LetMeSee
```

1. Execute **`configurar_firewall.bat`** como Administrador (apenas no primeiro dia).
2. De dois cliques em **`iniciar.bat`** (ele compila e abre a interface visual automaticamente).

---

## A Interface Visual (LetMeSee.exe)

A aplicacao possui uma interface compacta e moderna, sem necessidade de telas pretas de terminal:

* **Botao Liga / Desliga:** Inicia ou interrompe o espelhamento a qualquer momento com um clique.
* **Nome no iPhone:** Exibe o nome exato para conexao (ex: `LetMeSee - seu.usuario`), com botao de copiar.
* **IP da Rede:** Mostra o endereco IP local ativo para facilitar a validacao.
* **Bandeja do Sistema (Tray):** Ao fechar a janela, o aplicativo pode continuar em execucao silenciosa proximo ao relogio do Windows.

---

## Como Conectar pelo iPhone / iPad

1. Conecte o iPhone na **mesma rede Wi-Fi** do computador.
2. Abra a **Central de Controle** (deslize o dedo a partir do canto superior direito da tela).
3. Toque no botao **Espelhar a Tela** (icone com dois retangulos sobrepostos).
4. Selecione o identificador do seu computador: **`LetMeSee - [seu.usuario]`**.
5. A tela do dispositivo sera transmitida instantaneamente.

---

## Estrutura do Projeto

```text
LetMeSee/
├── src/
│   └── LetMeSeeApp.cs        # Codigo-fonte C# da interface grafica
├── build_ui.bat              # Compilador local da interface via csc.exe nativo
├── installer/
│   └── setup.iss             # Script Inno Setup para gerar LetMeSee-Setup.exe
├── .github/
│   └── workflows/
│       └── build-installer.yml # Esteira automatica de CI/CD para compilar o instalador
├── iniciar.bat               # Launcher inteligente (abre a interface visual)
├── configurar_firewall.bat   # Script de configuracao do Firewall do Windows
├── diagnostico.bat           # Utilitario de diagnostico de rede e portas
├── bin/                      # Motores de video (UxPlay, GStreamer, Bonjour)
├── scripts/                  # Automacoes PowerShell de execucao e download
└── README.md                 # Documentacao oficial
```

---

## Resolucao de Problemas Comuns

| Sintoma | Causa Provavel | Acao Recomendada |
| :--- | :--- | :--- |
| O computador nao aparece no iPhone | Dispositivos em redes diferentes | Certifique-se de que ambos estao no mesmo Wi-Fi. |
| O computador nao aparece no iPhone | Firewall bloqueando portas | Execute `configurar_firewall.bat` como Administrador ou reinstale via `LetMeSee-Setup.exe`. |
| Uso simultaneo com VPN Corporativa | VPN bloqueando rede local | Nas configuracoes da VPN, habilite **"Allow local LAN access"**. |
| Rede Guest / Visitante | Isolamento de clientes (AP Isolation) | Utilize a rede corporativa principal; redes de visitantes bloqueiam conexoes locais. |
| Validacao de IPs | Conexao ativa | Execute `diagnostico.bat` para verificar os IPs locais da maquina. |
