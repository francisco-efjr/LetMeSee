# LetMeSee

Receptor de espelhamento de tela iOS (AirPlay) para estações de trabalho Windows.

Permite transmitir áudio e vídeo de iPhones e iPads diretamente para o computador, em tempo real, sem a necessidade de instalar aplicativos ou perfis adicionais no dispositivo móvel.

---

## Pré-requisitos

* Computador com Windows 10 ou 11.
* iPhone ou iPad conectado à **mesma rede local/Wi-Fi** do computador.

---

## Como Instalar e Usar

### Passo 1: Clonar ou Baixar o Projeto
```bash
# Via SSH:
git clone git@github.com:francisco-efjr/LetMeSee.git

# Ou via HTTPS:
git clone https://github.com/francisco-efjr/LetMeSee.git

cd LetMeSee
```
*(Ou baixe e extraia o arquivo ZIP do repositório em uma pasta local).*

---

### Passo 2: Configurar o Firewall (Apenas no primeiro uso)
O Windows bloqueia portas de recepção por padrão. Para liberar as regras de rede:

1. Clique com o botão direito no arquivo **`configurar_firewall.bat`**.
2. Selecione **Executar como Administrador**.
3. Confirme na tela de segurança e aguarde a mensagem de sucesso.

---

### Passo 3: Iniciar o Serviço
1. Dê dois cliques em **`iniciar.bat`**.
   *(Na primeira execução, o script baixará automaticamente os componentes e codecs necessários de forma transparente).*
2. Uma janela será aberta aguardando a conexão com a identificação:
   ```text
   LetMeSee - [seu.usuario]
   ```

---

### Passo 4: Conectar o Dispositivo iOS
1. No iPhone ou iPad, abra a **Central de Controle** (deslize o dedo do canto superior direito para baixo).
2. Toque no botão **Espelhar a Tela** (ícone com dois retângulos).
3. Selecione o identificador do seu computador: **`LetMeSee - [seu.usuario]`**.
4. A tela do dispositivo será projetada instantaneamente.

> Para finalizar a transmissão, feche a janela no computador ou toque em **Parar Espelhamento** no celular.

---

## Estrutura do Projeto

```text
LetMeSee/
├── iniciar.bat               # Inicializador do serviço de espelhamento
├── configurar_firewall.bat   # Script de configuração de regras de rede (Admin)
├── diagnostico.bat           # Utilitário de diagnóstico de conexão
├── bin/                      # Binários de execução e bibliotecas de vídeo
├── scripts/                  # Automações PowerShell de execução e firewall
└── README.md                 # Documentação de uso
```

---

## Resolução de Problemas Comuns

| Sintoma | Causa Provável | Ação Recomendada |
| :--- | :--- | :--- |
| O computador não aparece no iPhone | Dispositivos em redes diferentes | Certifique-se de que ambos estão no mesmo Wi-Fi. |
| O computador não aparece no iPhone | Firewall bloqueando portas | Execute `configurar_firewall.bat` como Administrador. |
| Uso simultâneo com VPN Corporativa | VPN bloqueando rede local | Nas configurações da sua VPN, habilite a opção **"Allow local LAN access"**. |
| Rede Guest / Visitante | Isolamento de clientes (AP Isolation) | Utilize a rede corporativa padrão; redes de visitantes bloqueiam tráfego entre aparelhos. |
| Dúvidas sobre endereçamento IP | Conexão de rede ativa | Execute `diagnostico.bat` para visualizar os IPs locais atribuídos à máquina. |

---

## Distribuição Interna (Equipe de TI / Mantenedores)

Para disponibilizar o projeto pronto para uso imediato aos colaboradores:
1. Mantenha o executável `uxplay.exe` e suas respectivas bibliotecas DLL dentro da pasta `bin/`.
2. Efetue o commit da pasta `bin/` no repositório corporativo. Dessa forma, qualquer membro da equipe precisará apenas clonar e executar, sem etapas de compilação ou download externo.
