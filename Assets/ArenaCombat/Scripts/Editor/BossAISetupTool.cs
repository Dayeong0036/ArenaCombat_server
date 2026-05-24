using UnityEngine;
using UnityEditor;
using ArenaCombat.Core.AI;

public static class BossAISetupTool
{
    static readonly string[] PairNames = { "HH", "MH", "RH", "CH", "MM", "MR", "MC", "RR", "RC", "CC" };

    [MenuItem("ArenaCombat/Boss AI/Wire Pair Combos To Pool Manager")]
    static void WirePairCombos()
    {
        var mgr = Object.FindAnyObjectByType<BossAIPoolManager>();
        if (mgr == null)
        {
            Debug.LogError("[BossAISetup] BossAIPoolManager not found in scene.");
            return;
        }

        var so = new SerializedObject(mgr);
        var prop = so.FindProperty("_combos");
        if (prop == null || !prop.isArray)
        {
            Debug.LogError("[BossAISetup] _combos property not found.");
            return;
        }

        if (prop.arraySize != 10)
            prop.arraySize = 10;

        int wired = 0;
        for (int i = 0; i < PairNames.Length; i++)
        {
            string assetName = $"BossAI_{PairNames[i]}";
            string[] guids = AssetDatabase.FindAssets($"t:BossAIDefinition {assetName}", new[] { "Assets/ArenaCombat/Data/BossAI" });

            if (guids.Length == 0)
            {
                Debug.LogWarning($"[BossAISetup] Asset not found: {assetName}");
                continue;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var def = AssetDatabase.LoadAssetAtPath<BossAIDefinition>(path);
            if (def == null) continue;

            prop.GetArrayElementAtIndex(i).objectReferenceValue = def;
            wired++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mgr);
        Debug.Log($"[BossAISetup] Wired {wired}/10 pair combos to BossAIPoolManager.");
    }

    [MenuItem("ArenaCombat/Boss AI/Validate Pool Manager")]
    static void ValidatePoolManager()
    {
        var mgr = Object.FindAnyObjectByType<BossAIPoolManager>();
        if (mgr == null)
        {
            Debug.LogError("[Validate] BossAIPoolManager not found.");
            return;
        }

        var so = new SerializedObject(mgr);
        var defaultProp = so.FindProperty("_defaultAI");
        var combosProp = so.FindProperty("_combos");

        if (defaultProp == null || combosProp == null)
        {
            Debug.LogError("[Validate] _defaultAI or _combos property not found.");
            return;
        }

        Debug.Log("══════════════════════════════════════════");
        Debug.Log("[Validate] BossAIPoolManager Status");
        Debug.Log("══════════════════════════════════════════");

        if (defaultProp.objectReferenceValue == null)
            Debug.LogError("  _defaultAI: NULL (must be assigned!)");
        else
            Debug.Log($"  _defaultAI: {defaultProp.objectReferenceValue.name}");

        Debug.Log($"  _combos size: {combosProp.arraySize}");

        int wired = 0;
        int nullCount = 0;
        for (int i = 0; i < combosProp.arraySize && i < PairNames.Length; i++)
        {
            var elem = combosProp.GetArrayElementAtIndex(i);
            if (elem.objectReferenceValue != null)
            {
                Debug.Log($"  [{i}] {PairNames[i]}: {elem.objectReferenceValue.name}");
                wired++;
            }
            else
            {
                Debug.LogWarning($"  [{i}] {PairNames[i]}: NULL");
                nullCount++;
            }
        }

        Debug.Log($"  Total: {wired}/10 wired, {nullCount} empty");
        Debug.Log("══════════════════════════════════════════");
    }

    [MenuItem("ArenaCombat/Boss AI/[Runtime] Force Evaluate + Swap Now")]
    static void ForceEvalSwap()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BossAI] Must be in Play mode.");
            return;
        }
        BossAIPoolManager.Instance?.EvaluateAndSwap();
    }
}
