using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;


public class TrafficLights : MonoBehaviour {


    //IMPORTANT: for debugging, change loginterval , testing, and change gameDuration time to 300 (5 min); IMPORTANT!
    //IMPORTANT: for testing, change loginterval , testing, change filename, and change gameDuration time to 3600 (60 min); IMPORTANT!
    //IMPORTANT: for training, change loginterval , testing, change filename, enable writetocsv() and change gameDuration time to 86400 (1 day); IMPORTANT!
    //IMPORTANT: rounding the numbers is messing it up, can always do it post processing
    //IMPORTANT: cannot get trafficlight parameters here as this script is attached to cars and not trafficlight
    private string csvFilePath; // changed
    private const int gameDuration = 600;  //changed
    private float countTime = 0;
    private int step = 0;
    private bool endgameLight=false;
    //TO BE CHANGED FOR TRAINING
    private float lastDebugTime = 0f;
    private float logInterval = 3600f; // This sets the interval to 180 /60 seconds at which time logmetrics() debug prints
    // BUT ALSO REMEBER THAT NEG REWARD FOR TIME STOPPED IS IN LOGINTERVAL CALCULATEMETRICS().
    private bool testing = true;
    private bool metricsCalculated = false;
    public int currentStatus; // Variable to hold the current status changed
    //public float elapsedTime; // Variable to hold the elapsed time changed
    public float step1Duration;
    //public float step2Duration;
    // Static list to store references to all traffic cars
    //private static List<TrafficLights> allTrafficLights = new List<TrafficLights>();
    //public static List<TrafficLights> GetAllTrafficLights()
    //{
    //    return allTrafficLights;
    //}///no need can just do public TrafficLights[] trafficLight; in agent script
    //Debug.Log("allTrafficLights count: "+ allTrafficLights.Count);
    //private float timeAction = 1f; //changed to give 100seconds for each traffic light experiment
    private Dictionary<int, float> statusTimes = new Dictionary<int, float>();
    void InitializeStatusTimers()
    {
        // Add key-value pairs to the dictionary
        statusTimes[13] = 0f; // Initialize value to 0
        statusTimes[31] = 0f;
        statusTimes[14] = 0f;
        statusTimes[41] = 0f;
    }

    //t13, it means the light configuration is for a crossroad where the vertical direction has the right of way.
    //If it selects 31, it means the light configuration is for a crossroad where the horizontal direction has the right of way.

    [System.Serializable]
    public class TrafficLightState
    {
        public int status = 0; // (1 and 4 = RED) , (2 = Yellow) , (3 = Green) // (1 and 4 = RED; 4 is the red after yellow) , (2 = Yellow) , (3 = Green)  so 31 would be green and red combo for that intersection

        public GameObject t31;
        public GameObject t13;
        public GameObject t21;
        public GameObject t12;
        public GameObject t11;


        public GameObject stop31;
        public GameObject stop13;
    }


    public TrafficLightState tState;

    // Reference to the ML-Agents controller agent   changed
    //public ControllerAgent controllerAgent;

    // Use this for initialization

    //void RemoveTrafficLightReferenceInCars()
    //{
    //    // Find all game objects with the "vehicle" tag
    //    GameObject[] vehicles = GameObject.FindGameObjectsWithTag("vehicle");

    //    // Iterate through each vehicle object
    //    foreach (GameObject vehicle in vehicles)
    //    {
    //        // Get the TrafficCar component from the vehicle
    //        TrafficCar trafficCar = vehicle.GetComponent<TrafficCar>();

    //        // If the TrafficCar component is found
    //        if (trafficCar != null)
    //        {
    //            // Remove the reference to the traffic light script
    //            trafficCar.trafficLight = null;
    //        }
    //    }
    //}

