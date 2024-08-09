using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class collisions : MonoBehaviour
{
    // Start is called before the first frame update
      private Detection detectionScript;
      private int counter_left = 0;
      private int counter_right = 0;
   private bool trigger_delete = false;
    void Start()
    {
      detectionScript = FindAnyObjectByType<Detection>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

     void OnTriggerEnter(Collider collision)
     {
         if (detectionScript != null)
         {
            if (collision.CompareTag("left_step"))
            {
               detectionScript.SetIsCollision(true);
               detectionScript.GetTriggeringObjectName(collision.gameObject.name);
               // Debug.Log("OOOOOOOOOOOOOOOOOOOOO  Entered collision with " + collision.gameObject.name);
            }
            else if (collision.CompareTag("right_step"))
            {
               detectionScript.SetIsCollision(true);
               detectionScript.GetTriggeringObjectName(collision.gameObject.name);
               // Debug.Log("LLLLLLLLLLLLLLLLLLLLLL  Entered collision with " + collision.gameObject.name);
            }
         }
     }

     void OnTriggerStay(Collider collision)
     {
         if (detectionScript != null)
         {

            if (collision.CompareTag("left_step"))
            {
               detectionScript.SetIsCollision(false);
               counter_left += 1;
            }
            else if (collision.CompareTag("right_step"))
            {
               detectionScript.SetIsCollision(false);
               counter_right += 1;
            }

            if ((collision.CompareTag("left_step") && counter_left >= 10) || (collision.CompareTag("right_step") && counter_right >= 10))
            {
               trigger_delete = true;  
            }
            
            if(trigger_delete)
            {
               detectionScript.SetIsCollision(false);
               detectionScript.DeleteOldestStep();
               counter_left = 0;
               counter_right = 0;
               trigger_delete = false;
            }
         }
     }

     void OnTriggerExit(Collider collision)
     {
         if (detectionScript != null)
         {
            detectionScript.SetIsCollision(false);
            counter_left = 0;
            counter_right = 0;
         }
     }
}

