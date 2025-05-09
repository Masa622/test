// SPDX-License-Identifier: MIT

using System;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;


namespace GaussianSplatting.Editor
{
    [EditorToolContext("GaussianSplats", typeof(GaussianSplatRenderer)), Icon(k_IconPath)]//この属性継承することでGPUで表示されているGaussianSplatRendererをCPUに運べる
    class GaussianToolContext : EditorToolContext
    {
        const string k_IconPath = "Packages/org.nesnausk.gaussian-splatting/Editor/Icons/GaussianContext.png";

        Vector2 m_MouseStartDragPos;

        protected override Type GetEditorToolType(Tool tool)//move Toolのみ作動してる
        {
            if (tool == Tool.Move)
                return typeof(GaussianMoveTool);
            //if (tool == Tool.Rotate)
            //    return typeof(GaussianRotateTool); // not correctly working yet
            //if (tool == Tool.Scale)
            //    return typeof(GaussianScaleTool); // not working correctly yet when the GS itself has scale
            return null;
        }

        public override void OnWillBeDeactivated()//初期化
        {
            var gs = target as GaussianSplatRenderer;
            if (!gs)
                return;
            gs.EditDeselectAll();
        }

        static void HandleKeyboardCommands(Event evt, GaussianSplatRenderer gs)//選択してるときにキーボードで削除とかする
        {
            if (evt.type != EventType.ValidateCommand && evt.type != EventType.ExecuteCommand)
                return;
            bool execute = evt.type == EventType.ExecuteCommand;
            switch (evt.commandName)
            {
                // ugh, EventCommandNames string constants is internal :(
                case "SoftDelete":
                case "Delete":
                    if (execute)
                    {
                        gs.EditDeleteSelected();
                        GaussianSplatRendererEditor.RepaintAll();
                    }
                    evt.Use();
                    break;
                case "SelectAll":
                    if (execute)
                    {
                        gs.EditSelectAll();
                        GaussianSplatRendererEditor.RepaintAll();
                    }
                    evt.Use();
                    break;
                case "DeselectAll":
                    if (execute)
                    {
                        gs.EditDeselectAll();
                        GaussianSplatRendererEditor.RepaintAll();
                    }
                    evt.Use();
                    break;
                case "InvertSelection"://これ気になるな
                    if (execute)
                    {
                        gs.EditInvertSelection();
                        GaussianSplatRendererEditor.RepaintAll();
                    }
                    evt.Use();
                    break;
            }
        }

        static bool IsViewToolActive()
        {
            return Tools.viewToolActive || Tools.current == Tool.View || (Event.current != null && Event.current.alt);
        }

