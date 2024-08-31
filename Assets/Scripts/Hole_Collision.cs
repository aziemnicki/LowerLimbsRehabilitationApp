using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hole_Collision : MonoBehaviour
{
    private Detection detectionScript;      // main script to trigger collision

    
    void Start()
    {
        detectionScript = FindAnyObjectByType<Detection>();
    }

    void OnTriggerEnter(Collider collision)
    {
        if (detectionScript != null)
        {
            // both legs will trigger collision with obstacle
            if (collision.CompareTag("left_step") || collision.CompareTag("right_step"))    
            {
                detectionScript.SetHoleCollision(true);
            }
        }
    }
    
    void OnTriggerExit(Collider collision)
    {
        if (detectionScript != null)
        {
            // resets collision status
            if (collision.CompareTag("left_step") || collision.CompareTag("right_step"))    
            {
                detectionScript.SetHoleCollision(false);
            }
        }
    }

}
