using UnityEngine;

public class MenuUI: MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    
    [SerializeField] private GameObject[] pages;

    public void ToggleMenu()
    {
        menuPanel.SetActive(!menuPanel.activeSelf);
    }
    
    public void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
        }

    }
}
