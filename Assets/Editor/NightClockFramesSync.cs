using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only: keeps Assets/Resources/NightClockFrames.asset (what NightClock loads at runtime) in step
// with the numbered sprites in Assets/Sprites/clock it (0.png = no progress ... 12.png = full circle).
// Runs on its own whenever scripts reload or a sprite in that folder is (re)imported, so dropping in new
// clock art just works. Tools > Sync Night Clock Frames runs it by hand.
[InitializeOnLoad]
public static class NightClockFramesSync
{
    const string SpriteFolder = "Assets/Sprites/clock it";
    const string ResourcesFolder = "Assets/Resources";
    const string AssetPath = ResourcesFolder + "/NightClockFrames.asset";

    static NightClockFramesSync() => EditorApplication.delayCall += Sync;

    [MenuItem("Tools/Sync Night Clock Frames")]
    public static void Sync()
    {
        if (!AssetDatabase.IsValidFolder(SpriteFolder)) return;

        // Numbered files in order, so "10" doesn't sort before "2"
        SortedDictionary<int, string> paths = new SortedDictionary<int, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (int.TryParse(Path.GetFileNameWithoutExtension(path), out int index)) paths[index] = path;
        }
        if (paths.Count == 0) return;

        Sprite[] frames = new Sprite[paths.Count];
        int i = 0;
        foreach (string path in paths.Values)
        {
            EnsureSprite(path);
            frames[i++] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        foreach (Sprite s in frames)
        {
            if (s == null)
            {
                Debug.LogWarning("[NightClock] A clock frame isn't imported as a Sprite yet - will retry on the next import.");
                return;
            }
        }

        NightClockFrames asset = AssetDatabase.LoadAssetAtPath<NightClockFrames>(AssetPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
            asset = ScriptableObject.CreateInstance<NightClockFrames>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        else if (SameFrames(asset.frames, frames))
        {
            return;
        }

        asset.frames = frames;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NightClock] Synced {frames.Length} clock frames into {AssetPath}.");
    }

    // The art is 32x32 pixel art: import as a single Sprite and don't blur it when it's drawn bigger
    static void EnsureSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    static bool SameFrames(Sprite[] a, Sprite[] b)
    {
        if (a == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    // Catches the sprites finishing their first import (or being replaced) after the scripts have loaded
    class Watcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.StartsWith(SpriteFolder)) continue;
                EditorApplication.delayCall += Sync;
                return;
            }
        }
    }
}
