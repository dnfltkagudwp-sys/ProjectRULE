using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // The GLB chair swap (SwapGuardRoomFurnitureToGlb) copied the FBX chair's exact rotation
    // quaternion over, assuming both exports of the same Generate3D mesh share one internal
    // coordinate convention -- they don't (isolated renders show the GLB chair stands upright at
    // identity rotation just like the FBX one did, so the same non-identity rotation value that
    // kept the FBX chair standing tips the GLB one onto its back/side). Fixes it with a clean,
    // upright-guaranteed Y-only rotation instead of trying to salvage the old value: since Y-axis
    // rotation can never introduce tipping regardless of which mesh convention is in play, this
    // computes a facing angle from the chair's position toward the desk (a reasonable default --
    // "chair near a desk should face it") rather than guessing at the old rotation's intent.
    public static class FixGuardRoomChairUpright
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Graybox/Fix Guard Room Chair Upright")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var desk = GameObject.Find("GuardRoom_Desk");
            var chair = GameObject.Find("GuardRoom_Chair");
            if (desk == null || chair == null)
            {
                Debug.LogError("[FixGuardRoomChairUpright] Could not find GuardRoom_Desk/GuardRoom_Chair.");
                return;
            }

            Vector3 toDesk = desk.transform.position - chair.transform.position;
            toDesk.y = 0f;
            float yaw = Mathf.Atan2(toDesk.x, toDesk.z) * Mathf.Rad2Deg;

            chair.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[FixGuardRoomChairUpright] Chair set to upright, facing the desk (yaw={yaw:F1}). Scene saved.");
        }
    }
}
