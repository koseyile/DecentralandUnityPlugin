﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dcl
{
    public class DclExporter : EditorWindow
    {
        const int SPACE_SIZE = 5;

        [MenuItem("Decentraland/Scene Exporter", false, 1)]
        static void Init()
        {
            var window = (DclExporter) GetWindow(typeof(DclExporter));
            window.titleContent = new GUIContent("DCL Exporter");
            window.Show();
            window.minSize = new Vector2(240, 400);
        }

        private DclSceneMeta sceneMeta;

        private bool editParcelsMode;
        private string editParcelsText;

        private string exportPath;

        void OnGUI()
        {
            if (!sceneMeta)
            {
                CheckAndGetDclSceneMetaObject();
            }

            ParcelGUI();
            GUILayout.Space(SPACE_SIZE);

            StatGUI();
            GUILayout.Space(SPACE_SIZE);

            EditorGUI.BeginChangeCheck();

            OptionsGUI();
            GUILayout.Space(SPACE_SIZE);

            OwnerGUI();
            GUILayout.Space(SPACE_SIZE * 3);

            GUILayout.Label(LabelLocalization.DCLProjectPath, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            exportPath = EditorPrefs.GetString("DclExportPath");
            var newExportPath = EditorGUILayout.TextField(exportPath);
            if (GUILayout.Button("...", GUILayout.Width(24), GUILayout.Height(24)))
            {
                newExportPath = EditorUtility.OpenFolderPanel(LabelLocalization.SelectDCLProjectPath, exportPath, "");
                if (string.IsNullOrEmpty(newExportPath)) newExportPath = exportPath;
            }

            if (newExportPath != exportPath)
            {
                exportPath = newExportPath;
                EditorPrefs.SetString("DclExportPath", newExportPath);
            }

            EditorGUILayout.EndHorizontal();


            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(sceneMeta);
                EditorSceneManager.MarkSceneDirty(sceneMeta.gameObject.scene);
            }

            GUILayout.Space(SPACE_SIZE);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var oriColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Export", GUILayout.Width(220), GUILayout.Height(32)))
            {
                Export();
            }

            GUI.backgroundColor = oriColor;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(SPACE_SIZE * 2);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Init Project", GUILayout.Width(105)))
            {
                if (Directory.Exists(exportPath))
                {
                    if (EditorUtility.DisplayDialog("Confirm to init DCL project?",
                        string.Format("This will run 'dcl init' command in {0}. Are you sure?", exportPath), "Yes",
                        "No"))
                    {
                        DclCLI.DclInit(exportPath);
                    }
                }
                else
                {
                    ShowNotification(new GUIContent("You need to select a valid project folder!"));
                }
            }

            if (GUILayout.Button("Run Project", GUILayout.Width(105)))
            {
                if (Directory.Exists(exportPath))
                {
                    if (EditorUtility.DisplayDialog("Confirm to run DCL project?",
                        string.Format("This will run 'dcl start' command in {0}. Are you sure?", exportPath), "Yes",
                        "No"))
                    {
                        DclCLI.DclStart(exportPath);
                        ShowNotification(new GUIContent("DCL is starting\nWait 10 seconds"));
                    }
                }
                else
                {
                    ShowNotification(new GUIContent("You need to select a valid project folder!"));
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(SPACE_SIZE * 2);

            #region Help Link

            string url = "https://github.com/fairwood/DecentralandUnityPlugin";
            if (GUILayout.Button(string.Format(LabelLocalization.Document, url), EditorStyles.helpBox))
            {
                Application.OpenURL(url);
            }

            #endregion
        }

        void ParcelGUI()
        {
            EditorGUILayout.BeginVertical("box");
            var parcels = sceneMeta.parcels;
            EditorGUILayout.BeginHorizontal();
            var style = EditorStyles.foldout;
            style.fontStyle = FontStyle.Bold;
            var foldout = EditorUtil.GUILayout.AutoSavedFoldout("DclFoldParcel", string.Format("Parcels({0})", parcels.Count), true, style);
            if (foldout)
            {
                if (editParcelsMode)
                {
                    if (GUILayout.Button("Save"))
                    {
                        CheckAndGetDclSceneMetaObject();
                        try
                        {
                            var newParcels = new List<ParcelCoordinates>();
                            ParseTextToCoordinates(editParcelsText, newParcels);
                            parcels = newParcels;
                            sceneMeta.parcels = parcels;
                            editParcelsMode = false;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.Message);
                            EditorUtility.DisplayDialog("Invalid Format", e.Message, "OK");
                        }

                        EditorUtility.SetDirty(sceneMeta);
                        EditorSceneManager.MarkSceneDirty(sceneMeta.gameObject.scene);

                    }

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        editParcelsMode = false;
                        CheckAndGetDclSceneMetaObject();
                    }
                }
                else
                {
                    if (GUILayout.Button("Edit"))
                    {
                        var sb = new StringBuilder();
                        if (parcels.Count > 0)
                        {
                            sb.Append(ParcelToStringBuilder(parcels[0]));
                            for (int i = 1; i < parcels.Count; i++)
                            {
                                sb.Append('\n').Append(ParcelToStringBuilder(parcels[i]));
                            }
                        }

                        editParcelsText = sb.ToString();
                        editParcelsMode = true;
                        CheckAndGetDclSceneMetaObject();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel = 1;
            if (foldout)
            {
                if (editParcelsMode)
                {
                    editParcelsText = EditorGUILayout.TextArea(editParcelsText, GUILayout.Height(120));
                }
                else
                {
                    var sb = new StringBuilder();
                    if (parcels.Count > 0)
                    {
                        sb.Append(ParcelToStringBuilder(parcels[0])).Append(" (base)");
                        for (int i = 1; i < parcels.Count; i++)
                        {
                            sb.Append('\n').Append(ParcelToStringBuilder(parcels[i]));
                        }
                    }

                    EditorGUILayout.LabelField(sb.ToString(), GUILayout.Height(120));
                }
            }
            
            EditorGUI.indentLevel = 0;
            EditorGUILayout.EndVertical();
        }

        #region StatGUI

        void StatGUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            var oriFoldout = EditorPrefs.GetBool("DclFoldStat", true);
            var foldout = EditorGUILayout.Foldout(oriFoldout, "Statistics", true);
            if (foldout)
            {
                if (GUILayout.Button("Refresh"))
                {
                    sceneMeta.RefreshStatistics();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel = 1;
            if (foldout)
            {
                GUILayout.Label(LabelLocalization.KeepTheseNumbersSmaller, EditorStyles.centeredGreyMiniLabel);
                var n = sceneMeta.parcels.Count;
                var sceneStatistics = sceneMeta.sceneStatistics;
                StatisticsLineGUI("Triangles", sceneStatistics.triangleCount, LimitationConfigs.GetMaxTriangles(n));
                StatisticsLineGUI("Entities", sceneStatistics.entityCount, LimitationConfigs.GetMaxTriangles(n));
                StatisticsLineGUI("Bodies", sceneStatistics.bodyCount, LimitationConfigs.GetMaxBodies(n));
                StatisticsLineGUI("Materials", sceneStatistics.materialCount, LimitationConfigs.GetMaxMaterials(n));
                StatisticsLineGUI("Textures", sceneStatistics.textureCount, LimitationConfigs.GetMaxTextures(n));
                StatisticsLineGUI("Height", sceneStatistics.maxHeight, LimitationConfigs.GetMaxHeight(n));
            }

            if (foldout != oriFoldout) EditorPrefs.SetBool("DclFoldStat", foldout);

            WarningsGUI();
            EditorGUI.indentLevel = 0;
            EditorGUILayout.EndVertical();
        }

        void StatisticsLineGUI(string indexName, long leftValue, long rightValue)
        {
            var oriColor = GUI.contentColor;
            EditorGUILayout.BeginHorizontal();
            if (leftValue > rightValue)
            {
                GUILayout.Label(DclEditorSkin.WarningIconSmall, GUILayout.Width(20));
                GUI.contentColor = Color.yellow;
            }
            EditorGUILayout.LabelField(indexName, string.Format("{0} / {1}", leftValue, rightValue));
            EditorGUILayout.EndHorizontal();
            GUI.contentColor = oriColor;
        }

        void StatisticsLineGUI(string indexName, float leftValue, float rightValue)
        {
            var oriColor = GUI.contentColor;
            EditorGUILayout.BeginHorizontal();
            if (leftValue > rightValue)
            {
                GUILayout.Label(DclEditorSkin.WarningIconSmall, GUILayout.Width(20));
                GUI.contentColor = Color.yellow;
            }
            EditorGUILayout.LabelField(indexName, string.Format("{0} / {1}", leftValue, rightValue));
            EditorGUILayout.EndHorizontal();
            GUI.contentColor = oriColor;
        }

        #endregion

        #region WarningsGUI

        void WarningsGUI()
        {
            var foldout = EditorPrefs.GetBool("DclFoldStat", true);
            if (foldout)
            {
                var warningCount = sceneMeta.sceneWarningRecorder.OutOfLandWarnings.Count +
                                   sceneMeta.sceneWarningRecorder.UnsupportedShaderWarnings.Count +
                                   sceneMeta.sceneWarningRecorder.InvalidTextureWarnings.Count;

                //            GUILayout.Label(string.Format("Warnings({0})", warningCount));
                if (warningCount > 0)
                {
                    GUILayout.Label("Click the warning to focus in the scene", EditorStyles.centeredGreyMiniLabel);

                    foreach (var outOfLandWarning in sceneMeta.sceneWarningRecorder.OutOfLandWarnings)
                    {
                        WarningLineGUI(string.Format("Out of land range : {0}", outOfLandWarning.meshRenderer.name),
                            null, outOfLandWarning.meshRenderer.gameObject);
                    }

                    foreach (var warning in sceneMeta.sceneWarningRecorder.UnsupportedShaderWarnings)
                    {
                        var path = AssetDatabase.GetAssetPath(warning.renderer);
                        WarningLineGUI(string.Format("Unsupported shader : {0}", warning.renderer.name),
                            LabelLocalization.OnlyStandardShaderSupported, path);
                    }

                    foreach (var warning in sceneMeta.sceneWarningRecorder.InvalidTextureWarnings)
                    {
                        var path = AssetDatabase.GetAssetPath(warning.renderer);
                        WarningLineGUI(string.Format("Invalid texture size : {0}", warning.renderer.name),
                            LabelLocalization.TextureSizeMustBe, path);
                    }
                }
            }
        }

        void WarningLineGUI(string text, string hintMessage, GameObject gameObject)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(DclEditorSkin.WarningIconSmall, GUILayout.Width(20));
            var oriColor = GUI.contentColor;
            GUI.contentColor = Color.yellow;
            if (GUILayout.Button(text, EditorStyles.label))
            {
                if (hintMessage != null) ShowNotification(new GUIContent(hintMessage));
                EditorGUIUtility.PingObject(gameObject);
            }

            EditorGUILayout.EndHorizontal();
            GUI.contentColor = oriColor;
        }

        void WarningLineGUI(string text, string hintMessage, string assetPath)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(DclEditorSkin.WarningIconSmall, GUILayout.Width(20));
            var oriColor = GUI.contentColor;
            GUI.contentColor = Color.yellow;
            if (GUILayout.Button(text, EditorStyles.label))
            {
                if (hintMessage != null) ShowNotification(new GUIContent(hintMessage));
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            }

            EditorGUILayout.EndHorizontal();
            GUI.contentColor = oriColor;
        }

        #endregion

        void OptionsGUI()
        {
            EditorGUILayout.BeginVertical("box");

            var oriFoldout = EditorPrefs.GetBool("DclFoldOptions");
            var foldout = EditorGUILayout.Foldout(oriFoldout, "Options", true);
            if (foldout)
            {
                //mExportPBR = EditorGUILayout.Toggle("Export PBR Material", mExportPBR);
                //mExportAnimation = EditorGUILayout.Toggle("Export animation (beta)", mExportAnimation);
                //mConvertImage = EditorGUILayout.Toggle("Convert Images", mConvertImage);
                //mBuildZip = EditorGUILayout.Toggle("Build Zip", mBuildZip);
            }

            if (foldout != oriFoldout) EditorPrefs.SetBool("DclFoldOptions", foldout);

            EditorGUILayout.EndVertical();
        }

        void OwnerGUI()
        {
            EditorGUILayout.BeginVertical("box");

            var oriFoldout = EditorPrefs.GetBool("DclBoldOwner");
            var foldout = EditorGUILayout.Foldout(oriFoldout, "Owner Info (optional)", true);
            if (foldout)
            {
                EditorGUI.indentLevel = 1;
                sceneMeta.ethAddress = EditorGUILayout.TextField("Address", sceneMeta.ethAddress);
                sceneMeta.contactName = EditorGUILayout.TextField("Name", sceneMeta.contactName);
                sceneMeta.email = EditorGUILayout.TextField("Email", sceneMeta.email);
                EditorGUI.indentLevel = 0;
            }

            if (foldout != oriFoldout) EditorPrefs.SetBool("DclBoldOwner", foldout);

            EditorGUILayout.EndVertical();
        }

        private DateTime nextTimeRefresh;

        private void Update()
        {
            if (DateTime.Now > nextTimeRefresh)
            {
                if (!sceneMeta)
                {
                    CheckAndGetDclSceneMetaObject();
                }

                sceneMeta.RefreshStatistics();
                Repaint();
                nextTimeRefresh = DateTime.Now.AddSeconds(1);
            }
        }


        string GetSceneTsxFileTemplate()
        {
            var guids = AssetDatabase.FindAssets("dcl_scene_tsx_template");
            if (guids.Length <= 0)
            {
                if (EditorUtility.DisplayDialog("Cannot find dcl_scene_tsx_template.txt in the project!",
                    "Please re-install Decentraland Unity Plugin asset to fix this problem.", "Re-install", "Back"))
                {
                    //TODO:
                }

                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var template = AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset)) as TextAsset;
            return template.text;
        }

        string GetSceneJsonFileTemplate()
        {
            var guids = AssetDatabase.FindAssets("dcl_scene_json_template");
            if (guids.Length <= 0)
            {
                if (EditorUtility.DisplayDialog("Cannot find dcl_scene_json_template.txt in the project!",
                    "Please re-install Decentraland Unity Plugin asset to fix this problem.", "Re-install", "Back"))
                {
                    //TODO:
                }

                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var template = AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset)) as TextAsset;
            return template.text;
        }

        string GetParcelsString()
        {
            /*
          "30,-15",
          "30,-16",
          "31,-15"*/
            var sb = new StringBuilder();
            if (sceneMeta.parcels.Count > 0)
            {
                const string indentUnit = "  ";
                sb.AppendIndent(indentUnit, 3).Append(ParcelToString(sceneMeta.parcels[0]));
                for (var i = 1; i < sceneMeta.parcels.Count; i++)
                {
                    sb.Append(",\n");
                    sb.AppendIndent(indentUnit, 3).Append(ParcelToString(sceneMeta.parcels[i]));
                }
            }

            return sb.ToString();
        }

        void CheckAndGetDclSceneMetaObject()
        {
            var rootGameObjects = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var roots = SceneManager.GetSceneAt(i).GetRootGameObjects();
                rootGameObjects.AddRange(roots);
            }

            foreach (var go in rootGameObjects)
            {
                if (go.name == ".dcl")
                {
                    sceneMeta = go.GetComponent<DclSceneMeta>();
                    if (!sceneMeta)
                    {
                        sceneMeta = go.AddComponent<DclSceneMeta>();
                        EditorUtility.SetDirty(sceneMeta);
                        EditorSceneManager.MarkSceneDirty(go.scene);
                    }

                    return;
                }
            }

            //Did not find .dcl object. Create one.
            var o = new GameObject(".dcl");
            sceneMeta = o.AddComponent<DclSceneMeta>();
            EditorUtility.SetDirty(sceneMeta);
            EditorSceneManager.MarkSceneDirty(o.scene);
        }

        void Export()
        {
            if (string.IsNullOrEmpty(exportPath))
            {
                EditorUtility.DisplayDialog("NO Path!", "You must assign the export path!", null, "OK");
                return;
            }

            if (!Directory.Exists(exportPath)) Directory.CreateDirectory("exportPath");

            //delete all files in exportPath/unity_assets/

            var unityAssetsFolderPath = Path.Combine(exportPath, "unity_assets/");
            if (Directory.Exists(unityAssetsFolderPath))
            {
                ClearFolder(unityAssetsFolderPath);
            }
            else
            {
                Directory.CreateDirectory(unityAssetsFolderPath);
            }

            var meshesToExport = new List<GameObject>();
            var sceneXmlBuilder = new StringBuilder();
            var statistics = new SceneStatistics();

            SceneTraverser.TraverseAllScene(sceneXmlBuilder, meshesToExport, statistics, null);

            var sceneXml = sceneXmlBuilder.ToString();

            //scene.tsx
            var fileTxt = GetSceneTsxFileTemplate();
            fileTxt = fileTxt.Replace("{XML}", sceneXml);
            var filePath = Path.Combine(exportPath, "scene.tsx");
            File.WriteAllText(filePath, fileTxt);

            //glTF in unity_asset
            foreach (var go in meshesToExport)
            {
                sceneMeta.sceneToGlTFWiz.ExportGameObjectAndChildren(go, Path.Combine(unityAssetsFolderPath, go.name + ".gltf"),
                    null, false, true, false, false);
            }

            //textures
            var primitiveTexturesToExport = SceneTraverser.primitiveTexturesToExport;
            foreach (var texture in primitiveTexturesToExport)
            {
                var relPath = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(relPath))
                {
                    //built-in asset
                    var bytes = ((Texture2D) texture).EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(unityAssetsFolderPath, texture.name + ".png"), bytes);
                }
                else
                {
                    var path = Application.dataPath; //<path to project folder>/Assets
                    path = path.Remove(path.Length - 6, 6) + relPath;
                    var toPath = unityAssetsFolderPath + relPath;
                    var directoryPath = Path.GetDirectoryName(toPath);
                    if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
                    File.Copy(path, toPath, true);
                }
            }

            //scene.json
            fileTxt = GetSceneJsonFileTemplate();
            fileTxt = fileTxt.Replace("{ETH_ADDRESS}", sceneMeta.ethAddress);
            fileTxt = fileTxt.Replace("{CONTACT_NAME}", sceneMeta.contactName);
            fileTxt = fileTxt.Replace("{CONTACT_EMAIL}", sceneMeta.email);
            var parcelsString = GetParcelsString();
            fileTxt = fileTxt.Replace("{PARCELS}", parcelsString);
            if (sceneMeta.parcels.Count > 0)
            {
                fileTxt = fileTxt.Replace("{BASE}", ParcelToString(sceneMeta.parcels[0]));
            }

            filePath = Path.Combine(exportPath, "scene.json");
            File.WriteAllText(filePath, fileTxt);

            Debug.Log("===Export Complete===");
        }

        #region Utils

        public static void ParseTextToCoordinates(string text, List<ParcelCoordinates> coordinates)
        {
            coordinates.Clear();
            var lines = text.Replace("\r", "").Split('\n');
            foreach (var line in lines)
            {
                var elements = line.Trim().Split(',');
                if (elements.Length == 0) continue;
                if (elements.Length != 2)
                {
                    throw new Exception("A line does not have exactly 2 elements!");
                }

                var x = int.Parse(elements[0]);
                var y = int.Parse(elements[1]);
                coordinates.Add(new ParcelCoordinates(x, y));
            }
        }

        public static StringBuilder ParcelToStringBuilder(ParcelCoordinates parcel)
        {
            return new StringBuilder().Append(parcel.x).Append(',').Append(parcel.y);
        }

        public static string Vector3ToJSONString(Vector3 v)
        {
            return string.Format("{{x:{0},y:{1},z:{2}}}", v.x, v.y, v.z);
        }

        /// <summary>
        /// Color to HEX string(e.g. #AAAAAA)
        /// </summary>
        private static string ToHexString(Color color)
        {
            var color256 = (Color32) color;
            string R = Convert.ToString(color256.r, 16);
            if (R == "0")
                R = "00";
            string G = Convert.ToString(color256.g, 16);
            if (G == "0")
                G = "00";
            string B = Convert.ToString(color256.b, 16);
            if (B == "0")
                B = "00";
            string HexColor = "#" + R + G + B;
            return HexColor.ToUpper();
        }

        public static string ParcelToString(ParcelCoordinates parcel)
        {
            return string.Format("\"{0},{1}\"", parcel.x, parcel.y);
        }

        /// <summary>
        /// Clear all content including files & folders in a folder
        /// by [x_蜡笔小新](https://www.cnblogs.com/XuPengLB/p/6393117.html)
        /// </summary>
        /// <param name="dir"></param>
        public static void ClearFolder(string dir)
        {
            foreach (string d in Directory.GetFileSystemEntries(dir))
            {
                if (File.Exists(d))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(d);
                        if (fi.Attributes.ToString().IndexOf("ReadOnly") != -1)
                            fi.Attributes = FileAttributes.Normal;
                        File.Delete(d); //直接删除其中的文件 
                    }
                    catch
                    {

                    }
                }
                else
                {
                    try
                    {
                        DirectoryInfo d1 = new DirectoryInfo(d);
                        if (d1.GetFiles().Length != 0)
                        {
                            ClearFolder(d1.FullName); ////递归删除子文件夹
                        }

                        Directory.Delete(d);
                    }
                    catch
                    {

                    }
                }
            }
        }

        #endregion
    }
}