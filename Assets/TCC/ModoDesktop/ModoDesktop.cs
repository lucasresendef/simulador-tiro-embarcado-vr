using System;
using System.Linq;
using Autohand;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

// Modo desktop: permite usar o simulador sem óculos VR (ex.: rodando no Mac).
// Se nenhum headset estiver ativo (ou se o executável for aberto com "-desktop"),
// a câmera da cabeça passa a ser controlada pelo mouse, as mãos do AutoHand são
// desligadas e o fuzil fica preso à câmera, apontando para a mira no centro da tela.
// O disparo continua passando pelo AutoGun, então acertos e pontuação são os mesmos do VR.
// Teclas evitam as do Silantro (R reinicia a cena, V liga a luz).
// Com óculos conectado, não faz nada.
public static class ModoDesktop
{
    public static bool Ativo { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool forcado = Environment.GetCommandLineArgs().Contains("-desktop");
        Ativo = forcado || !XRSettings.isDeviceActive;
        if (!Ativo) return;

        // Roda dentro do sceneLoaded, antes do Start dos objetos da cena,
        // para impedir que as mãos peguem o fuzil (AdvancedInitialGrab).
        new GameObject("[Modo Desktop]").AddComponent<ControleDesktop>();
    }
}

// Executa antes do AutoGun, para que o FixedUpdate realinhe o fuzil antes do disparo
[DefaultExecutionOrder(-100)]
public class ControleDesktop : MonoBehaviour
{
    // Altura dos olhos do operador sentado, usada quando o rig não tem rastreamento
    const float AlturaOlhos = 1.2f;
    const float Sensibilidade = 0.1f;
    const float FovZoom = 30f;
    // Posição do cano em relação à câmera (direita, baixo, frente)
    static readonly Vector3 OffsetCano = new Vector3(0.12f, -0.18f, 0.8f);

    Camera cabeca;
    Camera cameraHelicoptero;
    AutoGun fuzil;
    AutoAmmo modeloCarregador;
    int capacidadeCarregador;
    float yaw, pitch, fovNormal;
    bool modoTiro;
    bool ouvinteAjustado;
    Vector3 pontoMirado;

    void Awake()
    {
        cabeca = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(TemTrackedPoseDriver);
        if (cabeca == null) cabeca = Camera.main;
        if (cabeca == null)
        {
            Debug.LogWarning("[Modo desktop] Nenhuma câmera encontrada na cena.");
            enabled = false;
            return;
        }

        PrepararCamera();
        PrepararInterface();
        PrepararCameraHelicoptero();
        PrepararFuzil();

        // O inventário preso ao corpo só segue a cabeça do VR e gera erro a cada quadro sem óculos
        foreach (var inventario in FindObjectsByType<BodySocketInventory>(FindObjectsSortMode.None))
            inventario.enabled = false;

        modoTiro = fuzil != null;
        if (!modoTiro) OlharParaMenu();
        Debug.Log($"[Modo desktop] Ativo na cena '{SceneManager.GetActiveScene().name}' (câmera '{cabeca.name}', fuzil: {(modoTiro ? fuzil.name : "nenhum")}).");
    }

    void PrepararCamera()
    {
        foreach (var componente in cabeca.GetComponents<Behaviour>())
            if (componente != null && componente.GetType().Name == "TrackedPoseDriver")
                componente.enabled = false;

        var local = cabeca.transform.localPosition;
        if (local.y < 0.1f)
            cabeca.transform.localPosition = new Vector3(local.x, AlturaOlhos, local.z);

        var angulos = cabeca.transform.localEulerAngles;
        yaw = angulos.y;
        pitch = Mathf.DeltaAngle(0f, angulos.x);
        fovNormal = cabeca.fieldOfView;
    }

