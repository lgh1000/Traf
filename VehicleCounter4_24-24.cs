//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using System.IO;


////IMPORTANT: for debugging, change loginterval , testing, and change gameDuration time to 300 (5 min); IMPORTANT!
////IMPORTANT: for testing, change loginterval , testing, change filename, and change gameDuration time to 3600 (60 min); IMPORTANT!
////IMPORTANT: for training, change loginterval , testing, change to controllerAgent =!null in void start; change filename, enable writetocsv() and change gameDuration time to 86400 (1 day); IMPORTANT!
//public class VehicleCounter : MonoBehaviour
//{
//    ControllerAgent agent;
//    //public int count = 0;
//    public int NoCarsThruCube = 0;
//    private string csvFilePath; // changed
//    private const int gameDuration = 600;  //changed
//    private bool endgame = false;
//    //TO BE CHANGED FOR TRAINING
//    private float logInterval = 3600f; // This sets the interval to 180 /60 seconds at which time logmetrics() debug prints
//    private bool testing = false;
//    private bool metricsCalculated = false;


//    void Start()
//    {
//        agent = FindObjectOfType<ControllerAgent>();
//        // Create a CSV file in the persistent data path // changed Users/sn/Downloads/Trafficmetrics.csv
//        csvFilePath = "VehicleCounterTrainingCheck.csv";
//        //csvFilePath = "VehicleCounterTestingSeCollInStay30+60sdestroyMoresidecast0after7InColltan37sp<1.csv";
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        //if (Mathf.Round(Time.timeSinceLevelLoad * 10f / 10f) % logInterval == 0 && !testing)
//        if (Time.timeSinceLevelLoad % logInterval == 0 && !testing)

//        {
//            calculateVehiclePassingCount();
//        }
//        else if (testing)
//        {
//            if ((int)(Time.timeSinceLevelLoad) > gameDuration && !endgame && !metricsCalculated)
//            {
//                Debug.Log("Simulation of Vehicle Counter Ended");
//                calculateVehiclePassingCount();
//                endgame = true;
//                metricsCalculated = true;
//            }
//        }
//    }

//    private void OnTriggerEnter(Collider other) //less computation than oncollision enter but neitehr work
//    //private void OnCollisionEnter(Collision collision)
//    //void OnControllerColliderHit(ControllerColliderHit hit)
//    {
//        //if (collision.gameObject.CompareTag("vehicle")) .tag == “CannonBall”
//        // Get the name of the cube this script is attached to

//        //if (other.gameObject.CompareTag("vehicle"))
//        //if (other.gameObject.tag =="vehicle")
//        NoCarsThruCube++;
//        //originally 0.1f
//        agent.AddReward((NoCarsThruCube - 0)*0.01f);
        
//        //count++;


//        // Get the name of the cube this script is attached to
//        //string cubeName = gameObject.name;

//        // Debug log indicating the vehicle crossed this cube
//        //Debug.Log("Vehicle crossed ");

//        //if (count >= 10)
//        //{
//        //    agent.AddReward(1f);//originally 1f
//        //    count = 0;

//        //    // Get the name of the cube this script is attached to
//        //    //string cubeName = gameObject.name;
//        //    //Debug.Log("10 Vehicles Crossed: "+ cubeName);
//        //}
//    }

//    void calculateVehiclePassingCount()
//    {
//        string metricsData = $"{NoCarsThruCube}";
//        // Write metrics data to the CSV file
//        WriteToCSV(metricsData);  //TO CHANGE!!!!
//        endgame = true;

//    }



//    void WriteToCSV(string data)//NO LOOP
//    {
//        // Check if the CSV file exists, if not, create it and write the header
//        if (!File.Exists(csvFilePath))
//        {
//            using (StreamWriter writer = new StreamWriter(csvFilePath))
//            {
//                writer.WriteLine("VehiclesPassingAnIntersection"); //  header
//            }
//        }

//        // Append the data to the CSV file
//        using (StreamWriter writer = new StreamWriter(csvFilePath, true))
//        {
//            writer.WriteLine(data);
//        }
//    }
//}
