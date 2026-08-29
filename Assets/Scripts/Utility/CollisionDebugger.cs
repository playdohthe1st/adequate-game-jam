using UnityEngine;

public class CollisionDebugger : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D col)
    {
        Debug.Log($"[CollisionDebugger] ENTER: {col.gameObject.name} (layer: {LayerMask.LayerToName(col.gameObject.layer)}, tag: {col.gameObject.tag})");
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        Debug.Log($"[CollisionDebugger] STAY:  {col.gameObject.name} (layer: {LayerMask.LayerToName(col.gameObject.layer)}, tag: {col.gameObject.tag})");
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        Debug.Log($"[CollisionDebugger] EXIT:  {col.gameObject.name} (layer: {LayerMask.LayerToName(col.gameObject.layer)}, tag: {col.gameObject.tag})");
    }
}