    void Start ()
    {
        //controllerAgent = null; //chnaged
        countTime = 0;
        step = 0;
        //step1Duration = 7;
        //step2Duration = 3;

        //allTrafficLights.Add(this);
        //Debug.Log("allTrafficLights count: " + allTrafficLights.Count);
        tState.status = (Random.Range(1, 8) < 4) ? 13 : 31;
        EnabledObjects(tState.status);
        InitializeStatusTimers();
        // Create a CSV file in the persistent data path // changed Users/sn/Downloads/Trafficmetrics.csv
        //csvFilePath = "TrafficLightsOptimization_2Rewards2PPO_879Car4_23_24.csv";
        //csvFilePath = "TrafficLightsTrainingCheck6.csv";
        //csvFilePath = "TrafficLightsTesting11_23_24_260kstepstrained3.csv";
        //PrintUnityPaths(); // Print all paths to see where files could be saved

        // Use persistentDataPath for saving data
        csvFilePath = "/Users/sn/Desktop/TrafficFlowProject/calculated_metrics_baseline30sec_largeCityAvg12_2_24/TrafficLightsTestingLargeCity07_04_25_baseline30_2.csv";

        // csvFilePath = "/Users/sn/Desktop/TrafficFlowProject/calcualted metrics/TrafficLightsTesting11_24_24_260kstepstrained.csv";
        //Debug.Log("CSV will be saved to: " + csvFilePath);
        //csvFilePath = "TrafficLightsTesting11_23_24_baseline3.csv";

        //csvFilePath = "TrafficLightsTRUEBaselineEnv238ObsLRCons_B0.05_R3_4_24_24_1.csv";
        //csvFilePath = "TrafficLightsTRUEBaselineEnv879Step1_7_On_4_28_24.csv";

        InvokeRepeating("Semaforo", Random.Range(0, 4), 1);
        //InvokeRepeating("Semaforo", 0.2f, 1);

    }

    //public void ResetStateL()
    //{
    //    // Reset the car's state to its initial values
    //    // This method should perform any necessary initialization or cleanup
    //    //Start(); // Call Start() to reinitialize the car
    //    //controllerAgent = null; //chnaged
    //    countTime = 0;
    //    step = 0;
    //    step1Duration = 10;
    //    //step2Duration = 3;

    //    //allTrafficLights.Add(this);
    //    //Debug.Log("allTrafficLights count: " + allTrafficLights.Count);
    //    tState.status = (Random.Range(1, 8) < 4) ? 13 : 31;
    //    EnabledObjects(tState.status);
    //}

