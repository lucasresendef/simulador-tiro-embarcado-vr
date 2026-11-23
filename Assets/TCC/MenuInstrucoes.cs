using UnityEngine;
using UnityEngine.UI;

// Navegação extra do menu inicial: página de instruções (botão "Instruções") e setas de voltar
// nas páginas secundárias, que retornam ao menu principal. Montada pela ferramenta
// TCC → Redesenhar menu inicial.
public class MenuInstrucoes : MonoBehaviour
{
    public GameStartMenu menu;
    public GameObject pagina;
    public Button abrir;
    public Button voltar;
    public Button[] setasVoltar;

    void Start()
    {
        if (pagina != null) pagina.SetActive(false);
        if (abrir != null) abrir.onClick.AddListener(Abrir);
        if (voltar != null) voltar.onClick.AddListener(VoltarAoMenu);
        if (setasVoltar != null)
            foreach (var seta in setasVoltar)
                if (seta != null) seta.onClick.AddListener(VoltarAoMenu);
    }

    void Abrir()
    {
        menu.HideAll();
        pagina.SetActive(true);
    }

    // O EnableMainMenu do menu original não esconde a seleção de cenários; esconde tudo antes
    void VoltarAoMenu()
    {
        menu.HideAll();
        if (pagina != null) pagina.SetActive(false);
        menu.EnableMainMenu();
    }
}
