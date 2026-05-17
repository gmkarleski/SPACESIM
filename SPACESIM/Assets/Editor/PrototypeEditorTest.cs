using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceSim.Foundation.EditorTools
{
    /// <summary>
    /// Editor-side smoke test for the Phase 0 prototype project setup.
    ///
    /// Registers a menu item under "Prototype/Verify Setup". When invoked, the menu item
    /// logs the Unity version, the parsed contents of Packages/manifest.json, and a final
    /// confirmation line that editor scripting infrastructure is functional.
    ///
    /// Run this once after opening the project in Unity to confirm the file-level setup
    /// from commit 027 is wired through to the editor at runtime.
    /// </summary>
    public static class PrototypeEditorTest
    {
        private const string MenuItemPath = "Prototype/Verify Setup";

        [MenuItem(MenuItemPath)]
        public static void VerifySetup()
        {
            Debug.Log("=== Prototype/Verify Setup ===");

            // 1. Unity version
            Debug.Log($"Unity version: {Application.unityVersion}");
            Debug.Log($"Editor platform: {Application.platform}");

            // 2. manifest.json contents
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"manifest.json not found at expected path: {manifestPath}");
                return;
            }

            try
            {
                string manifestText = File.ReadAllText(manifestPath);
                Debug.Log($"manifest.json located at: {manifestPath}");
                Debug.Log($"manifest.json byte length: {manifestText.Length}");

                // Log each dependency line for human review. Unity's JsonUtility does not
                // handle the dict-of-strings shape of manifest.json without a wrapping class,
                // so log line-by-line rather than parse.
                string[] lines = manifestText.Split('\n');
                int dependencyCount = 0;
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("\"com.unity."))
                    {
                        Debug.Log($"  package: {trimmed.TrimEnd(',')}");
                        dependencyCount++;
                    }
                }
                Debug.Log($"Total dependencies detected: {dependencyCount}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read manifest.json: {ex.Message}");
                return;
            }

            // 3. Confirm specific packages this commit added are present
            string manifestRaw = File.ReadAllText(manifestPath);
            bool hasMathematics = manifestRaw.Contains("com.unity.mathematics");
            bool hasBurst = manifestRaw.Contains("com.unity.burst");
            bool hasInputSystem = manifestRaw.Contains("com.unity.inputsystem");
            bool hasUrp = manifestRaw.Contains("com.unity.render-pipelines.universal");

            Debug.Log($"  com.unity.mathematics present: {hasMathematics}");
            Debug.Log($"  com.unity.burst present: {hasBurst}");
            Debug.Log($"  com.unity.inputsystem present: {hasInputSystem}");
            Debug.Log($"  com.unity.render-pipelines.universal present: {hasUrp}");

            // 4. Editor scripting infrastructure confirmation
            Debug.Log("Editor scripting infrastructure verified");
            Debug.Log("=== Verify Setup complete ===");
        }
    }
}
