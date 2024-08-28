using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hole_Collision : MonoBehaviour
{
    private Detection detectionScript;

    // Start is called before the first frame update
    void Start()
    {
        detectionScript = FindAnyObjectByType<Detection>();
    }

    void OnTriggerEnter(Collider collision)
    {
        if (detectionScript != null)
        {
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
            if (collision.CompareTag("left_step") || collision.CompareTag("right_step"))
            {
                detectionScript.SetHoleCollision(false);
            }
        }
    }

}
