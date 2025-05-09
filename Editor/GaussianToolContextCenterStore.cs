using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GaussianToolContextCenterStore
{
    private static List<Vector3> _centers = new List<Vector3>();

    public static void AddCenter(Vector3 pos)
    {
        _centers.Add(pos);
        Debug.Log($"✓ Center #{_centers.Count} stored: {pos}");
    }

    public static List<Vector3> GetCenters()
    {
        return _centers;
    }

    public static void ClearCenters()
    {
        _centers.Clear();
        Debug.Log("✕ All stored centers cleared");
    }
}