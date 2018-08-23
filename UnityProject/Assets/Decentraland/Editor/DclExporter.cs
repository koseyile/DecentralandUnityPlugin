﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

public class DclExporter : EditorWindow
{
    [MenuItem("Decentraland/Scene Exporter")]
    static void Init()
    {
        var window = (DclExporter) GetWindow(typeof(DclExporter));
        window.Show();
    }


    public List<string> warnings = new List<string>
    {
        "Out of land range! at (12,-32)",
        "Too many triangles! at (12,-32)",
        "Too large texture! at (12,-32)",
        "Unsupported shader! at (12,-32)",
    };

    string exportPath = "";

    private bool editParcelsMode = false;
    private string editParcelsText;
    
    void OnGUI()
    {
        #region Parcels

        var parcels = DclSceneMeta.parcels;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(string.Format("Parcels({0})", parcels.Count), GUILayout.Width(100));
        if (editParcelsMode)
        {
            if (GUILayout.Button("Save"))
            {
                try
                {
                    var newParcels = new List<int[]>();
                    ParseTextToCoordinates(editParcelsText, newParcels);
                    parcels = newParcels;
                    DclSceneMeta.parcels = parcels;
                    editParcelsMode = false;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                    EditorUtility.DisplayDialog("Invalid Format", e.Message, "OK");
                }
                CheckDclSceneMetaObject();
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                editParcelsMode = false;
                CheckDclSceneMetaObject();
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
                CheckDclSceneMetaObject();
            }
        }
        EditorGUILayout.EndHorizontal();
        if (editParcelsMode)
        {
            editParcelsText = EditorGUILayout.TextArea(editParcelsText, GUILayout.Height(200));
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
            EditorGUILayout.LabelField(sb.ToString(), GUILayout.Height(200));
        }


        #endregion


        ShowWarningsSector();
        EditorGUILayout.LabelField("Export Path");
        exportPath = EditorGUILayout.TextField(exportPath);
        var oriColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Export"))
        {
            Export();
        }
        GUI.backgroundColor = oriColor;



        #region Parcel Gizmo

//        Gizmos.DrawCube(Vector3.zero, new Vector3(10, 0.1f, 10));
//        HandleUtility.Repaint();
        

        #endregion
    }
    
    void ShowWarningsSector()
    {
        var oriColor = GUI.contentColor;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(string.Format("Warnings({0})", DclSceneMeta.parcels.Count), GUILayout.Width(100));
        GUILayout.Button("Refresh");
        EditorGUILayout.EndHorizontal();
        var sb = new StringBuilder();
        if (warnings.Count > 0)
        {
            GUI.contentColor = new Color(0.8f, 0.8f, 0.8f);
            EditorGUILayout.LabelField("Click the warning to focus in the scene");

            sb.Append(WarningToStringBuilder(warnings[0]));
            for (int i = 1; i < warnings.Count; i++)
            {
                sb.Append('\n').Append(WarningToStringBuilder(warnings[i]));
            }
        }

        GUI.contentColor = new Color(1, 1f, 0f);
        EditorGUILayout.LabelField(sb.ToString(), GUILayout.Height(200));

        GUI.contentColor = oriColor;
    }

