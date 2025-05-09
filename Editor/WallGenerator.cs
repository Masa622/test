#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class WallGenerator
{
    [MenuItem("GaussianTools/Generate Wall from Selected Markers")]
    public static void GenerateWallFromCenters()
    {


        GameObject[] selected = Selection.gameObjects;
        //GameObject[] markers = GameObject.FindGameObjectsWithTag("CenterMarker");

                if (selected.Length != 4)
                {
                    Debug.LogWarning("❗ 必ず4つのマーカーが必要です（四角形を作るため）");
                    return;
                }

                // 座標リスト取得
                List<Vector3> positions = new List<Vector3>();
                foreach (var m in selected)
                    positions.Add(m.transform.position);

                // 中心を使って時計回りに並べ替え（Z軸上にある前提）
                Vector3 center = Vector3.zero;
                foreach (var pos in positions)
                    center += pos;
                center /= positions.Count;

                positions.Sort((a, b) =>
                {
                    float angleA = Mathf.Atan2(a.z - center.z, a.x - center.x);
                    float angleB = Mathf.Atan2(b.z - center.z, b.x - center.x);
                    return angleA.CompareTo(angleB);
                });

                // 各辺の距離を出力
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 p1 = positions[i];
                    Vector3 p2 = positions[(i + 1) % positions.Count];
                    float distance = Vector3.Distance(p1, p2);
                    Debug.Log($"✅ 点{i}と点{(i + 1) % positions.Count}の距離: {distance:F3} units");
                }

                // 平面を作成
                GameObject quad = new GameObject("Plane");
                var mf = quad.AddComponent<MeshFilter>();
                var mr = quad.AddComponent<MeshRenderer>();

                Mesh mesh = new Mesh();

                mesh.vertices = positions.ToArray();

                // 面を作る順序（時計回り or 反時計回りで定義）
                mesh.triangles = new int[]
                {
                        0, 1, 2,
                        0, 2, 3
                };

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                mf.mesh = mesh;

                // マテリアル
                Material material = new Material(Shader.Find("Standard"));
                material.color = Color.white;
                material.SetFloat("_Glossiness", 0f); // マット感（ツヤなし）
                mr.sharedMaterial = material;

                Debug.Log("✓ 4点から壁を生成しました！");
    }
}

#endif