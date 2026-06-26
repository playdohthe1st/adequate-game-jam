using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SpriteSheetToAnimEditor : EditorWindow
{
    DefaultAsset spriteFolder;
    string animName = "NewAnimation";
    float frameRate = 14f;
    bool loop = true;

    [MenuItem("Tools/Sprite Sheet to Anim")]
    static void Open() => GetWindow<SpriteSheetToAnimEditor>("Sprite Sheet to Anim");

    void OnGUI()
    {
        GUILayout.Label("Folder of Sprites to Animation Clip", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        spriteFolder = (DefaultAsset)EditorGUILayout.ObjectField("Sprite Folder", spriteFolder, typeof(DefaultAsset), false);
        animName = EditorGUILayout.TextField("Animation Name", animName);
        frameRate = EditorGUILayout.FloatField("Frame Rate (fps)", frameRate);
        loop = EditorGUILayout.Toggle("Loop", loop);

        EditorGUILayout.Space();

        bool ready = spriteFolder != null && frameRate > 0f && !string.IsNullOrEmpty(animName);

        EditorGUI.BeginDisabledGroup(!ready);
        if (GUILayout.Button("Create .anim"))
            CreateAnim();
        EditorGUI.EndDisabledGroup();

        if (!ready)
            EditorGUILayout.HelpBox("Drag a folder of individual sprite assets from the Project window.", MessageType.Info);
    }

    static int ExtractTrailingNumber(string name)
    {
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i])) i--;
        return int.TryParse(name.Substring(i + 1), out int n) ? n : -1;
    }

    void CreateAnim()
    {
        string folderPath = AssetDatabase.GetAssetPath(spriteFolder);
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });

        var sprites = new List<Sprite>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Only direct children of the folder, not sub-folders
            if (System.IO.Path.GetDirectoryName(path).Replace('\\', '/') != folderPath)
                continue;
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) sprites.Add(s);
        }

        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog("No Sprites Found",
                "No sprites found directly inside the selected folder.",
                "OK");
            return;
        }

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

        string savePath = folderPath + "/" + animName + ".anim";

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
