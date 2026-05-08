using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Toolbars;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class SceneDropdown
{
    private const string ElementPath = "Scene Dropdown";
    private const string ShowBuildScenesOnlyKey = "SceneDropdown.ShowBuildScenesOnly";
    private static readonly List<string> SceneList = new();
    private static bool ShowBuildScenesOnly { get; set; }

    [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
    public static MainToolbarElement CreateSceneDropdown()
    {
        var sceneName = SceneManager.GetActiveScene().name;

        if (string.IsNullOrWhiteSpace(sceneName)) sceneName = "Scene";

        return new MainToolbarDropdown(
            new MainToolbarContent(sceneName, "Load a scene. Hold shift to load additively."),
            ShowDropdown)
        {
            populateContextMenu = menu => menu.AppendAction(
                "Only Show Scenes in Build Settings",
                _ =>
                {
                    ShowBuildScenesOnly = !ShowBuildScenesOnly;
                    EditorPrefs.SetBool(ShowBuildScenesOnlyKey, ShowBuildScenesOnly);
                    RefreshSceneList();
                },
                _ => ShowBuildScenesOnly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal)
        };
    }

    private static void ShowDropdown(Rect dropDownRect) => 
        UnityEditor.PopupWindow.Show(dropDownRect, new SceneDropdownPopup());

    private class SceneDropdownPopup : PopupWindowContent
    {
        private readonly SearchField _searchField = new();
        private Vector2 _scrollPosition;
        private string _searchText = string.Empty;
        private GUIStyle _sceneButtonStyle;

        public override Vector2 GetWindowSize()
        {
            const float width = 300f;
            const float maxHeight = 700f;
            const float padding = 6f;
            const float searchSpacing = 4f;
            float searchAreaHeight = padding * 2f + EditorGUIUtility.singleLineHeight + searchSpacing;
            float rowHeight = EditorGUIUtility.singleLineHeight + 2f;
            float height = SceneList.Count == 0
                ? EditorGUIUtility.singleLineHeight + searchAreaHeight
                : Mathf.Min(maxHeight, SceneList.Count * rowHeight + searchAreaHeight);

            return new Vector2(width, height);
        }

        public override void OnOpen() => _searchField.SetFocus();

        public override void OnGUI(Rect rect)
        {
            _sceneButtonStyle ??= new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleLeft
            };

            if (Event.current is { type: EventType.KeyDown or EventType.KeyUp, keyCode: KeyCode.LeftShift or KeyCode.RightShift })
                editorWindow.Repaint();

            const float padding = 6f;
            GUILayout.BeginArea(new Rect(padding, padding, rect.width - padding * 2f, rect.height - padding * 2f));

            Rect searchRect =
                GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            _searchText = _searchField.OnGUI(searchRect, _searchText);
            GUILayout.Space(4f);

            if (SceneList.Count == 0)
            {
                string message = ShowBuildScenesOnly
                    ? "No Scenes in Build Settings"
                    : "No Scenes in Project";
                GUILayout.Label(message, EditorStyles.centeredGreyMiniLabel);
                GUILayout.EndArea();
                return;
            }

            bool loadAdditively = Event.current is { shift: true };
            bool hasMatchingScene = false;
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            foreach (string scenePath in SceneList)
            {
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                if (!MatchesSearch(sceneName, scenePath))
                    continue;

                hasMatchingScene = true;
                string menuLabel = loadAdditively ? $"+ {sceneName}" : sceneName;
                if (GUILayout.Button(new GUIContent(menuLabel, scenePath), _sceneButtonStyle))
                {
                    editorWindow.Close();
                    LoadScene(scenePath, loadAdditively);
                }
            }

            if (!hasMatchingScene)
                GUILayout.Label("No matching scenes", EditorStyles.centeredGreyMiniLabel);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private bool MatchesSearch(string sceneName, string scenePath)
        {
            return string.IsNullOrWhiteSpace(_searchText)
                   || sceneName.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase)
                   || scenePath.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void LoadScene(string scenePath, bool loadAdditively)
    {
        if (Application.isPlaying)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Scene '{sceneName}' is not in the Build Settings.");
                return;
            }

            var mode = loadAdditively ? LoadSceneMode.Additive : LoadSceneMode.Single;
            SceneManager.LoadScene(sceneName, mode);
            return;
        }

        if (!File.Exists(scenePath))
        {
            Debug.LogError($"Scene at path '{scenePath}' does not exist.");
            return;
        }

        if (!loadAdditively && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var openMode = loadAdditively ? OpenSceneMode.Additive : OpenSceneMode.Single;
        EditorSceneManager.OpenScene(scenePath, openMode);
    }

    private static void RefreshSceneList()
    {
        SceneList.Clear();
        if (ShowBuildScenesOnly)
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                SceneList.Add(SceneUtility.GetScenePathByBuildIndex(i));
        else
            SceneList.AddRange(Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories));
    }

    private static void RefreshToolbar(Scene _, Scene __) => MainToolbar.Refresh(ElementPath);

    static SceneDropdown()
    {
        ShowBuildScenesOnly = EditorPrefs.GetBool(ShowBuildScenesOnlyKey, true);
        
        EditorApplication.projectChanged += RefreshSceneList;
        EditorBuildSettings.sceneListChanged += RefreshSceneList;
        EditorSceneManager.activeSceneChangedInEditMode += RefreshToolbar;
        
        RefreshSceneList();
    }
}