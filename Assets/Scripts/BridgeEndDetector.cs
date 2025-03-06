using UnityEngine;
using System.Collections;

using UnityEngine;

public class BridgeEndDetector : MonoBehaviour
{
    public BridgeController bridgeController;
    private bool hasDetectedCollision = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasDetectedCollision) return;
        
        Debug.Log("BridgeEndDetector phát hiện va chạm với: " + other.name + ", Tag: " + other.tag);
        
        if (other.CompareTag("Pillar"))
        {
            hasDetectedCollision = true;
            
            if (bridgeController != null)
            {
                //bridgeController.NotifyBridgeConnectedToPillar(other.transform);
            }
        }
    }
}