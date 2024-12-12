using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleCounter : MonoBehaviour
{
    ControllerAgent agent;
    public int count = 0;
    void Start()
    {
        agent = GameObject.FindAnyObjectByType<ControllerAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("vehicle"))
        {
            agent.AddReward(0.2f);
            count++;
        }
        if (count >= 10)
        {
            agent.AddReward(1f);
            count = 0;
        }
    }
}