    string GetSceneFileTemplate()
    {
        var guids = AssetDatabase.FindAssets("dcl_scene_template");
        if (guids.Length <= 0)
        {
            if (EditorUtility.DisplayDialog("Cannot find dcl_scene_template.txt in the project!",
                "Please re-install Decentraland Unity Plugin asset to fix this problem.", "Re-install", "Back"))
            {
                //TODO:
            }

            return null;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        Debug.Log("Path:" + path);
        var template = AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset)) as TextAsset;
        Debug.Log("Txt:" + template.text);
        return template.text;
    }

    const string indentUnit = "  ";

    string GetSceneXml()
    {
        var rootGameObjects = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var roots = SceneManager.GetSceneAt(i).GetRootGameObjects();
            rootGameObjects.AddRange(roots);
        }


        var sceneXml = new StringBuilder();
        AppendIndent(sceneXml, indentUnit, 3);
        sceneXml.AppendFormat("<scene position={{{0}}}>\n", Vector3ToJSONString(new Vector3(5, 0, 5)));
        foreach (var rootGO in rootGameObjects)
        {
            sceneXml.Append(RecursivelyGetNodeXml(rootGO.transform, 4));
        }
        AppendIndent(sceneXml, indentUnit, 3);
        sceneXml.Append("</scene>");

        return sceneXml.ToString();
    }

    StringBuilder RecursivelyGetNodeXml(Transform tra, int indentLevel)
    {
        if (!tra.gameObject.activeInHierarchy) return null;

        StringBuilder xmlNode = null;
        StringBuilder xmlNodeTail = null;
        var components = tra.GetComponents<Component>();
        string nodeName = null;
        var position = tra.localPosition;
        var scale = tra.localScale;
        var eulerAngles = tra.localEulerAngles;
        string pColor = null;
        var extraProperties = new StringBuilder();
        foreach (var component in components)
        {
            if (component is Transform) continue;
            if (component is MeshFilter)
            {
                var meshFilter = component as MeshFilter;
                if (meshFilter.sharedMesh == PrimitiveHelper.GetPrimitiveMesh(PrimitiveType.Cube))
                {
                    nodeName = "box";
                }
                else if (meshFilter.sharedMesh == PrimitiveHelper.GetPrimitiveMesh(PrimitiveType.Sphere))
                {
                    nodeName = "sphere";
                }
                else if (meshFilter.sharedMesh == PrimitiveHelper.GetPrimitiveMesh(PrimitiveType.Quad))
                {
                    nodeName = "plane";
                }
                else if (meshFilter.sharedMesh == PrimitiveHelper.GetPrimitiveMesh(PrimitiveType.Cylinder))
                {
                    nodeName = "cylinder";
                    extraProperties.Append(" radius={0.5}");
                }
                if (nodeName != null)
                {
                    //read color
                    var rdrr = tra.GetComponent<MeshRenderer>();
                    if (rdrr)
                    {
                        var matColor = rdrr.sharedMaterial.color;
                        pColor = ToHexString(matColor);
                    }
                }
            }
            if (component is TextMesh)
            {
                nodeName = "text";
                var tm = component as TextMesh;
                extraProperties.AppendFormat(" value=\"{0}\"", tm.text);
                scale *= tm.fontSize * 0.5f;
                //extraProperties.AppendFormat(" fontS=\"{0}\"", 100);
                pColor = ToHexString(tm.color);
                var rdrr = tra.GetComponent<MeshRenderer>();
                if (rdrr)
                {
                    var width = rdrr.bounds.extents.x * 2;
                    extraProperties.AppendFormat(" width=\"{0}\"", width);
                }
            }
        }
        if (nodeName == null)
        {
            nodeName = "entity";
        }
        if (pColor != null)
        {
            extraProperties.AppendFormat(" color=\"{0}\"", pColor);
        }
        xmlNode = new StringBuilder();
        AppendIndent(xmlNode, indentUnit, indentLevel);
        xmlNode.AppendFormat("<{0} position={{{1}}} scale={{{2}}} rotation={{{3}}}{4}>", nodeName, Vector3ToJSONString(position), Vector3ToJSONString(scale), Vector3ToJSONString(eulerAngles), extraProperties);
        xmlNodeTail = new StringBuilder().AppendFormat("</{0}>\n", nodeName);

        var childrenXmls = new List<StringBuilder>();
        foreach (Transform child in tra)
        {
            var childXml = RecursivelyGetNodeXml(child, indentLevel + 1);
            if (childXml != null) childrenXmls.Add(childXml);
        }

        Debug.Log(tra.name);

        if (childrenXmls.Count == 0)
        {
            if (xmlNode == null)
            {
                return null;
            }
            else
            {
                return xmlNode.Append(xmlNodeTail);
            }
        }
        else
        {
            if (xmlNode == null)
            {
                foreach (var childXml in childrenXmls)
                {
                    xmlNode.Append(childXml);
                }
                return xmlNode.Append(xmlNodeTail);
            }
            else
            {
                xmlNode.Append("\n");
                foreach (var childXml in childrenXmls)
                {
                    xmlNode.Append(childXml);
                }
                AppendIndent(xmlNode, indentUnit, indentLevel);
                return xmlNode.Append(xmlNodeTail);
            }
        }
    }

    static StringBuilder AppendIndent(StringBuilder sb, string indentUnit, int indentLevel)
    {
        for (int i = 0; i < indentLevel; i++)
        {
            sb.Append(indentUnit);
        }
        return sb;
    }

    static void CheckDclSceneMetaObject()
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
                if (!go.GetComponent<DclSceneMeta>())
                {
                    go.AddComponent<DclSceneMeta>();
                }
                return;
            }
        }
        //Did not find .dcl object. Create one.
        new GameObject(".dcl", typeof(DclSceneMeta));
    }

    void Export()
    {
        if (string.IsNullOrEmpty(exportPath))
        {
            EditorUtility.DisplayDialog("NO Path!", "You must assign the export path!", null, "OK");
            return;
        }

        var fileTxt = GetSceneFileTemplate();
        var sceneXml = GetSceneXml();
        fileTxt = fileTxt.Replace("{XML}", sceneXml);

        if (!Directory.Exists(exportPath)) Directory.CreateDirectory("exportPath");
        var filePath = Path.Combine(exportPath, "scene.tsx");
        File.WriteAllText(filePath, fileTxt);
    }

    #region Utils

    public static void ParseTextToCoordinates(string text, List<int[]> coordinates)
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
            coordinates.Add(new[] {x, y});
        }
    }

    public static StringBuilder ParcelToStringBuilder(int[] parcel)
    {
        return new StringBuilder().Append(parcel[0]).Append(',').Append(parcel[1]);
    }

    public static StringBuilder WarningToStringBuilder(string warning)
    {
        return new StringBuilder().Append(warning);
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
        var color256 = (Color32)color;
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

    #endregion
}