# Simulador de Tiro Embarcado em RV

Simulador de Realidade Virtual para treinamento de tiro embarcado em helicóptero (AS350 Esquilo), voltado aos Operadores Aerotáticos (OAT) da PMMG. É a continuação do simulador desenvolvido por Caetano (2024, PPGEELT/UFU) e serve de base ao TCC de Lucas Ferreira Resende (FACOM/UFU), que acrescenta alvos móveis ao treinamento.

## Versões

| Item | Versão |
|---|---|
| Unity | **6000.3.27f1** (Unity 6.3 LTS) |
| Render pipeline | URP 17.3 |
| XR | OpenXR 1.16, XR Plug-in Management 4.6, XR Interaction Toolkit 3.3 |
| Input | Input System 1.20 (com o Input Manager antigo ainda ativo, usado pelo Silantro) |
| Alvos móveis (TCC) | AI Navigation 2.0, Splines 2.9 |

O projeto original estava no Unity 2021.3.45f2; o primeiro commit deste repositório preserva esse estado.

## Cenas

1. `Assets/Scenes/1 Start Scene.unity` — menu inicial.
2. `Assets/Scenes/cena principal.unity` — cidade, helicóptero e alvo (carregada pelo menu).

## Como executar

### Com óculos VR (PC Windows)

Conecte o headset ao PC (Quest Link/Air Link, PICO Connect ou SteamVR) com o runtime OpenXR ativo e dê Play. O OAT usa o fuzil com os controles; o instrutor pilota o helicóptero pelo teclado ou joystick.

### Sem óculos (modo desktop)

Se nenhum headset estiver ativo, o modo desktop liga sozinho (`Assets/TCC/ModoDesktop/ModoDesktop.cs`). Também pode ser forçado em um executável com o argumento `-desktop`.

| Ação | Controle |
|---|---|
| Mirar | Mouse |
| Atirar | Clique esquerdo |
| Zoom | Segurar botão direito |
| Recarregar | T |
| Alternar para a câmera do helicóptero | H |
| Liberar o cursor | Esc (clique para voltar) |
| Menu inicial | Segurar botão direito para olhar; clicar nos botões |

O disparo passa pelo mesmo `AutoGun` do VR, então acertos, cronômetro e pontuação são os mesmos nos dois modos.

### Piloto automático

Opcional, para o instrutor se concentrar no treinamento. Com **P** liga/desliga e com **O** troca o padrão de voo, todos com a porta do operador (esquerda) voltada para a área de treinamento:

| Padrão | Voo |
|---|---|
| Órbita | Círculo em torno da área (padrão: 300 ft, raio de 60 m, 20 kt) |
| Deslocamento linear | Circuito oval com passagens retas pela área (40 kt nas retas) |
| Pairado | Para onde a frenagem terminar e paira |

Se o motor estiver desligado, ele dá a partida e decola na vertical. A altura sobe sozinha se algum prédio ficar a menos de 20 m da trajetória. Ao desligar, a aeronave volta para a física do Silantro mantendo a velocidade. Os parâmetros ficam no componente `PilotoAutomatico` do helicóptero.

### Alvos móveis

Além do tambor fixo, a cena principal tem (objeto `[TCC] Treinamento`):

- **Veículos** (`VeiculoAlvo`): 4 carros em dois circuitos sobre as ruas ao sul do alvo (rotas em Splines, mão direita), de 30 a 55 km/h. Atingido, o carro para por alguns segundos e retoma o percurso.
- **Pessoas em fuga** (`PessoaEmFuga`): 6 pessoas na praça do alvo. Máquina de estados Parada → Alerta → Fugindo → Escondida → Retornando; ao perceber o helicóptero, fogem da posição prevista da aeronave pela NavMesh da área. Atingida, a pessoa cai e reaparece depois de alguns segundos.
- **Placar** (`GerenciadorTreinamento`): disparos e acertos por tipo de alvo, no canto da tela.

Para recriar o cenário (rotas, NavMesh, veículos, pessoas e piloto automático): menu **TCC → Configurar cenário de treinamento**.

### Pilotagem (Silantro)

F1/F2 liga/desliga o motor, 1/2 coletivo, W/S/A/D cíclico, Q/E pedais, C troca a câmera do helicóptero, V liga a luz e **R reinicia a cena**.

## Assets de terceiros

O projeto inclui assets da Unity Asset Store (Silantro Helicopter Simulator Toolkit, Auto Hand, Fantastic City Generator, entre outros), sujeitos às licenças de seus autores. Por isso o repositório deve permanecer **privado**.
