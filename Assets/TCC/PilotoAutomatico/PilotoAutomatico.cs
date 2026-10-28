using Oyedoyin.Rotary.LowFidelity;
using UnityEngine;
using UnityEngine.InputSystem;

// Piloto automático para o treinamento: voa os padrões do protocolo do COMAVE
// (órbita, deslocamento linear e pairado) em torno da área de treinamento, ou acompanha
// um veículo em fuga lado a lado (perseguição), sempre com a
// porta do operador voltada para o alvo. É opcional: P liga/desliga e O troca o padrão;
// desligado, o helicóptero volta para a física do Silantro mantendo a velocidade atual.
// Enquanto ligado, o voo é cinemático, o que torna as passagens repetíveis entre sessões.
[RequireComponent(typeof(Controller))]
public class PilotoAutomatico : MonoBehaviour
{
    public enum Padrao { Orbita, Linear, Pairado, Acompanhamento }
    public enum LadoPorta { Esquerda, Direita }

    [Tooltip("Centro da área de treinamento (nível do solo)")]
    public Transform centro;
    public Padrao padrao = Padrao.Orbita;
    [Tooltip("Lado da aeronave em que o operador atira")]
    public LadoPorta porta = LadoPorta.Esquerda;
    [Tooltip("Altura sobre o centro, em pés (o protocolo usa 300 ft)")]
    public float alturaPes = 300f;
    [Tooltip("Raio da órbita e distância lateral das passagens, em metros")]
    public float raio = 60f;
    public float velocidadeOrbitaNos = 20f;
    public float velocidadeLinearNos = 40f;
    [Tooltip("Comprimento das retas no padrão linear, em metros")]
    public float comprimentoReta = 160f;
    [Tooltip("Direção das retas do padrão linear (graus a partir do norte)")]
    public float rumoReta = 90f;
    [Tooltip("Veículo acompanhado no padrão de acompanhamento (perseguição)")]
    public Transform alvoAcompanhado;
    [Range(0f, 2f)] public float vibracao = 1f;
    public bool ligarAoIniciar;

    const float Nos = 0.5144f;
    const float AceleracaoMaxima = 3f, VelocidadeMaxima = 30f, SubidaMaxima = 5f, GiroMaximo = 25f;
    // No acompanhamento a aeronave precisa alcançar e seguir um carro em alta velocidade
    const float AceleracaoAcompanhamento = 6f, VelocidadeMaximaAcompanhamento = 50f;
    const float FolgaTelhados = 20f;

    public bool Ligado { get; private set; }
    public Vector3 Velocidade => velocidade;

    Controller controle;
    Rigidbody corpo;
    Vector3 portaLocal, velocidade, aceleracaoSuave, posicaoNominal, velocidadeAlvo, ultimaPosicaoAlvo;
    float fase, sentido, alturaEfetiva, ultimoYaw;
    RigidbodyInterpolation interpolacaoOriginal;
    string aviso;

    void Awake()
    {
        controle = GetComponent<Controller>();
        corpo = GetComponent<Rigidbody>();
    }

    void Start()
    {
        portaLocal = porta == LadoPorta.Esquerda ? Vector3.left : Vector3.right;
        // Porta à esquerda: a aeronave circula com o alvo à esquerda (sentido anti-horário)
        sentido = porta == LadoPorta.Esquerda ? 1f : -1f;

        if (ligarAoIniciar || ConfiguracaoSessao.PilotoAutomaticoAoIniciar) Ligar();
    }

