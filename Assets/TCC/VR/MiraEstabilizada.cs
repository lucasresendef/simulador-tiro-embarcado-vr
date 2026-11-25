using Autohand;
using UnityEngine;

// Mira estabilizada do fuzil no VR, com as duas mãos.
// No AutoHand, cada mão puxa a arma fisicamente para o seu controle; com as duas mãos na arma o cano
// parava num equilíbrio entre os dois puxões, 20–30° fora da direção pretendida (medido com
// TesteMiraVR: erro médio de 22° e nenhum acerto no suspeito a 78 m).
// Aqui, enquanto a arma está na mão principal, a arma e as mãos são posicionadas diretamente:
//  - a mão principal (direita) define a posição e a rotação da arma, como no controle;
//  - a mão de apoio (esquerda) define a direção do cano: a linha entre as duas mãos, como num fuzil
//    real. Se o controle esquerdo estiver longe da posição de apoio, a arma segue só a mão direita;
//  - a mão de apoio é desenhada presa ao guarda-mão.
// As mãos e a arma ficam cinemáticas enquanto isso; ao soltar, voltam à física do AutoHand.
// Adicionado pelo AdvancedInitialGrab (campo "miraEstabilizada").
[DefaultExecutionOrder(50)]
public class MiraEstabilizada : MonoBehaviour
{
    public Hand maoPrincipal, maoApoio;
    public Grabbable pegadaPrincipal, pegadaApoio;

    // Faixa de distância entre os controles e desvio máximo da linha das mãos para usar o apoio
    const float DistanciaMinimaApoio = 0.12f;
    const float DistanciaMaximaApoio = 0.9f;
    const float AnguloMaximoApoio = 45f;

    bool ativo, usaApoio;
    Rigidbody arma, corpoPrincipal, corpoApoio;
    Pose armaNaMaoPrincipal, maoApoioNaArma;
    Vector3 linhaNaArma;   // direção mão principal → mão de apoio, no espaço da arma

    float esperarApoioAte;
    public float DiagDistancia, DiagAngulo;   // TEMPORÁRIO: diagnóstico do apoio
    const float EsperaApoio = 1.5f;

    // Desliga por um instante (física normal), para uma mão poder pegar a arma
    public void Pausar()
    {
        Desligar();
        esperarApoioAte = Time.time + EsperaApoio;
    }

    void Start() => esperarApoioAte = Time.time + EsperaApoio + 1f;

    public void Configurar(Hand principal, Grabbable pegada, Hand apoio, Grabbable pegadaDeApoio)
    {
        maoPrincipal = principal;
        pegadaPrincipal = pegada;
        maoApoio = apoio;
        pegadaApoio = pegadaDeApoio;
    }

    void FixedUpdate()
    {
        bool segurando = maoPrincipal != null && pegadaPrincipal != null && maoPrincipal.holdingObj == pegadaPrincipal;
        bool apoio = segurando && maoApoio != null && pegadaApoio != null && maoApoio.holdingObj == pegadaApoio;
        // O AutoHand não pega objeto cinemático: enquanto alguma mão está pegando, a física volta ao normal
        bool pegando = (maoPrincipal != null && maoPrincipal.IsGrabbing()) || (maoApoio != null && maoApoio.IsGrabbing());
        if (!segurando || pegando)
        {
            Desligar();
            return;
        }
        // Logo depois da pegada da mão principal, espera a mão de apoio pegar o guarda-mão
        if (!ativo && !apoio && maoApoio != null && pegadaApoio != null && Time.time < esperarApoioAte) return;
        if (!ativo || apoio != usaApoio) Ligar(apoio);
        Aplicar();
    }

    // O helicóptero é interpolado: os controles (filhos dele) só chegam à posição do quadro no Update
    // e no rastreamento logo antes de desenhar. Posicionar a arma só no passo de física a deixava
    // dezenas de centímetros atrás das mãos com o helicóptero a 27 m/s. Por isso a pose é refeita
    // também no fim do quadro e imediatamente antes de desenhar.
    void OnEnable() => Application.onBeforeRender += AntesDeDesenhar;

    void OnDisable()
    {
        Application.onBeforeRender -= AntesDeDesenhar;
        Desligar();
    }

    void LateUpdate()
    {
        if (BotaoAApertado()) Realinhar();
        if (ativo) Aplicar(true);
    }

    bool botaoAAnterior;