    private void Semaforo()
    {
        if (Time.realtimeSinceStartup - lastDebugTime >= logInterval && !testing)//timeSinceLevelLoadresets at end of episode
        {
            calculateTrafficLightMetrics();
            lastDebugTime = Time.realtimeSinceStartup;
        }
        else if (testing)
        {
            if ((int)(Time.timeSinceLevelLoad) > gameDuration && !endgameLight && !metricsCalculated)
            {
                Debug.Log("Simulation of Lights Ended");
                calculateTrafficLightMetrics();
                endgameLight = true;
                metricsCalculated = true;
                // Optionally cancel the repeating invoke
                //CancelInvoke("Semaforo");
                //return;//this only stop the current iiteration of the method so if need to cancel the whole then:
                #if UNITY_EDITOR
                                UnityEditor.EditorApplication.isPlaying = false;  // Stops play mode in editor
                #else
                                        Application.Quit();  // Quits the built application
                #endif
            }
            
        }


        countTime += 1;
        

        if (step == 0)
        {
            currentStatus = tState.status;//changed


            //if (countTime > 10) //original
            if (countTime > step1Duration) //changed
            {
                countTime = 0;
                //Debug.Log("step1Duration "+ step1Duration);

                if (statusTimes.ContainsKey(currentStatus))
                {
                    statusTimes[currentStatus] += step1Duration;
                    //Debug.Log("step1Duration in TrafficLightscript: " + step1Duration);
                    //Debug.Log("currentStatus: " + currentStatus + " Timer: " + statusTimes[currentStatus]);
                }

                //if (Time.realtimeSinceStartup - lastDebugTime >= logInterval && !testing)//timeSinceLevelLoadresets at end of episode
                //{
                //    calculateTrafficLightMetrics();
                //    lastDebugTime = Time.realtimeSinceStartup;
                //}
                //else if (testing)
                //{ 
                //    if ((int)(Time.timeSinceLevelLoad) > gameDuration && !endgameLight && !metricsCalculated)
                //    {
                //        Debug.Log("Simulation of Traffic Lights Ended");
                //        calculateTrafficLightMetrics();
                //        endgameLight = true;
                //        metricsCalculated = true;
                //    }   
                //}

                step = 1;

                if (tState.status == 13)
                    tState.status = 12;
                else if (tState.status == 31)
                    tState.status = 21;

                EnabledObjects(tState.status);
            }
        }

        
        else if (step == 1)
        {
            currentStatus = tState.status;

            if (countTime >= 3)
            {
                countTime = 0;
                //if (statusTimes.ContainsKey(currentStatus))
                //{
                //    statusTimes[currentStatus] += 3f;
                //    //Debug.Log("currentStatus: " + currentStatus + " Timer: " + statusTimes[currentStatus]);
                //}

                //if ((int)(Time.timeSinceLevelLoad) > gameDuration)
                //{
                //    Debug.Log("Simulation Ended");
                //    calculateTrafficLightMetrics();
                //}
                //elapsedTime = 3;
                step = 2;

                if (tState.status == 12)
                    tState.status = 41;
                else if (tState.status == 21)
                    tState.status = 14;
                EnabledObjects(tState.status);

            }

        }
        else if (step == 2)//time for pedestrian crossings
        {
            currentStatus = tState.status;

            if (countTime >= 1)//originally 3, but given no pedestrians here , 1 is fine
            //if (countTime >= step2Duration)//changed  //PEDESTRIAN CROSSING TIME(both sides stay in red)
            {
                countTime = 0;
                //if (statusTimes.ContainsKey(currentStatus))
                //{
                //    statusTimes[currentStatus] += step2Duration;
                //    //Debug.Log("currentStatus: " + currentStatus + " Timer: " + statusTimes[currentStatus]);
                //}


                //if (Time.realtimeSinceStartup - lastDebugTime >= logInterval && !testing)//timeSinceLevelLoadresets at end of episode
                //{
                //    calculateTrafficLightMetrics();
                //    lastDebugTime = Time.realtimeSinceStartup;
                //}
                //else if (testing)
                //{
                //    if ((int)(Time.timeSinceLevelLoad) > gameDuration && !endgameLight && !metricsCalculated)
                //    {
                //        Debug.Log("Simulation of Traffic Lights Ended");
                //        calculateTrafficLightMetrics();
                //        endgameLight = true;
                //        metricsCalculated = true;
                //    }
                //}

                step = 0;

                if (tState.status == 14)
                    tState.status = 13;
                else if (tState.status == 41)
                    tState.status = 31;

                EnabledObjects( tState.status);
            }

        }


    }

    
    void EnabledObjects(int habilita)
    {

        tState.t12.SetActive(habilita == 12);
        tState.t21.SetActive(habilita == 21);
        tState.t13.SetActive(habilita == 13);
        tState.t31.SetActive(habilita == 31);
        tState.t11.SetActive(habilita == 11 || habilita == 14 || habilita == 41);

        tState.stop13.SetActive(habilita != 31);
        tState.stop31.SetActive(habilita != 13);



    }

    

    void calculateTrafficLightMetrics()
    {
        //string metricsData = $"{statusTimes[13]},{statusTimes[31]},{statusTimes[14]},{statusTimes[41]}";
        string metricsData = $"{Time.realtimeSinceStartup}, {statusTimes[13]},{statusTimes[31]},{step1Duration}";

        // Write metrics data to the CSV file
        WriteToCSV(metricsData);
        //Debug.Log($"LIGHT Metrics calculated at {Time.timeSinceLevelLoad} seconds");


    }