    void Update()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado.pKey.wasPressedThisFrame)
        {
            if (Ligado) Desligar();
            else Ligar();
        }
        if (teclado.oKey.wasPressedThisFrame)
        {
            int total = alvoAcompanhado != null ? 4 : 3;
            padrao = (Padrao)(((int)padrao + 1) % total);
            if (Ligado) IniciarPadrao();
        }
    }

    public void Ligar()
    {
        if (padrao == Padrao.Acompanhamento && alvoAcompanhado == null) padrao = Padrao.Orbita;
        if (centro == null && padrao != Padrao.Acompanhamento)
        {
            aviso = "sem centro da área de treinamento definido";
            return;
        }
        if (controle.CurrentEngineState == Controller.EngineState.Off) controle.StartEngine();

        Ligado = true;
        velocidade = corpo != null ? corpo.linearVelocity : Vector3.zero;
        posicaoNominal = corpo != null ? corpo.position : transform.position;
        ultimoYaw = transform.eulerAngles.y;
        if (corpo != null)
        {
            interpolacaoOriginal = corpo.interpolation;
            corpo.isKinematic = true;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
        }
        IniciarPadrao();
    }

    public void Desligar()
    {
        Ligado = false;
        if (corpo != null)
        {
            corpo.isKinematic = false;
            corpo.interpolation = interpolacaoOriginal;
            corpo.linearVelocity = velocidade;
            corpo.angularVelocity = Vector3.zero;
        }
        // Coletivo neutro: o Silantro mantém a aeronave pairando
        controle.m_collective_power = 1f;
    }

    void IniciarPadrao()
    {
        if (padrao == Padrao.Acompanhamento)
        {
            alturaEfetiva = Altura;
            aviso = null;
            if (alvoAcompanhado != null) ultimaPosicaoAlvo = alvoAcompanhado.position;
            return;
        }
        fase = FaseMaisProxima(transform.position);
        alturaEfetiva = AlturaSegura();
        aviso = alturaEfetiva > Altura + 0.5f ? $"altura elevada para {alturaEfetiva / 0.3048f:F0} ft por causa dos prédios" : null;
    }

    float Altura => alturaPes * 0.3048f;

    void FixedUpdate()
    {
        if (!Ligado) return;
        // Espera o motor girar antes de decolar
        if (controle.CurrentEngineState != Controller.EngineState.Active) return;

        float dt = Time.fixedDeltaTime;
        // A vibração é só visual: a trajetória é calculada sobre a posição nominal
        var posicao = posicaoNominal;

        var velocidadeDesejada = VelocidadeDesejada(posicao);
        // Decola na vertical: só se desloca na horizontal depois de ganhar altura
        float alturaDesejada = padrao == Padrao.Acompanhamento ? PontoAcompanhamento().y : PontoDoPadrao(fase).y;
        if (AlturaSobreSolo(posicao) < 30f && alturaDesejada - posicao.y > 10f)
            velocidadeDesejada = new Vector3(0f, velocidadeDesejada.y, 0f);

        float aceleracaoMaxima = padrao == Padrao.Acompanhamento ? AceleracaoAcompanhamento : AceleracaoMaxima;
        var novaVelocidade = Vector3.MoveTowards(velocidade, velocidadeDesejada, aceleracaoMaxima * dt);
        aceleracaoSuave = Vector3.Lerp(aceleracaoSuave, (novaVelocidade - velocidade) / dt, 0.05f);
        velocidade = novaVelocidade;
        posicao += velocidade * dt;
        posicaoNominal = posicao;

        var rotacao = Orientacao(posicao, dt);
        posicao += Vibracao();
        if (corpo != null)
        {
            corpo.MovePosition(posicao);
            corpo.MoveRotation(rotacao);
        }
        else transform.SetPositionAndRotation(posicao, rotacao);
    }

    // Perseguição de um ponto à frente na trajetória (pure pursuit): a fase acompanha o ponto
    // da trajetória mais próximo da aeronave e o rumo aponta para um ponto adiante dele.
    Vector3 VelocidadeDesejada(Vector3 posicao)
    {
        if (padrao == Padrao.Acompanhamento) return VelocidadeAcompanhamento(posicao);

        if (padrao == Padrao.Pairado)
        {
            // Perfil de frenagem: chega ao ponto sem ultrapassá-lo
            var paraPonto = PontoDoPadrao(0f) - posicao;
            var horizontalPonto = Horizontal(paraPonto);
            float distancia = horizontalPonto.magnitude;
            float rapidezPonto = Mathf.Min(VelocidadeMaxima, Mathf.Sqrt(2f * 0.7f * AceleracaoMaxima * distancia), 0.8f * distancia);
            return (distancia > 0.01f ? horizontalPonto / distancia * rapidezPonto : Vector3.zero)
                + Vector3.up * Mathf.Clamp(paraPonto.y * 0.4f, -SubidaMaxima, SubidaMaxima);
        }

        fase = FaseProxima(posicao, fase);
        var naTrajetoria = PontoDoPadrao(fase);
        var adiante = PontoDoPadrao(fase + Antecipacao);
        float afastamento = Vector3.Distance(Horizontal(naTrajetoria), Horizontal(posicao));
        // Longe da trajetória (aproximação), voa mais rápido até ela
        float rapidez = Mathf.Min(VelocidadeMaxima, VelocidadeNoPadrao() + Mathf.Max(0f, afastamento - 10f) * 0.3f);
        var horizontal = Horizontal(adiante - posicao);
        var vertical = Mathf.Clamp((naTrajetoria.y - posicao.y) * 0.4f, -SubidaMaxima, SubidaMaxima);
        return (horizontal.sqrMagnitude > 0.01f ? horizontal.normalized * rapidez : Vector3.zero) + Vector3.up * vertical;
    }

    const float Antecipacao = 15f;

    // Lado a lado com o veículo, a um raio de distância, com a porta voltada para ele
    Vector3 PontoAcompanhamento()
    {
        var frente = Horizontal(velocidadeAlvo).sqrMagnitude > 1f ? Horizontal(velocidadeAlvo).normalized : Horizontal(alvoAcompanhado.forward).normalized;
        var lado = Vector3.Cross(Vector3.up, frente) * (porta == LadoPorta.Esquerda ? 1f : -1f);
        return alvoAcompanhado.position + lado * raio + Vector3.up * Altura;
    }

    Vector3 VelocidadeAcompanhamento(Vector3 posicao)
    {
        if (alvoAcompanhado == null) return Vector3.zero;
        float dt = Time.fixedDeltaTime;
        velocidadeAlvo = Vector3.Lerp(velocidadeAlvo, (alvoAcompanhado.position - ultimaPosicaoAlvo) / dt, 0.1f);
        ultimaPosicaoAlvo = alvoAcompanhado.position;

        var erro = PontoAcompanhamento() - posicao;
        var horizontal = Vector3.ClampMagnitude(Horizontal(velocidadeAlvo) + Horizontal(erro) * 0.5f, VelocidadeMaximaAcompanhamento);
        return horizontal + Vector3.up * Mathf.Clamp(erro.y * 0.4f, -SubidaMaxima, SubidaMaxima);
    }

    // Busca local, em torno da fase atual, do ponto da trajetória mais próximo
    float FaseProxima(Vector3 posicao, float faseAtual)
    {
        float melhor = faseAtual, menorDistancia = float.MaxValue;
        for (float s = faseAtual - 10f; s <= faseAtual + 40f; s += 1f)
        {
            float d = (Horizontal(PontoDoPadrao(s)) - Horizontal(posicao)).sqrMagnitude;
            if (d < menorDistancia) { menorDistancia = d; melhor = s; }
        }
        return melhor;
    }

    // Porta voltada para o ponto de referência, com inclinação na direção da aceleração
    Quaternion Orientacao(Vector3 posicao, float dt)
    {
        var paraAlvo = Horizontal(PontoDeReferencia(posicao) - posicao);
        float yawDesejado = Mathf.Atan2(paraAlvo.x, paraAlvo.z) * Mathf.Rad2Deg - Mathf.Atan2(portaLocal.x, portaLocal.z) * Mathf.Rad2Deg;
        if (paraAlvo.sqrMagnitude < 1f) yawDesejado = ultimoYaw;
        ultimoYaw = Mathf.MoveTowardsAngle(ultimoYaw, yawDesejado, GiroMaximo * dt);

        var cima = (Vector3.up * 9.81f + Horizontal(aceleracaoSuave)).normalized;
        cima = Vector3.RotateTowards(Vector3.up, cima, 20f * Mathf.Deg2Rad, 0f);
        var frente = Quaternion.Euler(0f, ultimoYaw, 0f) * Vector3.forward;
        return Quaternion.LookRotation(Vector3.ProjectOnPlane(frente, cima), cima);
    }

    Vector3 Vibracao()
    {
        if (vibracao <= 0f) return Vector3.zero;
        float t = Time.time * 4f;
        return new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f) * 0.05f * vibracao;
    }

    // ---- Geometria dos padrões. A fase é a distância percorrida (m) ou, no pairado, ignorada.

    float VelocidadeNoPadrao() => padrao switch
    {
        Padrao.Orbita => velocidadeOrbitaNos * Nos,
        // Nas curvas do circuito, limita a aceleração lateral a cerca de 3 m/s²
        Padrao.Linear => EmCurva(fase) ? Mathf.Min(velocidadeLinearNos * Nos, Mathf.Sqrt(3f * raio)) : velocidadeLinearNos * Nos,
        _ => 0f
    };

    Vector3 PontoDoPadrao(float s)
    {
        var c = centro.position + Vector3.up * alturaEfetiva;
        switch (padrao)
        {
            case Padrao.Orbita:
            {
                float angulo = sentido * s / raio;
                return c + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * raio;
            }
            case Padrao.Linear:
            {
                var p = PontoNoCircuito(s);
                return c + EixoReta() * p.x + LateralReta() * p.y;
            }
            default:
                return Horizontal(pontoPairado) + Vector3.up * c.y;
        }
    }

    Vector3 DirecaoDoPadrao(float s)
    {
        if (padrao == Padrao.Pairado) return Vector3.zero;
        var d = PontoDoPadrao(s + 1f) - PontoDoPadrao(s);
        return d.sqrMagnitude > 0f ? d.normalized : Vector3.zero;
    }

    Vector3 PontoDeReferencia(Vector3 posicao)
    {
        if (padrao == Padrao.Acompanhamento) return alvoAcompanhado != null ? alvoAcompanhado.position : posicao;
        if (padrao != Padrao.Linear) return centro.position;
        // No linear, a porta aponta para o eixo do circuito (perpendicular às retas)
        var local = Vector3.Dot(posicao - centro.position, EixoReta());
        return centro.position + EixoReta() * Mathf.Clamp(local, -comprimentoReta / 2f, comprimentoReta / 2f);
    }

    Vector3 EixoReta() => Quaternion.Euler(0f, rumoReta, 0f) * Vector3.forward;
    Vector3 LateralReta() => Vector3.Cross(Vector3.up, EixoReta());

    float PerimetroCircuito => 2f * comprimentoReta + 2f * Mathf.PI * raio;

    bool EmCurva(float s)
    {
        s = Mathf.Repeat(s, PerimetroCircuito);
        float meiaVolta = Mathf.PI * raio;
        return (s > comprimentoReta && s < comprimentoReta + meiaVolta) || s > 2f * comprimentoReta + meiaVolta;
    }

    // Circuito oval (duas retas e duas meias-voltas) no plano (eixo, lateral), percorrido no sentido da porta
    Vector2 PontoNoCircuito(float s)
    {
        float L = comprimentoReta, meia = L / 2f, meiaVolta = Mathf.PI * raio;
        s = Mathf.Repeat(s, PerimetroCircuito);
        Vector2 p;
        if (s < L) p = new Vector2(-meia + s, -raio);
        else if (s < L + meiaVolta) { float a = -Mathf.PI / 2f + (s - L) / raio; p = new Vector2(meia + Mathf.Cos(a) * raio, Mathf.Sin(a) * raio); }
        else if (s < 2f * L + meiaVolta) p = new Vector2(meia - (s - L - meiaVolta), raio);
        else { float a = Mathf.PI / 2f + (s - 2f * L - meiaVolta) / raio; p = new Vector2(-meia + Mathf.Cos(a) * raio, Mathf.Sin(a) * raio); }
        // O oval acima é horário visto de cima; para porta à esquerda, espelha
        if (sentido > 0f) p.y = -p.y;
        return p;
    }

    Vector3 pontoPairado;

    float FaseMaisProxima(Vector3 posicao)
    {
        if (padrao == Padrao.Pairado)
        {
            // Paira onde a frenagem terminar, se for perto da área; senão, a um raio do centro
            var horizontal = Horizontal(velocidade);
            var parada = posicao + horizontal * (horizontal.magnitude / (2f * 0.7f * AceleracaoMaxima));
            var radial = Horizontal(parada - centro.position);
            pontoPairado = radial.magnitude < raio * 3f ? parada : centro.position + radial.normalized * raio;
            return 0f;
        }

        float melhor = 0f, menorDistancia = float.MaxValue;
        float total = padrao == Padrao.Orbita ? 2f * Mathf.PI * raio : PerimetroCircuito;
        for (float s = 0f; s < total; s += 2f)
        {
            float d = Vector3.Distance(Horizontal(PontoDoPadrao(s)), Horizontal(posicao));
            if (d < menorDistancia) { menorDistancia = d; melhor = s; }
        }
        return melhor;
    }

    // Garante folga sobre os telhados ao longo de todo o padrão
    float AlturaSegura()
    {
        alturaEfetiva = Altura;
        float maiorTelhado = float.MinValue;
        float total = padrao switch { Padrao.Orbita => 2f * Mathf.PI * raio, Padrao.Linear => PerimetroCircuito, _ => 1f };
        for (float s = 0f; s < total; s += 5f)
        {
            var p = PontoDoPadrao(s);
            maiorTelhado = Mathf.Max(maiorTelhado, AlturaDoSolo(p));
        }
        return Mathf.Max(Altura, maiorTelhado - centro.position.y + FolgaTelhados);
    }

    float AlturaDoSolo(Vector3 ponto)
    {
        foreach (var hit in Physics.RaycastAll(new Vector3(ponto.x, 1000f, ponto.z), Vector3.down, 2000f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform)) return hit.point.y;
        return centro.position.y;
    }

    float AlturaSobreSolo(Vector3 posicao)
    {
        float maisAlto = float.MinValue;
        foreach (var hit in Physics.RaycastAll(posicao + Vector3.up, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform)) maisAlto = Mathf.Max(maisAlto, hit.point.y);
        return maisAlto == float.MinValue ? 500f : posicao.y - maisAlto;
    }

    static Vector3 Horizontal(Vector3 v) => new Vector3(v.x, 0f, v.z);

    void OnGUI()
    {
        string nomePadrao = padrao switch { Padrao.Orbita => "órbita", Padrao.Linear => "deslocamento linear", Padrao.Acompanhamento => "acompanhamento", _ => "pairado" };
        string texto = Ligado
            ? $"PILOTO AUTOMÁTICO: {nomePadrao.ToUpper()} ({alturaEfetiva / 0.3048f:F0} ft, {(padrao == Padrao.Acompanhamento ? "afastamento" : "raio")} {raio:F0} m, {velocidade.magnitude / Nos:F0} kt) | P: desligar | O: trocar padrão"
            : $"Piloto automático desligado ({nomePadrao}) | P: ligar | O: trocar padrão";
        if (Ligado && controle.CurrentEngineState != Controller.EngineState.Active) texto += " | aguardando o motor";
        if (!string.IsNullOrEmpty(aviso)) texto += " | " + aviso;
        GUI.Label(new Rect(10, 55, Screen.width - 20, 22), texto);
    }
}
