#if UNITY_EDITOR
using UnityEditor;

public class WallGenMenu
{
    [MenuItem("GaussianTools/Generate Wall from Centers")]
    public static void GenerateWall()
    {
        WallGenerator.GenerateWallFromCenters();
    }
}
#endif