    // Botão A do controle direito (Quest e PICO), só na borda de descida
    bool BotaoAApertado()
    {
        var controle = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        bool apertado = controle.isValid && controle.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool a) && a;
        bool borda = apertado && !botaoAAnterior;
        botaoAAnterior = apertado;
        return borda;
    }

    // Botão A: o cano passa a apontar exatamente para onde o controle direito aponta agora.
    // A relação entre as mãos e o cano é guardada assim, e a mira com as duas mãos continua a partir dela.
    public void Realinhar()
    {
        if (!ativo) return;
        var fuzil = arma.GetComponentInChildren<AutoGun>();
        if (fuzil == null || fuzil.shootForward == null || maoPrincipal.follow == null) return;

        // Parte da pose calculada pela própria mira (a mesma que Aplicar usa), e não da pose do corpo
        // rígido: esta fica atrasada em relação ao controle, e cada A acumulava o atraso (a arma subia)
        if (!CalcularPoseArma(out var alvoMao, out var pontoPegada, out var rotArma, out var posArma)) return;

        // Direção do cano no espaço da arma (fixa), aplicada à pose calculada
        var canoNaArma = Quaternion.Inverse(arma.transform.rotation) * fuzil.shootForward.forward;
        var cano = rotArma * canoNaArma;

        // Gira a arma em torno da mão até o cano coincidir com a direção do controle
        var correcao = Quaternion.FromToRotation(cano, maoPrincipal.follow.forward);
        rotArma = correcao * rotArma;
        posArma = pontoPegada + correcao * (posArma - pontoPegada);

        var inv = Quaternion.Inverse(alvoMao.rotation);
        armaNaMaoPrincipal = new Pose(inv * (posArma - pontoPegada), inv * rotArma);
        if (usaApoio)
            linhaNaArma = (Quaternion.Inverse(rotArma) * (AlvoDe(maoApoio).position - pontoPegada)).normalized;
        Debug.Log($"[TCC] Mira realinhada com o controle direito (correção de {Quaternion.Angle(Quaternion.identity, correcao):0.0}°).");
        maoPrincipal.PlayHapticVibration(0.1f, 0.5f);
    }

    void AntesDeDesenhar()
    {
        if (ativo && this != null) Aplicar(true);
    }

    void Ligar(bool comApoio)
    {
        Desligar();
        arma = pegadaPrincipal.body;
        corpoPrincipal = maoPrincipal.body;
        if (arma == null || corpoPrincipal == null) return;

        // Guarda as posições relativas do momento da pegada (mãos nos pontos de pegada da arma)
        armaNaMaoPrincipal = Relativa(PoseDe(corpoPrincipal), PoseDe(arma));
        usaApoio = comApoio && maoApoio.body != null;
        if (usaApoio)
        {
            corpoApoio = maoApoio.body;
            maoApoioNaArma = Relativa(PoseDe(arma), PoseDe(corpoApoio));
            linhaNaArma = (Quaternion.Inverse(arma.rotation) * (corpoApoio.position - corpoPrincipal.position)).normalized;
            corpoApoio.isKinematic = true;
        }
        corpoPrincipal.isKinematic = true;
        arma.isKinematic = true;
        ativo = true;
        Debug.Log($"[TCC] MiraEstabilizada ligada (apoio: {usaApoio}).");
    }

    void Desligar()
    {
        if (!ativo) return;
        Debug.Log("[TCC] MiraEstabilizada desligada.");
        if (arma != null) arma.isKinematic = false;
        if (corpoPrincipal != null) corpoPrincipal.isKinematic = false;
        if (corpoApoio != null) corpoApoio.isKinematic = false;
        arma = corpoPrincipal = corpoApoio = null;
        ativo = usaApoio = false;
    }

    // Quando a arma é entregue à mão (ForceGrab), o AutoHand leva a mão até a arma e guarda a diferença
    // entre a mão e o controle como deslocamento da pegada; com a arma vinda de longe isso deixava a mão
    // 30–70 cm fora do controle e a arma girada até 76°. Zerado, a mão fica no controle.
    static void ZerarDeslocamentoPegada(Hand mao)
    {
        if (mao == null) return;
        mao.grabPositionOffset = Vector3.zero;
        mao.grabRotationOffset = Quaternion.identity;
    }

    // imediato: fora do passo de física, move direto (transform e corpo) em vez de MovePosition
    void Aplicar(bool imediato = false)
    {
        if (!CalcularPoseArma(out _, out _, out var rotArma, out var posArma)) return;

        var poseArma = new Pose(posArma, rotArma);
        var poseMao = Composta(poseArma, Inversa(armaNaMaoPrincipal));
        // Com parentOnGrab a arma é filha da mão principal e já acompanha a mão; movê-la também a deslocaria duas vezes
        Mover(corpoPrincipal, poseMao, imediato);
        if (!arma.transform.IsChildOf(corpoPrincipal.transform))
            Mover(arma, poseArma, imediato);
        if (usaApoio && corpoApoio != null)
            Mover(corpoApoio, Composta(poseArma, maoApoioNaArma), imediato);
    }

    // Pose que a arma deve ter agora, a partir dos controles: posição e rotação da mão principal,
    // e direção do cano pela linha entre as duas mãos (se o apoio estiver em uso)
    bool CalcularPoseArma(out Pose alvoMao, out Vector3 pontoPegada, out Quaternion rotArma, out Vector3 posArma)
    {
        ZerarDeslocamentoPegada(maoPrincipal);
        if (usaApoio) ZerarDeslocamentoPegada(maoApoio);

        // Onde a mão principal deveria estar (controle + deslocamentos da pegada)
        if (maoPrincipal.follow == null || (usaApoio && maoApoio.follow == null))
        {
            alvoMao = default;
            pontoPegada = posArma = Vector3.zero;
            rotArma = Quaternion.identity;
            return false;
        }
        alvoMao = AlvoDe(maoPrincipal);

        rotArma = alvoMao.rotation * armaNaMaoPrincipal.rotation;
        posArma = alvoMao.position + alvoMao.rotation * armaNaMaoPrincipal.position;
        pontoPegada = alvoMao.position;

        if (usaApoio)
        {
            {
                var linhaDesejada = AlvoDe(maoApoio).position - pontoPegada;
                float distancia = linhaDesejada.magnitude;
                var linhaAtual = rotArma * linhaNaArma;
                DiagDistancia = distancia; DiagAngulo = Vector3.Angle(linhaAtual, linhaDesejada);
                if (distancia > DistanciaMinimaApoio && distancia < DistanciaMaximaApoio &&
                    Vector3.Angle(linhaAtual, linhaDesejada) < AnguloMaximoApoio)
                {
                    // Gira a arma em torno da mão principal até a linha das mãos coincidir com a dos controles
                    var correcao = Quaternion.FromToRotation(linhaAtual, linhaDesejada / distancia);
                    rotArma = correcao * rotArma;
                    posArma = pontoPegada + correcao * (posArma - pontoPegada);
                }
            }
        }
        return true;
    }

    // Alvo da mão: o controle mais os deslocamentos da peça segurada, como no HandBase.SetMoveTo.
    // Lá o deslocamento de posição é girado pela rotação da própria mão, e não pela do controle; como
    // aqui a mão é posicionada pela mira, o alvo passava a depender do quadro anterior e, depois de um
    // movimento grande, ficava num equilíbrio errado (3,5° de erro permanente, medido com TesteMiraVR).
    // Aqui tudo sai só do controle, e sem o ajuste do AutoHand para duas mãos (a disputa evitada).
    static Pose AlvoDe(Hand mao)
    {
        var pos = mao.follow.position + mao.grabPositionOffset;
        var rot = mao.follow.rotation * mao.grabRotationOffset;
        var segurado = mao.holdingObj;
        if (segurado != null)
        {
            var rotOffset = segurado.heldRotationOffset;
            var posOffset = segurado.heldPositionOffset;
            if (mao.left)
            {
                rotOffset = -rotOffset;
                rotOffset.x *= -1;
                posOffset.x *= -1;
            }
            pos += rot * posOffset;
            rot *= Quaternion.Euler(rotOffset);
        }
        return new Pose(pos, rot);
    }

    static void Mover(Rigidbody corpo, Pose pose, bool imediato)
    {
        if (imediato)
        {
            corpo.transform.SetPositionAndRotation(pose.position, pose.rotation);
            corpo.position = pose.position;
            corpo.rotation = pose.rotation;
        }
        else
        {
            corpo.MovePosition(pose.position);
            corpo.MoveRotation(pose.rotation);
        }
    }

    static Pose PoseDe(Rigidbody corpo) => new Pose(corpo.position, corpo.rotation);

    // Pose de b no espaço de a
    static Pose Relativa(Pose a, Pose b)
    {
        var inv = Quaternion.Inverse(a.rotation);
        return new Pose(inv * (b.position - a.position), inv * b.rotation);
    }

    // Pose local "filha" no espaço de "pai", em coordenadas do mundo
    static Pose Composta(Pose pai, Pose filha) =>
        new Pose(pai.position + pai.rotation * filha.position, pai.rotation * filha.rotation);

    static Pose Inversa(Pose p)
    {
        var inv = Quaternion.Inverse(p.rotation);
        return new Pose(inv * -p.position, inv);
    }
}
