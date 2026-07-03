using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu. Holds the menu root plus a list pairing each minigame with the button that
/// launches it, so wiring stays in one place. A home button stops the current minigame
/// (without scoring) and returns to the menu.
/// </summary>
public class Main_Menu : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Minigame_Base minigame;
        public Button button;
    }

    [Title("Main Menu")]
    [Tooltip("Root object of the main menu, shown on the menu and hidden while a minigame runs.")]
    [SerializeField] private GameObject mainMenuRoot;

    [Tooltip("Each minigame paired with the button that starts it.")]
    [SerializeField] private List<Entry> entries = new();

    [Tooltip("Stops the current minigame without scoring and returns to the menu.")]
    [SerializeField] private Button homeButton;

    private void Awake()
    {
        foreach (Entry entry in entries)
        {
            if (entry == null || entry.button == null)
                continue;

            Entry captured = entry; // safe closure capture
            entry.button.onClick.AddListener(() => StartGame(captured));
        }

        if (homeButton != null)
            homeButton.onClick.AddListener(GoHome);
    }

    private void Start()
    {
        ShowMenu();
    }

    private void StartGame(Entry entry)
    {
        if (entry.minigame == null || Minigame_Manager.Instance == null)
            return;

        HideMenu();
        Minigame_Manager.Instance.StartMinigame(entry.minigame);
    }

    /// <summary>Aborts any running minigame (no score) and shows the menu.</summary>
    public void GoHome()
    {
        if (Minigame_Manager.Instance != null)
            Minigame_Manager.Instance.AbortActive();

        ShowMenu();
    }

    public void ShowMenu()
    {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(true);
    }

    public void HideMenu()
    {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);
    }
}
