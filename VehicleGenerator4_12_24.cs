using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleGenerator : MonoBehaviour
{
    GameObject[] vehicles;
    Vector3[] initialPoses;
    Quaternion[] initialRotations;

    private void Awake()
    {
        vehicles = GameObject.FindGameObjectsWithTag("vehicle");
        initialPoses = new Vector3[vehicles.Length];
        initialRotations = new Quaternion[vehicles.Length];
    }
    private void Start()
    {
        int i = 0;
        foreach (var vehicle in vehicles)
        {
            initialPoses[i++] = vehicle.transform.position;
        }
        i = 0;
        foreach(var vehicle in vehicles)
        {
            initialRotations[i++] = vehicle.transform.rotation;
        }
    }
    public void ResetPoses()
    {
        foreach (var vehicle in vehicles)
        {
            vehicle.GetComponent<TrafficCar>().enabled = false;
        }
        int i = 0;
        foreach(var vehicle in vehicles)
        {
            vehicle.transform.position = initialPoses[i++];
        }
        i= 0;
        foreach(var vehicle in vehicles)
        {
            vehicle.transform.rotation = initialRotations[i++];
        }
        foreach (var vehicle in vehicles)
        {
            vehicle.GetComponent<TrafficCar>().enabled=true;
        }
        Debug.Log("Reset Called");
    }

}