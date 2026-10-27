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

### Pilotagem (Silantro)

F1/F2 liga/desliga o motor, 1/2 coletivo, W/S/A/D cíclico, Q/E pedais, C troca a câmera do helicóptero, V liga a luz e **R reinicia a cena**.

## Assets de terceiros

O projeto inclui assets da Unity Asset Store (Silantro Helicopter Simulator Toolkit, Auto Hand, Fantastic City Generator, entre outros), sujeitos às licenças de seus autores. Por isso o repositório deve permanecer **privado**.