        public override void OnToolGUI(EditorWindow window)//SceneViewでの描画とマウス操作
        {
            if (!(window is SceneView sceneView))
                return;
            var gs = target as GaussianSplatRenderer;
            if (!gs)
                return;
                
            GaussianSplatRendererEditor.BumpGUICounter();

            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event evt = Event.current;
            HandleKeyboardCommands(evt, gs);
            var evtType = evt.GetTypeForControl(id);
            switch (evtType)
            {
                case EventType.Layout:
                    // make this be the default tool, so that we get focus when user clicks on nothing else
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseDown:
                    if (IsViewToolActive())
                        break;
                    if (HandleUtility.nearestControl == id && evt.button == 0)
                    {
                        // shift/command adds to selection, ctrl removes from selection: if none of these
                        // are present, start a new selection
                        if (!evt.shift && !EditorGUI.actionKey && !evt.control)
                            gs.EditDeselectAll();

                        // record selection state at start
                        gs.EditStoreSelectionMouseDown();
                        GaussianSplatRendererEditor.RepaintAll();

                        GUIUtility.hotControl = id;
                        m_MouseStartDragPos = evt.mousePosition;
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id && evt.button == 0)
                    {
                        Rect rect = FromToRect(m_MouseStartDragPos, evt.mousePosition);
                        Vector2 rectMin = HandleUtility.GUIPointToScreenPixelCoordinate(rect.min);
                        Vector2 rectMax = HandleUtility.GUIPointToScreenPixelCoordinate(rect.max);
                        gs.EditUpdateSelection(rectMin, rectMax, sceneView.camera, evt.control);
                        GaussianSplatRendererEditor.RepaintAll();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && evt.button == 0)
                    {
                        m_MouseStartDragPos = Vector2.zero;
                        GUIUtility.hotControl = 0;
                        evt.Use();

                        // ★ すでに4点記録していたらスキップ
                        //if (GaussianToolContextCenterStore.GetCenters().Count >= 4)
                            //break;


                        if (gs.editSelectedSplats > 0)//////////////////
                        {
                            int kSplatSize = UnsafeUtility.SizeOf<GaussianSplatAssetCreator.InputSplatData>();
                            using var gpuData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gs.splatCount, kSplatSize);
                            if (!gs.EditExportData(gpuData, true))
                            {
                                Debug.LogError("Failed to export splat data.");
                                return;
                            }

                            var data = new GaussianSplatAssetCreator.InputSplatData[gpuData.count];
                            gpuData.GetData(data);

                            // CPU上に選択情報があるっぽい
                            var selectionFlags = gs.GetSelectionFlags();
                            //var selectionFlags = gs.GetType().GetField("m_EditSelectionFlags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(gs) as uint[];
                            if (selectionFlags == null)
                            {
                                Debug.LogError("Couldn't access selection flags.");
                                return;
                            }



                            int printed = 0;
                            for (int i = 0; i < data.Length; ++i)
                            {
                                int wordIdx = i >> 5;
                                int bitIdx = i & 31;
                                bool isSelected = (selectionFlags[wordIdx] & (1u << bitIdx)) != 0;
                                if (!isSelected) continue;

                                var splat = data[i];
                                Debug.Log($"[Selected] Pos: {splat.pos}, Scale: {splat.scale}");
                                if (++printed >= 10) break;
                            }

                            Vector3 center = Vector3.zero;
                            int selectedCount = 0;

                            for (int i = 0; i < data.Length; ++i)
                            {
                                int wordIdx = i >> 5;
                                int bitIdx = i & 31;
                                bool isSelected = (selectionFlags[wordIdx] & (1u << bitIdx)) != 0;
                                if (!isSelected) continue;

                                var splat = data[i];
                                center += splat.pos;
                                selectedCount++;
                            }

                            if (selectedCount > 0)
                            {
                                center /= selectedCount;
                                Debug.Log($"✓ Center of selected {selectedCount} splats: {center}");

                                            
                                // 保存
                                GaussianToolContextCenterStore.AddCenter(center);



                                // 中心点を可視化：赤い小さい球体マーカー
                                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                                marker.name = $"CenterMarker_{GaussianToolContextCenterStore.GetCenters().Count}";
                                marker.tag = "CenterMarker"; // ← これを追加！
                                marker.transform.position = center;
                                marker.transform.localScale = Vector3.one * 0.05f;

                                // 色をつける（例：赤色）
                                var mat = new Material(Shader.Find("Standard"));
                                mat.color = Color.red;
                                marker.GetComponent<Renderer>().sharedMaterial = mat;

                            }

                            
                        }
                    }

                    break;
                case EventType.Repaint:
                    // draw cutout gizmos
                    Handles.color = new Color(1,0,1,0.7f);
                    var prevMatrix = Handles.matrix;
                    foreach (var cutout in gs.m_Cutouts)//空間内のガウス分布をピンク色の円で描画
                    {
                        if (!cutout)
                            continue;
                        Handles.matrix = cutout.transform.localToWorldMatrix;
                        if (cutout.m_Type == GaussianCutout.Type.Ellipsoid)
                        {
                            Handles.DrawWireDisc(Vector3.zero, Vector3.up, 1.0f);
                            Handles.DrawWireDisc(Vector3.zero, Vector3.right, 1.0f);
                            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 1.0f);
                        }
                        if (cutout.m_Type == GaussianCutout.Type.Box)
                            Handles.DrawWireCube(Vector3.zero, Vector3.one * 2);
                    }

                    

                    Handles.matrix = prevMatrix;
                    // draw selection bounding box
                    if (gs.editSelectedSplats > 0)//選択中のスプラットの範囲を描画しboundsでボックス表示にしてる
                    {
                        var selBounds = GaussianSplatRendererEditor.TransformBounds(gs.transform, gs.editSelectedBounds);
                        Handles.DrawWireCube(selBounds.center, selBounds.size);
                    }
                    // draw drag rectangle
                    if (GUIUtility.hotControl == id && evt.mousePosition != m_MouseStartDragPos)//不明
                    {
                        GUIStyle style = "SelectionRect";
                        Handles.BeginGUI();
                        style.Draw(FromToRect(m_MouseStartDragPos, evt.mousePosition), false, false, false, false);
                        Handles.EndGUI();
                    }
                    break;
            }
        }

        // build a rect that always has a positive size
        static Rect FromToRect(Vector2 from, Vector2 to)//不明
        {
            if (from.x > to.x)
                (from.x, to.x) = (to.x, from.x);
            if (from.y > to.y)
                (from.y, to.y) = (to.y, from.y);
            return new Rect(from.x, from.y, to.x - from.x, to.y - from.y);
        }
        
    }
}