using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class TrafficSystem : MonoBehaviour {


    public GameObject[] IaCars;

    public bool intenseTraffic = true;

    void Awake()
    {
        if (GameObject.Find("RoadMark") && GameObject.Find("RoadMarkRev"))
            InverseCarDirection(true);

        LoadCars(intenseTraffic);
    }

    private void InverseCarDirection(bool actualside)
    {


        GameObject[] roadMark = GameObject.FindObjectsOfType(typeof(GameObject)).Select(g => g as GameObject).Where(g => g.name.Equals("Road-Mark")).ToArray();
        for (int i = 0; i < roadMark.Length; i++)
            roadMark[i].transform.Find("RoadMark").gameObject.SetActive(actualside);

        roadMark = GameObject.FindObjectsOfType(typeof(GameObject)).Select(g => g as GameObject).Where(g => g.name.Equals("Road-Mark-Rev")).ToArray();
        for (int i = 0; i < roadMark.Length; i++)
            roadMark[i].transform.Find("RoadMarkRev").gameObject.SetActive(!actualside);


    }


    public void LoadCars(bool intenseTraffic = true)
    {


        int nVehicles = 0;

        DestroyImmediate(GameObject.Find("CarContainer"));

        Transform CarContainer = new GameObject("CarContainer").transform;

        GameObject carro;

        FCGWaypointsContainer[] ts = GameObject.FindObjectsOfType(typeof(FCGWaypointsContainer)).Select(g => g as FCGWaypointsContainer).ToArray();

        int n = ts.Length;
        Debug.Log(n + " are no. of waypoints");//136, so total vehicles with base loop w intense traffic is 272? times how many ever ext loop
        // so for 50x it is 272 x50=
        for (int loopCount = 0; loopCount < 50; loopCount++)
        {
            for (int i = 0; i < n; i++)
            {
                int randomIndex = Random.Range(0, n);
                FCGWaypointsContainer tsi = ts[randomIndex].GetComponent<FCGWaypointsContainer>();



                carro = (GameObject)Instantiate(IaCars[Mathf.Clamp(Random.Range(0, IaCars.Length), 0, IaCars.Length - 1)], tsi.waypoints[0].transform.position, tsi.waypoints[0].transform.rotation);
                carro.transform.SetParent(CarContainer);

                carro.GetComponent<TrafficCar>().path = ts[i].gameObject;

                nVehicles++;
                //Debug.Log(nVehicles + " innerloop vehicles were instantiated");
                //print(nVehicles + " innerloop vehicles were instantiated");

                if (intenseTraffic == true)
                {

                    if (Vector3.Distance(tsi.waypoints[0].transform.position, tsi.waypoints[1].transform.position) > 50)
                    {


                        carro = (GameObject)Instantiate(IaCars[Mathf.Clamp(Random.Range(0, IaCars.Length), 0, IaCars.Length - 1)], Vector3.Lerp(tsi.waypoints[0].transform.position, tsi.waypoints[1].transform.position, 0.4f), tsi.waypoints[0].transform.rotation);

                        carro.transform.SetParent(CarContainer);

                        carro.GetComponent<TrafficCar>().path = ts[i].gameObject;

                        nVehicles++;

                    }
                }
            }
        }
        

        Debug.Log(nVehicles + " vehicles were instantiated");
        //print(nVehicles + " innerloop vehicles were instantiated");

    }



}