    // Sem fuzil (menu): começa olhando para o painel ativo, que no VR fica à altura dos olhos
    void OlharParaMenu()
    {
        var painel = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.renderMode == RenderMode.WorldSpace && c.isActiveAndEnabled);
        if (painel == null) return;
        var direcao = painel.transform.position - cabeca.transform.position;
        if (cabeca.transform.parent != null) direcao = cabeca.transform.parent.InverseTransformDirection(direcao);
        var angulos = Quaternion.LookRotation(direcao).eulerAngles;
        yaw = angulos.y;
        // Um pouco acima do centro dos botões, para o título também aparecer
        pitch = Mathf.DeltaAngle(0f, angulos.x) - 8f;
        cabeca.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // Os menus usam só raycasters de controle VR; adiciona o raycaster de mouse
    void PrepararInterface()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.renderMode != RenderMode.WorldSpace) continue;
            if (canvas.worldCamera == null) canvas.worldCamera = cabeca;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    // Os scripts de câmera do helicóptero (Silantro) guardam a câmera externa no campo "normalExterior"
    void PrepararCameraHelicoptero()
    {
        foreach (var camera in FindObjectsByType<PhantomCamera>(FindObjectsSortMode.None))
        {
            if (camera.normalExterior == null) continue;
            // Só é ativada ao apertar H; ativa desde o início, o Silantro a liga no Start
            cameraHelicoptero = camera.normalExterior;
            return;
        }
    }

    void PrepararFuzil()
    {
        fuzil = FindFirstObjectByType<AutoGun>();
        if (fuzil == null) return;

        foreach (var pegada in FindObjectsByType<AdvancedInitialGrab>(FindObjectsSortMode.None))
            pegada.enabled = false;
        foreach (var mao in FindObjectsByType<Autohand.Hand>(FindObjectsSortMode.None))
            mao.gameObject.SetActive(false);

        var corpo = fuzil.GetComponent<Rigidbody>();
        if (corpo != null)
        {
            corpo.isKinematic = true;
            corpo.interpolation = RigidbodyInterpolation.None;
        }
        fuzil.transform.SetParent(cabeca.transform, true);

        // Guarda uma cópia desativada de um carregador para recarregar com a tecla T
        var carregador = FindObjectsByType<AutoAmmo>(FindObjectsSortMode.None).FirstOrDefault();
        if (carregador != null)
        {
            capacidadeCarregador = carregador.currentAmmo;
            bool ativo = carregador.gameObject.activeSelf;
            carregador.gameObject.SetActive(false);
            modeloCarregador = Instantiate(carregador);
            modeloCarregador.name = "[Modo Desktop] Modelo de carregador";
            carregador.gameObject.SetActive(ativo);
        }
    }

    void Start()
    {
        if (modoTiro)
        {
            bool semCarregador = fuzil.magazinePoint != null && fuzil.magazinePoint.GetPlacedObject() == null;
            if (fuzil.GetAmmo() == 0 || semCarregador) Recarregar();
            TravarCursor(true);
        }
    }

    void Update()
    {
        var mouse = Mouse.current;
        var teclado = Keyboard.current;
        if (mouse == null || teclado == null) return;

        // No primeiro quadro, depois do Start de todos os objetos da cena
        if (!ouvinteAjustado)
        {
            OuvirPor(cabeca);
            ouvinteAjustado = true;
        }

        if (teclado.escapeKey.wasPressedThisFrame) TravarCursor(false);
        if (teclado.hKey.wasPressedThisFrame) AlternarCameraHelicoptero();

        bool cursorTravado = Cursor.lockState == CursorLockMode.Locked;
        // Sem fuzil (ex.: menu) o cursor fica livre e a visão gira segurando o botão direito
        if (cursorTravado || (!modoTiro && mouse.rightButton.isPressed))
        {
            var delta = mouse.delta.ReadValue() * Sensibilidade;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
            cabeca.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        if (!modoTiro) return;

        if (!cursorTravado)
        {
            if (mouse.leftButton.wasPressedThisFrame) TravarCursor(true);
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame) fuzil.PressTrigger();
        if (mouse.leftButton.wasReleasedThisFrame) fuzil.ReleaseTrigger();
        if (teclado.tKey.wasPressedThisFrame) Recarregar();
        cabeca.fieldOfView = mouse.rightButton.isPressed ? FovZoom : fovNormal;
    }

    // Posiciona o fuzil depois da câmera se mover, com o cano apontando para o ponto sob a mira
    void LateUpdate()
    {
        if (!modoTiro || fuzil.shootForward == null) return;
        pontoMirado = PontoSobAMira();
        ApontarPara(pontoMirado);
    }

    // O AutoGun dispara no passo de física. Com mais de um passo por quadro, a aeronave já se
    // moveu e girou em relação ao que o operador viu; por isso o cano aponta para o ponto do
    // mundo que estava sob a mira no último quadro (os alvos só se movem no Update)
    void FixedUpdate()
    {
        if (!modoTiro || fuzil.shootForward == null) return;
        Physics.SyncTransforms();
        ApontarPara(pontoMirado);
    }

    Vector3 PontoSobAMira()
    {
        var raio = new Ray(cabeca.transform.position, cabeca.transform.forward);
        foreach (var hit in Physics.RaycastAll(raio, fuzil.maxHitDistance, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            // Ignora o próprio fuzil e a estrutura do helicóptero logo à frente da câmera
            if (hit.distance < 2f || hit.transform.IsChildOf(fuzil.transform)) continue;
            return hit.point;
        }
        return raio.GetPoint(fuzil.maxHitDistance);
    }

    void ApontarPara(Vector3 alvo)
    {
        var cano = fuzil.shootForward;
        var canoLocalPos = fuzil.transform.InverseTransformPoint(cano.position);
        var canoLocalRot = Quaternion.Inverse(fuzil.transform.rotation) * cano.rotation;
        var posicaoCano = cabeca.transform.TransformPoint(OffsetCano);
        var rotacaoCano = Quaternion.LookRotation(alvo - posicaoCano, cabeca.transform.up);

        fuzil.transform.rotation = rotacaoCano * Quaternion.Inverse(canoLocalRot);
        fuzil.transform.position += posicaoCano - fuzil.transform.TransformPoint(canoLocalPos);
    }

    void Recarregar()
    {
        if (fuzil.magazinePoint == null || modeloCarregador == null) return;

        var atual = fuzil.magazinePoint.GetPlacedObject();
        if (atual != null)
        {
            fuzil.magazinePoint.Remove();
            Destroy(atual.gameObject);
        }

        var novo = Instantiate(modeloCarregador, fuzil.magazinePoint.transform.position, fuzil.magazinePoint.transform.rotation);
        novo.name = "Carregador (modo desktop)";
        novo.gameObject.SetActive(true);
        novo.SetAmmo(capacidadeCarregador);
        fuzil.magazinePoint.Place(novo.GetComponent<Grabbable>());
        if (!fuzil.IsSlideLoaded()) fuzil.LoadSlide();
    }

    void AlternarCameraHelicoptero()
    {
        if (cameraHelicoptero == null) return;
        bool usarHelicoptero = cabeca.enabled;
        cameraHelicoptero.gameObject.SetActive(true);
        AtivarCamera(cabeca, !usarHelicoptero);
        AtivarCamera(cameraHelicoptero, usarHelicoptero);
        OuvirPor(usarHelicoptero ? cameraHelicoptero : cabeca);
    }

    static void AtivarCamera(Camera camera, bool ativa)
    {
        camera.enabled = ativa;
        var ouvinte = camera.GetComponent<AudioListener>();
        if (ouvinte != null) ouvinte.enabled = ativa;
    }

    // A cena tem mais de um AudioListener; deixa ativo só o da câmera em uso
    static void OuvirPor(Camera camera)
    {
        var ouvinte = camera.GetComponent<AudioListener>();
        if (ouvinte == null) ouvinte = camera.gameObject.AddComponent<AudioListener>();
        foreach (var outro in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            outro.enabled = outro == ouvinte;
    }

    static void TravarCursor(bool travar)
    {
        Cursor.lockState = travar ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !travar;
    }

    static bool TemTrackedPoseDriver(Camera camera) =>
        camera.GetComponents<Behaviour>().Any(c => c != null && c.GetType().Name == "TrackedPoseDriver");

    void OnGUI()
    {
        if (modoTiro && cabeca.enabled)
        {
            var centro = new Vector2(Screen.width / 2f, Screen.height / 2f);
            GUI.DrawTexture(new Rect(centro.x - 10, centro.y - 1, 20, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centro.x - 1, centro.y - 10, 2, 20), Texture2D.whiteTexture);
        }

        string ajuda = modoTiro
            ? $"MODO DESKTOP (sem óculos)   Munição: {fuzil.GetAmmo()}\n" +
              "Mouse: mirar | Clique esq.: atirar | Botão dir.: zoom | T: recarregar | H: câmera do helicóptero | Esc: liberar cursor"
            : "MODO DESKTOP (sem óculos)\nSegure o botão direito e mova o mouse para olhar | Clique nos botões do menu";
        GUI.Label(new Rect(10, 10, Screen.width - 20, 40), ajuda);
    }
}