    void WriteToCSV(string data)//NO LOOP
    {
        // Check if the CSV file exists, if not, create it and write the header
        if (!File.Exists(csvFilePath))
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath))
            {
                writer.WriteLine("RealtimeSinceStartup, RedGrn-RedYll, GrnRed-YllRed, Step1 Light Duration");
                //writer.WriteLine("RedGrn-RedYll, GrnRed-yllRed," +
                //"Red-RedAfterYellowLight14, RedAfterYellow-RedLight41"); //  header
            }
        }

        // Append the data to the CSV file
        using (StreamWriter writer = new StreamWriter(csvFilePath, true))
        {
            writer.WriteLine(data); 
        }
    }

}







// Create a dictionary with integer keys and float values
//private int trafficLightStatus;


//private void Update()
//{
//    //Determine traffic light status:

//    if (statusTimes.ContainsKey(currentStatus))
//    {
//        statusTimes[currentStatus] += Time.deltaTime;
//        Debug.Log("currentStatus: " + currentStatus + " Timer: " + statusTimes[currentStatus]);
//    }

//    if ((int)(Time.timeSinceLevelLoad) > gameDuration)
//    {
//        Debug.Log("Simulation Ended");
//        calculateTrafficLightMetrics();
//        //gameEnded = true;
//        //metricsCalculated = true;
//    }
//}
//ORIGINAL CODE: 
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;


//public class TrafficLights : MonoBehaviour
//{

//    private float countTime = 0;
//    private int step = 0;

//    [System.Serializable]
//    public class TrafficLightState
//    {
//        public int status = 0; // (1 and 4 = RED) , (2 = Yellow) , (3 = Green) 


//        public GameObject t31;
//        public GameObject t13;
//        public GameObject t21;
//        public GameObject t12;
//        public GameObject t11;


//        public GameObject stop31;
//        public GameObject stop13;



//    }


//    public TrafficLightState tState;


//    // Use this for initialization
//    void Start()
//    {

//        countTime = 0;
//        step = 0;

//        tState.status = (Random.Range(1, 8) < 4) ? 13 : 31;
//        EnabledObjects(tState.status);

//        InvokeRepeating("Semaforo", Random.Range(0, 4), 1);


//    }

//    private void Semaforo()
//    {
//        countTime += 1;

//        if (step == 0)
//        {

//            if (countTime > 10)
//            {
//                countTime = 0;
//                step = 1;

//                if (tState.status == 13)
//                    tState.status = 12;
//                else if (tState.status == 31)
//                    tState.status = 21;

//                EnabledObjects(tState.status);

//            }

//        }
//        else if (step == 1)
//        {

//            if (countTime >= 3)
//            {
//                countTime = 0;
//                step = 2;

//                if (tState.status == 12)
//                    tState.status = 41;
//                else if (tState.status == 21)
//                    tState.status = 14;
//                EnabledObjects(tState.status);

//            }

//        }
//        else if (step == 2)
//        {

//            if (countTime >= 3)
//            {
//                countTime = 0;
//                step = 0;

//                if (tState.status == 14)
//                    tState.status = 13;
//                else if (tState.status == 41)
//                    tState.status = 31;

//                EnabledObjects(tState.status);
//            }

//        }


//    }


//    void EnabledObjects(int habilita)
//    {

//        tState.t12.SetActive(habilita == 12);
//        tState.t21.SetActive(habilita == 21);
//        tState.t13.SetActive(habilita == 13);
//        tState.t31.SetActive(habilita == 31);
//        tState.t11.SetActive(habilita == 11 || habilita == 14 || habilita == 41);

//        tState.stop13.SetActive(habilita != 31);
//        tState.stop31.SetActive(habilita != 13);



//    }



//}
//Debug.Log("step1Duration: " + step1Duration);
//Debug.Log("step2Duration: " + step2Duration);
//Debug.Log("Time.timeSinceLevelLoad: " + Time.timeSinceLevelLoad);
// Query ML model for actions



//if (controllerAgent != null)
//{

//    step1Duration = controllerAgent.Cstep1Duration;
//    step2Duration = controllerAgent.Cstep2Duration;
//    //float[] actions = controllerAgent.GetModelActions();
//    //step1Duration = actions[0];
//    //step2Duration = actions[1];
//}
//else
//{
//    // Default durations if no ML agent is controlling
//    step1Duration = 10f;
//    step2Duration = 3f;
//}

