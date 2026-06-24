using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SpriteSheetToAnimEditor : EditorWindow
{
    Texture2D spriteSheet;
    string animName = "NewAnimation";
    float frameRate = 14f;
    bool loop = true;

    [MenuItem("Tools/Sprite Sheet to Anim")]
    static void Open() => GetWindow<SpriteSheetToAnimEditor>("Sprite Sheet to Anim");

    void OnGUI()
    {
        GUILayout.Label("Sprite Sheet to Animation Clip", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        spriteSheet = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", spriteSheet, typeof(Texture2D), false);
        animName = EditorGUILayout.TextField("Animation Name", animName);
        frameRate = EditorGUILayout.FloatField("Frame Rate (fps)", frameRate);
        loop = EditorGUILayout.Toggle("Loop", loop);

        EditorGUILayout.Space();

        bool ready = spriteSheet != null && frameRate > 0f && !string.IsNullOrEmpty(animName);

        EditorGUI.BeginDisabledGroup(!ready);
        if (GUILayout.Button("Create .anim"))
            CreateAnim();
        EditorGUI.EndDisabledGroup();

        if (!ready)
            EditorGUILayout.HelpBox("Assign a sprite sheet with sprites already sliced via the Sprite Editor (Multiple mode).", MessageType.Info);
    }

    static int ExtractTrailingNumber(string name)
    {
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i])) i--;
        return int.TryParse(name.Substring(i + 1), out int n) ? n : -1;
    }

    void CreateAnim()
    {
        string sheetPath = AssetDatabase.GetAssetPath(spriteSheet);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);

        var sprites = new List<Sprite>();
        foreach (var a in assets)
        {
            if (a is Sprite s)
                sprites.Add(s);
        }

        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog("No Sprites Found",
                "No sprite sub-assets found. Make sure the texture is imported as Sprite (Multiple) and sliced in the Sprite Editor.",
                "OK");
            return;
        }

        // Natural numeric sort so sheet_10 comes after sheet_9, not sheet_1
        sprites.Sort((a, b) =>
        {
            int ai = ExtractTrailingNumber(a.name);
            int bi = ExtractTrailingNumber(b.name);
            return ai != bi ? ai.CompareTo(bi) : string.CompareOrdinal(a.name, b.name);
        });

        var clip = new AnimationClip { frameRate = frameRate };

        if (loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        float secondsPerFrame = 1f / frameRate;
        var keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * secondsPerFrame,
                value = sprites[i]
            };
        }

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        string dir = Path.GetDirectoryName(sheetPath);
        string savePath = Path.Combine(dir, animName + ".anim").Replace('\\', '/');

        // Avoid overwriting without asking
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath) != null)
        {
            if (!EditorUtility.DisplayDialog("Overwrite?",
                    $"{savePath} already exists. Overwrite?", "Overwrite", "Cancel"))
                return;
        }

        AssetDatabase.CreateAsset(clip, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(clip);
        Debug.Log($"Created animation clip: {savePath} ({sprites.Count} frames @ {frameRate} fps)");
    }
}
