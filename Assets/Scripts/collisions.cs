using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class collisions : MonoBehaviour
{
   
   private Detection detectionScript;  // main script to trigger collision
   private int counter_left = 0;       // counts frames of collision with left leg
   private int counter_right = 0;      // counts frames of collision with right leg
   private bool trigger_delete = false;

   
   void Start()
   {
   detectionScript = FindAnyObjectByType<Detection>();
   }

   void OnTriggerEnter(Collider collision)
   {
      if (detectionScript != null)
      {
         if (collision.CompareTag("left_step"))
         {
            detectionScript.SetIsCollision(true);
         }
         else if (collision.CompareTag("right_step"))
         {
            detectionScript.SetIsCollision(true);
         }
      }
   }

   void OnTriggerStay(Collider collision)
   {
      if (detectionScript != null)
      {
         // updates counter, without setting collision
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

         // set bool to true if any leg is in collision for over 10 frames
         if ((collision.CompareTag("left_step") && counter_left >= 10) || (collision.CompareTag("right_step") && counter_right >= 10))
         {
            trigger_delete = true;  
         }
         
         // resets collision state
         if(trigger_delete)     
         {
            detectionScript.SetIsCollision(false);
            // trigger last step delete in main script
            detectionScript.DeleteOldestStep();    
            counter_left = 0;
            counter_right = 0;
            trigger_delete = false; 
         }
      }
   }

   // reset state after exiting collision
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

