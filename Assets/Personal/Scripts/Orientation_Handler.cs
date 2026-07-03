using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

public enum GameOrientation { Portrait, Landscape }

/// <summary>
/// Forces the screen orientation per game scenario (main menu / each minigame). Runs on
/// mobile by default; can be forced on desktop for debugging. Also offers an in-editor
/// preview that switches the Game view to a portrait/landscape resolution so the UI can be
/// laid out for each orientation.
/// </summary>
public class Orientation_Handler : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Minigame_Base minigame;
        public GameOrientation orientation = GameOrientation.Portrait;
    }

    [Title("Setup")]
    [HelpBox("Sets screen orientation per scenario. Active on mobile, or on desktop if forced below.", MessageMode.Log)]
    [Tooltip("Also manage orientation on desktop / in the editor (for debugging).")]
    [SerializeField] private bool forceOnDesktop;

    [Title("Orientations")]
    [Tooltip("Orientation used for the main menu and any neutral state.")]
    [SerializeField] private GameOrientation menuOrientation = GameOrientation.Portrait;

    [Tooltip("Orientation to apply when each minigame starts.")]
    [SerializeField] private List<Entry> minigames = new();

    [Title("Editor Preview")]
    [Tooltip("Switch the Game view to the orientation below so you can adjust the UI for it.")]
    [OnValueChanged(nameof(ApplyPreview))]
    [SerializeField] private bool previewAspectRatio;

    [ShowField(nameof(previewAspectRatio))]
    [OnValueChanged(nameof(ApplyPreview))]
    [SerializeField] private GameOrientation previewOrientation = GameOrientation.Portrait;

    [ShowField(nameof(previewAspectRatio))]
    [Tooltip("Resolution used to preview portrait (match your Game view preset).")]
    [SerializeField] private Vector2Int portraitResolution = new(1290, 2796);

    [ShowField(nameof(previewAspectRatio))]
    [Tooltip("Resolution used to preview landscape (match your Game view preset).")]
    [SerializeField] private Vector2Int landscapeResolution = new(2796, 1290);

    private readonly Dictionary<Minigame_Base, GameOrientation> map = new();

    private void Awake()
    {
        map.Clear();
        foreach (Entry e in minigames)
        {
            if (e == null || e.minigame == null)
                continue;

            map[e.minigame] = e.orientation;
            e.minigame.Started += OnMinigameStarted;
            e.minigame.Ended += OnMinigameEnded;
        }
    }

    private void OnDestroy()
    {
        foreach (Entry e in minigames)
        {
            if (e == null || e.minigame == null)
                continue;

            e.minigame.Started -= OnMinigameStarted;
            e.minigame.Ended -= OnMinigameEnded;
        }
    }

    private void Start()
    {
        Apply(menuOrientation); // start in the menu orientation
    }

    private void OnMinigameStarted(Minigame_Base minigame)
    {
        Apply(map.TryGetValue(minigame, out GameOrientation o) ? o : menuOrientation);
    }

    private void OnMinigameEnded(Minigame_Base minigame)
    {
        Apply(menuOrientation); // back to the menu orientation
    }

    private void Apply(GameOrientation orientation)
    {
        if (!Application.isMobilePlatform && !forceOnDesktop)
            return;

        Screen.orientation = orientation == GameOrientation.Portrait
            ? ScreenOrientation.Portrait
            : ScreenOrientation.LandscapeLeft;
    }

    private void ApplyPreview()
    {
#if UNITY_EDITOR
        Vector2Int res;
        if (previewAspectRatio)
            res = previewOrientation == GameOrientation.Portrait ? portraitResolution : landscapeResolution;
        else
            res = new Vector2Int(1920, 1080); // Full HD when preview is turned off

        GameViewSizeSetter.SetResolution(res.x, res.y);
#endif
    }

#if UNITY_EDITOR
    // Switches the Game view to a fixed resolution via reflection (the API is internal).
    private static class GameViewSizeSetter
    {
        public static void SetResolution(int width, int height)
        {
            try
            {
                Assembly editorAsm = typeof(Editor).Assembly;
                Type sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
                Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object instance = singletonType.GetProperty("instance").GetValue(null);

                object currentGroupType = sizesType.GetProperty("currentGroupType").GetValue(instance);
                object group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { currentGroupType });

                int index = FindIndex(group, width, height);
                if (index == -1)
                {
                    AddCustomSize(editorAsm, group, width, height);
                    index = FindIndex(group, width, height);
                }

                if (index == -1)
                    return;

                Type gameViewType = editorAsm.GetType("UnityEditor.GameView");
                EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
                MethodInfo callback = gameViewType.GetMethod("SizeSelectionCallback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                callback.Invoke(window, new object[] { index, null });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Orientation_Handler] Couldn't set Game view size: {e.Message}");
            }
        }

        private static int FindIndex(object group, int width, int height)
        {
            Type groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null)
                      + (int)groupType.GetMethod("GetCustomCount").Invoke(group, null);
            MethodInfo getSize = groupType.GetMethod("GetGameViewSize");

            for (int i = 0; i < total; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                Type sizeType = size.GetType();
                int w = (int)sizeType.GetProperty("width").GetValue(size);
                int h = (int)sizeType.GetProperty("height").GetValue(size);
                if (w == width && h == height)
                    return i;
            }

            return -1;
        }

        private static void AddCustomSize(Assembly editorAsm, object group, int width, int height)
        {
            Type sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
            Type sizeTypeEnum = editorAsm.GetType("UnityEditor.GameViewSizeType");
            ConstructorInfo ctor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });

            object fixedRes = Enum.Parse(sizeTypeEnum, "FixedResolution");
            object newSize = ctor.Invoke(new[] { fixedRes, width, height, $"{width}x{height}" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
        }
    }
#endif
}
