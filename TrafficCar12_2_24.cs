using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;

//IMPORTANT: for debugging,testing=true;  loginterval; change gameDuration time to 300 (5 min); IMPORTANT!
//IMPORTANT: for testing,, 2items:  testing=true;change filename ONLY;  and can verify loginterval ; change gameDuration time to 3600 (60 min); IMPORTANT!
//IMPORTANT: for training, 2items:  testing=false ;change filename ONLY; can verify loginterval,  gameDuration 
//IMPORTANT: rounding the numbers is messing it up, can always do it post processing
//IMPORTANT: cannot get trafficlight parameters here as this script is attached to cars and not trafficlight
//IMPORTANT" time stoped and timestoppedduring collision often keep resetting due to a frame of speed>0.5 and may take longer to destroy
// IMPORTANT: time stopped during collisiion of 30-35 sec+ time stoped  at 45 sec respectively are best for destruction.
// former at 30 s is even better. so maybe 30+45 as otherwise cars waiting in line behind collision would keep getting destroyed. longer
//the timestoped, more detructions, but less serious collisions related destructions
//USE "CHANGE BACK" SERACH TERM TO GO BACK TO NN MODEL, OTHER WISE THIS IS TRUE BASELINE
//inactive state(vs destroy on collisions) is messing up all metrics


//Every car GameObject and every traffic light GameObject has its own instance of the ControllerAgent script attached to it,
//    so, each traffic light duration and each car speed is individually controlled by its respective ControllerAgent instance

public class TrafficCar : MonoBehaviour
{
    
    public float timeStoped; //changed
    private string csvFilePath; // changed

    //[HideInInspector]
    public GameObject path;

    //[HideInInspector]
    public GameObject atualWay;


    [HideInInspector]
    public Transform mRayC;

    [HideInInspector]
    public Transform[] wheel;

    public WheelCollider[] wCollider;

    // List to store the game objects
    public List<GameObject> vehicleList = new List<GameObject>();// changed
    //private List<GameObject> EndvehicleList = new List<GameObject>();// changed

    //private List<CarStats> carStatsList = new List<CarStats>();// changed

    // Reference to the ML-Agents controller agent   changed
    //public ControllerAgent controllerAgent;
    //public TrafficLights trafficLight;

    ////Method to remove or reset the reference
    //public void RemoveTrafficLightReference()
    //{
    //    trafficLight = null; // Reset the reference to null
    //}

    public float speedAction = 1.0f; // changed as thsi was causing speedranges beyond 55f, but actually not going above FCG hard limit of 30; it remains at 3.6 for cehicles in unity editor 
    public ControllerAgent agent;//COMMENT OUT FOR TRUE BASELINE, CHANGE BACK! 
    //private Vector3 initialPosition; // Store the initial position of the car
    //public TrafficCarManager trafficCarManager;

    private int countWays;
    public Transform[] nodes;
    public int currentNode = 0;
    private float distance;
    public Vector3 directionToNode;
    private float steer = 0.0f;
    private float offset = 0.75f; //offset is 1 for tan 45; 0.75 for tan 37; // Offset for side raycasts  for tan 30 ( 30 degrees from straight) is 0.577 // changed
    //private float sideRaycastDistance = 2f; // Distance for side raycasts  // changed
    //private float side90offset =0;
    private int sidecastdetect = 2;
    //public List<GameObject> inactiveCars = new List<GameObject>();
    // Static dictionaries accessible from other scripts:
    //public static Dictionary<string, float> nonCollDestroyedCarDict = new Dictionary<string, float>();
    //public static Dictionary<string, float> collDestroyedCarDict = new Dictionary<string, float>();
    // Static dictionaries accessible from other scripts:////works but getting differnet cars each time:
    //public static Dictionary<string, List<float>> nonCollDestroyedCarDict = new Dictionary<string, List<float>>();
    //public static Dictionary<string, List<float>> collDestroyedCarDict = new Dictionary<string, List<float>>();





    public float speed;
    private float brake = 0;
    private float motorTorque = 0;

    private Vector3 steerCurAngle = Vector3.zero;

    private Rigidbody myRigidbody;

    private FCGWaypointsContainer atualWayScript;

    private Vector3 relativeVector;

    public CarWheelsTransform wheelsTransforms;

    private FCGWaypointsContainer fcgWaypointsContainer;
    public float limitSpeed { get; internal set; }
    //public object velocity { get; internal set; }

    [System.Serializable]
    public class CarWheelsTransform
    {

        public Transform frontRight;
        public Transform frontLeft;

        public Transform backRight;
        public Transform backLeft;

        public Transform backRight2;
        public Transform backLeft2;

    }


    public CarSetting carSetting;

    [System.Serializable]
    public class CarSetting
    {

        public bool showNormalGizmos = false;

        public Transform carSteer;

        [Range(10000, 60000)]
        public float springs = 25000.0f;

        [Range(1000, 6000)]
        public float dampers = 1500.0f;

        [Range(60, 200)]
        public float carPower = 120f;

        [Range(5, 10)]//original 5-10
        public float brakePower = 8f;


        [Range(20, 30)]
        public float limitSpeed = 30f;//can change the range right above; 57kph is 35mph

        [Range(30, 72)] //original  [Range(30, 72)]
        public float maxSteerAngle = 35.0f;

    }

    private void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        //TrafficCarManager.AddTrafficCar(gameObject, initialPosition, initialRotation);
    }


    private Vector3 shiftCentre = new Vector3(0.0f, -0.05f, 0.0f);

    private Transform GetTransformWheel(string wheelName)
    {
        GameObject[] wt;

        wt = GameObject.FindObjectsOfType(typeof(GameObject)).Select(g => g as GameObject).Where(g => g.name.Equals(wheelName) && g.transform.parent.root == transform).ToArray();

        if (wt.Length > 0)
            return wt[0].transform;
        else
            return null;

    }


    public void Configure()
    {

        if (!wheelsTransforms.frontRight)
            wheelsTransforms.frontRight = GetTransformWheel("FR");

        if (!wheelsTransforms.frontLeft)
            wheelsTransforms.frontLeft = GetTransformWheel("FL");

        if (!wheelsTransforms.backRight)
            wheelsTransforms.backRight = GetTransformWheel("BR");

        if (!wheelsTransforms.backLeft)
            wheelsTransforms.backLeft = GetTransformWheel("BL");

        if (!wheelsTransforms.backRight2)
            wheelsTransforms.backRight2 = transform.Find("BR2");

        if (!wheelsTransforms.backLeft2)
            wheelsTransforms.backLeft2 = transform.Find("BL2");


        if (!transform.GetComponent<Rigidbody>())
            transform.gameObject.AddComponent<Rigidbody>();

        if (transform.gameObject.GetComponent<Rigidbody>().mass < 4000f)
            transform.gameObject.GetComponent<Rigidbody>().mass = 4000f;


        float p = wheelsTransforms.frontRight.localPosition.z + 0.6f;

        if (!transform.Find("RayC"))
        {
            mRayC = new GameObject("RayC").transform;
            mRayC.SetParent(transform);
            mRayC.localRotation = Quaternion.identity;
            mRayC.localPosition = new Vector3(0f, 0.5f, p);
        }
        else if (!mRayC)
            mRayC = transform.Find("RayC");


        carSetting.maxSteerAngle = (int)Mathf.Clamp(Vector3.Distance(wheelsTransforms.frontRight.transform.position, wheelsTransforms.backRight.transform.position) * 12, 35, 72);


        wheel = new Transform[4];
        wCollider = new WheelCollider[4];


        GameObject center = new GameObject("Center");
        Vector3[] centerPos = new Vector3[4];
        Vector3 nCenter = new Vector3(0, 0, 0);


        wheel[0] = wheelsTransforms.frontRight;
        wheel[1] = wheelsTransforms.frontLeft;
        wheel[2] = wheelsTransforms.backRight;
        wheel[3] = wheelsTransforms.backLeft;

        for (int i = 0; i < 4; i++)
        {
            wCollider[i] = SetWheelComponent(i);
            // Define CenterOfMass
            center.transform.SetParent(wheel[i].transform);
            center.transform.localPosition = new Vector3(0, 0, 0);
            center.transform.SetParent(transform);
            centerPos[i] = center.transform.localPosition -= new Vector3(0, wCollider[i].radius, 0);
            nCenter += centerPos[i];

        }

        shiftCentre = (nCenter / 4);
        DestroyImmediate(center);

    }
    //Static list to store references to all traffic cars
    private static List<TrafficCar> allTrafficCars = new List<TrafficCar>();



    // Start is called before the first frame update


    void Start() //changed
    {
        //Debug.Log("trafficCar script present in start");
        //Check if this traffic car is tagged as a vehicle
        //if (gameObject.CompareTag("vehicle"))
        //{
        //    // Add this traffic car to the list of all traffic cars
        //    allTrafficCars.Add(this);
        //}
        agent = GameObject.FindObjectOfType<ControllerAgent>();//COMMENT OUT FOR TRUE BASELINE, CHANGE BACK! 
        //controllerAgent = null;

        //Find all game objects with the specified tag
        //GameObject[] targetObjects = GameObject.FindGameObjectsWithTag("vehicle");   //changed

        //// Add the game objects to the list
        //vehicleList.AddRange(targetObjects);    //changed
        //Debug.Log("Initial allTrafficCars.Count: " + allTrafficCars.Count);//4/19/24: 879  

        //initialPosition = transform.position;
        if (path)
            Init(path);
        // Store initial state
        //initialPosition = transform.position;
        //initialRotation = transform.rotation;
        //initialLimitSpeed = carSetting.limitSpeed;

        InitializeSpeedRangeTimers();//changed
                                     //InitializeStatusTimers();// changed

        //RemoveTrafficLightReference();

        lastPosition = transform.localPosition; // Initialize last position
        //originalPosition = transform.localPosition;
        // Create a CSV file in the persistent data path // changed Users/sn/Downloads/Trafficmetrics.csv
        //csvFilePath = "TrafficmetricsOptimization_2Rewards2PPO_879Car4_23_24.csv";// will be saved in /Users/sn/Downloads/TrafficManagementMLAgent 2/Trafficmetrics.csv
        //csvFilePath = "TrafficmetricsTesting11_23_24_260kstepstrained3.csv";
        csvFilePath = "/Users/sn/Desktop/TrafficFlowProject/calculated_metrics_baseline30sec_largeCityAvg12_2_24/TrafficMetricsTestingLargeCity07_04_25_baseline30_2.csv";

        // csvFilePath = "/Users/sn/Desktop/TrafficFlowProject/calcualted metrics/TrafficMetricsTesting11_24_24_260kstepstrained.csv";        //csvFilePath = "TrafficmetricsTesting11_23_24_baseline.csv";
        //csvFilePath = "TrafficmetricsTRUEBaselineEnv238ObsLRCons_B0.05_R3_4_24_24_1.csv";
        //csvFilePath = "TrafficmetricsTRUEBaselineEnv238ObsLRCons_B0.05_R3_4_27_24_Light20.csv";
        //csvFilePath = "TrafficCarsTRUEBaselineEnv879Step1_7_On_4_28_24.csv";
        //csvFilePath = "TrafficmetricsTestToTrashSeCollInStay30+60sMoredestroysidecast0after7InColltan37sp<1.csv";
        //csvFilePath = Path.Combine(Application.persistentDataPath, "Trafficmetrics.csv"); // changed
        //error: "/Users/sn/Library/Application Support/DefaultCompany/TrafficManagementMLAgent/Trafficmetrics.csv".

        //InvokeRepeating("RunAgent", Random.Range(0, 4), 100);//100 sec is the interval at which controlleragent changes speed
    }

    //public void ResetStateC()
    //{
    //    // Reset the car's state to its initial values
    //    // This method should perform any necessary initialization or cleanup
    //    //Start(); // Call Start() to reinitialize the car
    //    // Reset position and rotation
    //    //transform.position = initialPosition;
    //    //transform.rotation = initialRotation;

    //    // Reset car's speed
    //    //carSetting.limitSpeed = initialLimitSpeed;
    //    if (path)
    //        Init(path);

    //}
    //OnDestroy is called when the GameObject is destroyed
    //void OnDestroy() //not sure if script will continue working afte car destroyed. so willa dd it right before individual destructions
    //{
    //    // Remove this traffic car from the list of all traffic cars
    //    allTrafficCars.Remove(this);
    //}
    // Method to get all traffic cars
    //public static List<TrafficCar> GetAllTrafficCars()
    //{
    //    return allTrafficCars;
    //}

    //private void WriteToCSV(string v)
    //{
    //    throw new System.NotImplementedException();
    //}

    //private void WriteToCSV(string v)
    //{
    //    throw new System.NotImplementedException();
    //}

    public void Init(GameObject pth)
    {
        path = pth;

        myRigidbody = transform.GetComponent<Rigidbody>();

        myRigidbody.centerOfMass = shiftCentre;


        atualWay = path;
        atualWayScript = atualWay.GetComponent<FCGWaypointsContainer>();


        DefineNewPath();

        currentNode = 1;

        distance = Vector3.Distance(nodes[currentNode].position, transform.position);
        directionToNode = nodes[currentNode].position - transform.position;// gives the direction and distance , a vector

        InvokeRepeating("MoveCar", 0.02f, 0.02f);

    }





    private WheelCollider SetWheelComponent(int w)
    {
        WheelCollider result;

        if (transform.Find(wheel[w].name + " - WheelCollider"))
            DestroyImmediate(transform.Find(wheel[w].name + " - WheelCollider").gameObject);

        GameObject wheelCol = new GameObject(wheel[w].name + " - WheelCollider");

        wheelCol.transform.SetParent(transform);
        wheelCol.transform.position = wheel[w].position;
        wheelCol.transform.eulerAngles = transform.eulerAngles;

        WheelCollider col = (WheelCollider)wheelCol.AddComponent(typeof(WheelCollider));

        result = wheelCol.GetComponent<WheelCollider>();

        JointSpring js = col.suspensionSpring;

        js.spring = carSetting.springs;
        js.damper = carSetting.dampers;
        col.suspensionSpring = js;

        col.suspensionDistance = 0.05f;
        col.radius = (wheel[w].GetComponent<MeshFilter>().sharedMesh.bounds.size.z * wheel[w].transform.localScale.z) * 0.5f;
        col.mass = 1500;

        return result;

    }






    void DefineNewPath()
    {

        nodes = new Transform[atualWay.transform.childCount];
        int n = 0;
        foreach (Transform child in atualWay.transform)
            nodes[n++] = child;

        countWays = nodes.Length;
        currentNode = 0;

    }



    float iRC = 0;

    void MoveCar()
    {

        relativeVector = transform.InverseTransformPoint(nodes[currentNode].position);
        steer = ((relativeVector.x / relativeVector.magnitude) * carSetting.maxSteerAngle);

        speed = myRigidbody.velocity.magnitude * speedAction;

        mRayC.localRotation = Quaternion.Euler(new Vector3(0, steer, 0));//Sets the local rotation of the raycast object (mRayC) based on the current steering angle of the vehicle

        VerificaPoints();




        iRC++;
        if (iRC >= 4) // fixed raycasts are performed every 6 iterations of the MoveCar() method. This might be done to optimize performance or to ensure that the raycasts are not performed too frequently.
        {
            // Perform the forward raycast
            float forwardDistance = FixedRaycasts();

            // Perform the side raycast with a specified offset
            float RsideDistance = SideRaycast(offset);
            float LsideDistance = SideRaycast(-offset);

            // Check which obstacle is closer
            float minDistance = Mathf.Max(forwardDistance, RsideDistance, LsideDistance);
            brake = minDistance;


            // Now 'brake' contains the minimum distance from either raycast
            iRC = 0;

            //if (LsideDistance < RsideDistance)
            //{
            //    // Adjust steering to avoid collision on the left side
            //    steer = 10f;
            //}
            //else if (RsideDistance < LsideDistance)
            //{
            //    // Adjust steering to avoid collision on the right side
            //    steer = -10f;
            //}
            //else
            //{
            //    steer = ((relativeVector.x / relativeVector.magnitude) * carSetting.maxSteerAngle);
            //}
        }



        if (speed < 1)// && !isCollided)BACK UP for serious collisions if not destroyed //changed, removed destroy car // original speed < 1
        {
            timeStoped += Time.fixedDeltaTime;
            if (timeStoped > 7) // this uses consecutive time stopped as if not 0, it resets to timestoped =0; original 60s
            {
                sidecastdetect = 0;
            }
            else
            {
                sidecastdetect = 2;
            }
            if (timeStoped > 60) // this uses consecutive time stopped as if not 0, it resets to timestoped =0; original 60s
            {
                ////Debug.Log(transform.name + " was destroyed");
                nonCollisionDestroyed++;
                agent.AddReward(-1f);//COMMENT OUT FOR TRUE BASELINE, CHANGE BACK! 
                if (testing)
                {
                    CalculateMetrics();
                }
                isDestroyed = true;
                //allTrafficCars.Remove(this);
                // Check if the key already exists in the dictionary//works but getting differnet cars each time
                //if (nonCollDestroyedCarDict.ContainsKey(gameObject.name))
                //{
                //    // Key exists, add the destroyTime to the existing list
                //    nonCollDestroyedCarDict[gameObject.name].Add(Time.timeSinceLevelLoad);
                //}
                //else
                //{
                //    List<float> destroyTimes = new List<float>();
                //    destroyTimes.Add(Time.timeSinceLevelLoad);
                //    nonCollDestroyedCarDict.Add(gameObject.name, destroyTimes);
                //}
                Destroy(transform.gameObject);
                // Set the car inactive instead of destroying it
                //gameObject.SetActive(false);

                // Add the inactive car to the list in TrafficCarManager
                //trafficCarManager.inactiveCars.Add(gameObject);

                //    //RespawnCar();     //changed
                //    // Move the car back to its original position
                //    //transform.localPosition = originalPosition;
                //}
            }
        }
        else
        {
            timeStoped = 0;
            sidecastdetect = 2;
        }

        float bk = 0;

        Quaternion _rot;
        Vector3 _pos;
        //Debug.Log("carSetting.limitSpeed in TrafficCar Script: " + carSetting.limitSpeed);
        for (int k = 0; k < 4; k++)
        {


            if (speed > carSetting.limitSpeed)
                bk = Mathf.Lerp(100, 1000, (speed - carSetting.limitSpeed) / 10);

            if (bk > brake) brake = bk;


            /*
            try
            {
            */

            if (brake == 0)
                wCollider[k].brakeTorque = 0;
            else
            {
                wCollider[k].motorTorque = 0;
                wCollider[k].brakeTorque = carSetting.brakePower * brake;
            }

            /*
            }
            catch (System.Exception e)
            {
                Debug.Log("Error - wheels in " + transform.name);
                Debug.LogException(e, this);
                Destroy(transform.gameObject);
                return;
            }
            */


            if (k < 2)
            {
                motorTorque = Mathf.Lerp(carSetting.carPower * 30, 0, speed / carSetting.limitSpeed);
                wCollider[k].motorTorque = motorTorque;
                wCollider[k].steerAngle = steer;
            }

            wCollider[k].GetWorldPose(out _pos, out _rot);
            wheel[k].position = _pos;
            wheel[k].rotation = _rot;


        }


        if (wheelsTransforms.backRight2)
        {
            wheelsTransforms.backRight2.rotation = wheelsTransforms.backRight.rotation;
            wheelsTransforms.backLeft2.rotation = wheelsTransforms.backRight.rotation;
        }

        //steeringwheel movement
        if (carSetting.carSteer)
            carSetting.carSteer.localEulerAngles = new Vector3(steerCurAngle.x, steerCurAngle.y, steerCurAngle.z - steer);  //carSetting.carSteer.localEulerAngles = new Vector3(steerCurAngle.x, steerCurAngle.y, steerCurAngle.z + ((steer / 180) * -30.0f));

    }



    private void VerificaPoints()
    {

        if (distance < 5) //distance between the vehicle and the current waypoint (distance) is less than 5 units.
        {

            if (currentNode < countWays - 1) //if the current node index (currentNode) is less than the total number of waypoints (countWays) minus 1. If this condition is true, it means there are more waypoints to follow.
                currentNode++;//increments the currentNode index by 1, indicating that the vehicle should move towards the next waypoint.
            else
            {//Randomly selects a new path for the vehicle by choosing a random waypoint from the nextWay array of the current waypoint's FCGWaypointsContainer.
                //Updates the atualWay and atualWayScript variables to reflect the new path.
                atualWay = atualWayScript.nextWay[Random.Range(0, atualWayScript.nextWay.Length)];

                atualWayScript = atualWay.GetComponent<FCGWaypointsContainer>();

                DefineNewPath(); // initializes the new path for the vehicle.

            }

        }

        distance = Vector3.Distance(nodes[currentNode].position, transform.position); //Recalculates the distance between the vehicle and the current waypoint
        directionToNode = nodes[currentNode].position - transform.position;// gives the direction and distance , a vector

    }
    float SideRaycast(float offsetX) // offset would be 0.577 for 30 degree angle (tan30)
    {
        RaycastHit sideHit;
        int sideWdist = sidecastdetect; //temporarily disabling it with 0; Adjust the distance for the side raycast as needed; maximum distance (in Unity units) that the raycast will travel.
        float sideRStop = 0;

        Vector3 sideRayDirection = mRayC.forward + (mRayC.right * offsetX); // Offset the raycast direction
        //Debug.DrawRay(mRayC.position, sideRayDirection * sideWdist, Color.green);

        if (Physics.Raycast(mRayC.position, sideRayDirection, out sideHit, sideWdist))
        {
            //Debug.DrawRay(mRayC.position, sideRayDirection * sideWdist, Color.blue);
            sideRStop = 6000 / sideHit.distance;
        }

        return sideRStop;
    }



    float FixedRaycasts()   //calculates a stopping distance based on the distance to the detected obstacle.
    {

        RaycastHit hit;
        int wdist = 6;// maximum distance (in Unity units) that the raycast will travel.
        float rStop = 0;//stopping distance

        mRayC.localRotation = Quaternion.Euler(new Vector3(0, steer, 0));//Sets the local rotation of the raycast object (mRayC) based on the current steering angle of the vehicle

        //Debug.DrawRay(mRayC.position, mRayC.forward * wdist, Color.yellow);

        if (Physics.Raycast(mRayC.position, mRayC.forward, out hit, wdist)) //Performs a raycast from the position of mRayC in the forward direction. If the raycast hits an object within wdist units, the condition is true, and the details of the hit are stored in the hit variable.
        {

            //Debug.DrawRay(mRayC.position, mRayC.forward * wdist, Color.red);//red debug ray to visualize the raycast hit
            rStop = 6000 / hit.distance;//inversely proportional to the distance to the hit object, so the closer the object, the larger the stopping distance.

        }

        return rStop;
    }
    //changed from now on:


    // Define logInterval as a field
    //TO BE CHANGED FOR TRAINING
    private float lastDebugTime = 0f;//float debugInterval = 3600f; // Debug every hour (3600 seconds)
    private float logInterval = 3600; // This sets the interval to 180 /60 seconds at which time logmetrics() debug prints
    // BUT ALSO REMEBER THAT NEG REWARD FOR TIME STOPPED IS IN LOGINTERVAL CALCULATEMETRICS().
    private bool gameEnded = false;
    private bool testing = true;
    private const int gameDuration = 600;
    public bool isCollided = false;
    public bool isSeriouslyCollided = false;
    private bool metricsCalculated = false;
    public bool isDestroyed = false;
    //private bool collMetricsDone = false;//dont need these as during testing we can end at 10 min and ignore everything after that. and in training
    // training, agent ends episodes in 10 minutes or with first destruction.
    //private bool stoppedMetricsDone = false;


    //private float collisionStartTime = 0f;
    //public int count = 0;
    //public int overallCount = 0;
    private Vector3 lastPosition; // Last recorded position of the car
    public float totalDistance; // Total distance traveled by the car
    private float totalSpeed = 0;
    //private Vector3 originalPosition;
    private static int frames = 0;
    //private static int framesWhenNotStopped = 0;
    //private int addedframe = 0;
    //private float speedAccumulationTimer = 0f;

    // Metrics variables
    //private float totalTimeElapsed; // Total time elapsed in the game
    //VERY IMPORTANT THAT THESE ARE NOT STATIC VARIABLES
    public int nonVehicleCollisionCount = 0;
    public int seriousCollisions = 0;
    //private  int avgtotalCollisionCount = 0;
    //private  int AllVehicleAvgTotalCollisionCount = 0;
    //private  int avgTotalCollisionsPerCar = 0;

    public int vehicleCollisionCount = 0;
    //private  int avgvehicleCollisionCount = 0;
    //private  int AllVehicleAvgvehicleCollisionCount = 0;
    //private  int avgVehicleCollisionsPerCar = 0;

    private float timeStopped = 0f;
    //private float timeNotStopped = 0f;

    //private float totalTimeStopped = 0f;
    //private  float avgtotalTimeStopped = 0f;
    private float RtotalTimeStopped = 0f;
    //private  float AllVehicleAvgTotaltimeStopped = 0f;
    //private  float AllVehicleAvgTotaltimeStoppedperCar = 0f;


    //private  float speedWhenNotStopped = 0f;
    //private  float avgSpeedWhenNotStopped = 0f;
    //private  float AllVehicleAvgSpeedWhenNotStopped = 0f;
    //private  float AllVehicleAvgSpeedWhenNotStoppedperCar = 0f;

    private float totalTimeinminsoFar = 0f;

    public float timeStoppedDuringColl = 0f;
    //private int cubesStops = 0;
    private int nonCollisionDestroyed = 0;
    public int passThroughStop = 0;

    //public static class GlobalInactiveCarsList   ////
    //{
    //    // Define a static list accessible from any script
    //    public static List<GameObject> inactiveCars = new List<GameObject>();
    //}


    //// Variables for AvgAllVehicle metrics
    //private float AvgAllVehicletimeStopped = 0f;
    //private float AvgAllVehiclewtdAvgSpeedWhenNotStopped = 0f;
    //private int numVehicles = 0;

    private const int speedRangeInterval = 5; // Interval for speed ranges
    private Dictionary<string, float> speedRangeTimers = new Dictionary<string, float>(); // for each vehicle    
    //private Dictionary<string, float> totalSpeedRangeTimers = new Dictionary<string, float>(); // all vehicles
    public float congestionTimer = 0f;
    public Vector3 initialPosition;
    public Quaternion initialRotation;
    //private float initialLimitSpeed;

    // Method to initialize speed range timers for this vehicle
    void InitializeSpeedRangeTimers()
    {
        //speedRangeTimers.Clear(); // Clear existing data if any
        for (int i = 0; i < 7; i++) // 11 speed ranges: 0-5, 5-10, ..., 50-55 (7) other wise go to 60-65 using 13
        {
            string rangeKey = "speed" + (i * speedRangeInterval) + "-" + ((i + 1) * speedRangeInterval);
            speedRangeTimers[rangeKey] = 0;
            //totalSpeedRangeTimers[rangeKey] = 0;
        }
    }


    // Collision detection method for cars
    private void OnCollisionEnter(Collision collision)//private void collision exit not working
    {
        GameObject other = collision.gameObject;
        // Check if collided object is a vehicle
        if (!isCollided)
        {
            if (other.CompareTag("vehicle"))
            {
                //collisionStartTime = Time.timeSinceLevelLoad;
                vehicleCollisionCount++;
                agent.AddReward(-0.01f);//COMMENT OUT FOR TRUE BASELINE, CHANGE BACK! 
                isCollided = true; // Set flag to prevent multiple counting
            }
            //else if (other.CompareTag("cube"))
            //{
            //    agent.AddReward(0.2f);
            //    count++;
            //    overallCount++;

            //    // Get the name of the cube this script is attached to
            //    //string cubeName = gameObject.name;

            //    // Debug log indicating the vehicle crossed this cube
            //    Debug.Log("Vehicle crossed ");
            //    isCollided = true; // Set flag to prevent multiple counting
            //}
            //else if (other.CompareTag("cube") || other.CompareTag("Stop")) // this was 0; will include? all the vehicles collisions with the virtual traffic intersection vehicle counters, or stop13 and 31 colliders in traffic lights

            //{
            //    cubesStops++;
            //}
            else
            {
                // Increment collision count for this car
                //collisionStartTime = Time.timeSinceLevelLoad;
                nonVehicleCollisionCount++;
                //Debug.Log("non-vehicle collision by:"+ gameObject.name +" "+path);
                agent.AddReward(-0.01f); //CHANGED FOR TRUE BASELINE, CHANGE BACK!
                isCollided = true; // Set flag to prevent multiple counting}
            }
        }
    }
    private void OnCollisionStay(Collision collision)
    {
        if (speed < 1)
        {
            timeStoppedDuringColl += Time.fixedDeltaTime;
        }
        else
        {
            timeStoppedDuringColl = 0;
        }

        if (timeStoppedDuringColl > 7)
        {
            sidecastdetect = 0;
        }
        else
        {
            sidecastdetect = 2;
        }

        if (!isSeriouslyCollided && timeStoppedDuringColl > 30f) // if this code is in oncollisionstay, it overcounts; // if this code is in oncollisionExit, it overcounts
        {

            seriousCollisions++;
            agent.AddReward(-1f);//COMMENT OUT FOR TRUE BASELINE, CHANGE BACK! 
            isSeriouslyCollided = true;
            if (testing)
            {
                CalculateMetrics(); //as otherwise these serious coll are not recorded as car gets destroyed.
            }
            isDestroyed = true;
            //allTrafficCars.Remove(this);////works but getting differnet cars destroyed each time
            //if (collDestroyedCarDict.ContainsKey(gameObject.name))
            //{
            //    // Key exists, add the destroyTime to the existing list
            //    collDestroyedCarDict[gameObject.name].Add(Time.timeSinceLevelLoad);
            //}
            //else
            //{
            //    List<float> destroyTimes = new List<float>();
            //    destroyTimes.Add(Time.timeSinceLevelLoad);
            //    collDestroyedCarDict.Add(gameObject.name, destroyTimes);
            //}
            Destroy(transform.gameObject);//destroy vehicle
            // Set the car inactive instead of destroying it
            //gameObject.SetActive(false);

            // Add the inactive car to the list in TrafficCarManager
            //trafficCarManager.inactiveCars.Add(gameObject);

            //RespawnCar();
            //agent.EndEpisode(); //REMOVE AS NEEDED

        }
        //else if (isSeriouslyCollided && speed < 1 && (Time.timeSinceLevelLoad - collisionStartTime) > 60)
        //{
        //    Destroy(transform.gameObject);//destroy vehicle 
        //    isCollided = false;
        //    isSeriouslyCollided = false;
        //}

    }
    private void OnCollisionExit(Collision collision)
    {

        // Reset collision handling flag when exiting collision
        isCollided = false;
        isSeriouslyCollided = false;

    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Stop"))
        {
            passThroughStop++;
            agent.AddReward(0.01f);// typical total sums of all vehicles after 10 min baseline is 485,//COMMENT OUT FOR TRUE BASELINE, CHANGE BACK! 
            //ranging from 0 mostly to 12 for individual vehicles, incentivized to decrease no. o fdestroyed vehicles
            // Increment cube collision count or perform other actions
        }

    }

    //    else if (isCollided && !isSeriouslyCollided && speed <1)
    //    {
    //        if ((Time.timeSinceLevelLoad - collisionStartTime) > 30)
    //        {
    //            seriousCollisions++;
    //            agent.AddReward(-1.0f);
    //            agent.EndEpisode();
    //            isSeriouslyCollided = true;
    //        }
    //        else if ((Time.timeSinceLevelLoad - collisionStartTime) > 60)
    //        {
    //            Destroy(transform.gameObject);
    //        }

    //    }
    //}


    //            // Check if collision handling is already done
    //            if (isCollided)
    //        return;

    //    // Check if collided object is a vehicle
    //    if (other.CompareTag("vehicle"))
    //    {
    //        // Increment vehicle collision count for this car
    //        vehicleCollisionCount++;
    //        agent.AddReward(-0.5f);
    //        isCollided = true; // Set flag to prevent multiple counting
    //        agent.EndEpisode();
    //    }
    //    else   // this will count all the vehicles collisions with the virtual traffic intersection vehicle counters
    //    {
    //        // Increment collision count for this car
    //        nonVehicleCollisionCount++;
    //        agent.AddReward(-0.1f);
    //        isCollided = true; // Set flag to prevent multiple counting
    //        //LogMetrics();
    //    }
    //}
    //int frameCount = 0;



    //internal float limitSpeed;

    void Update() //No LOOP
    {
        //EACH FRAME IS 0.0005597 SEC
        //Debug.Log("Update() called.");// to check how many times update is being called per frame
        //Debug.Log("total frames: "+frames+" time so far: " + Time.timeSinceLevelLoad);// to check how many times update is being called per frame
        //float deltaTime = Time.deltaTime; // Get the time elapsed since the last frame, this says it is 0.333sec but each frame is actually 0.0006sec
        //Debug.Log("deltatime: " + deltaTime);always 0.333

        //Debug.Log("Initial allTrafficCars.Count: " + allTrafficCars.Count);
        //CALCUALTE VALUES EACH FRAME for this CAR
        // Trim float values to 2 decimal places

        if (speed < 1)
        {
            //timeStopped += 0.0006f;
            timeStopped += Time.deltaTime;
        }

        // Determine the speed range for the current speed
        int speedRange = (int)(speed / speedRangeInterval) * speedRangeInterval;
        string rangeKey = "speed" + speedRange + "-" + (speedRange + speedRangeInterval);
        //Debug.Log("speed: " + speed + "timeStoped: " + timeStoped + "rangekey: " + rangeKey);
        // Increment the timer for the corresponding speed range
        //Debug.Log(speed);


        if (speedRangeTimers.ContainsKey(rangeKey))
        {
            //speedRangeTimers[rangeKey] += 0.0006f;
            speedRangeTimers[rangeKey] += Time.deltaTime;
            //Debug.Log(rangeKey + " speed: " + speed + " timer: " + speedRangeTimers[rangeKey]);
        }

        //congestionTimer = speedRangeTimers["speed0-5"];
        //if (congestionTimer > 7f)
        //{
        //    // Calculate the increase in negative reward
        //    float points = (float)((congestionTimer - 7f) * -0.05);

        //    // Apply the negative reward to the agent
        //    agent.AddReward(points);
        //}
        // Calculate the distance traveled since the last frame
        float distanceTraveled = Vector3.Distance(transform.localPosition, lastPosition);
        // Add the distance traveled to the total distance
        totalDistance += distanceTraveled;
        // Update the last position to the current position
        lastPosition = transform.localPosition;

        totalSpeed += speed;


        //REWARDS 3   //COMMENT OUT ALL OF REWARDS3 FOR TRUE BASELINE, CHANGE BACK! 
        //float dist = (((totalDistance / 1000) - 0) * 1e-5f);//FOR KM
        agent.AddReward((totalDistance - 0) * 1e-8f); // TO_3
        agent.AddReward((timeStopped - 0f) * -1e-5f);//TO_3
        agent.AddReward((speedRangeTimers["speed25-30"] - 0) * 1e-5f);//TO_3
        /////??//////////////////////////////////
        //frameCount++;
        //if (frameCount>10)// overall reward per 10 min episode from 8 rewards in baseline 30:60 environment is around 0.92.
        //{
        //    //agent.AddReward((1 - (speedRangeTimers["speed0-5"] / Time.timeSinceLevelLoad)) * 0.75f); // not getting many rewards here b/c 1-1*0.75 //TO_0
        //    agent.AddReward((timeStopped - 0f) * -0.1f);//TO_2
        //    agent.AddReward((speedRangeTimers["speed25-30"]-0) * 0.09f);//TO_2
        //    //float diatanceIncrease = (float)((totalDistance - 1f) * 0.0002f);//TO_0
        //    agent.AddReward((totalDistance - 0f) * 0.02f); //TO_2
        //    frameCount = 0;
        //}

        //// Calculate the time since the collision started
        //float collisionDuration = Time.timeSinceLevelLoad - collisionStartTime;

        //// Check if the car has collided and the collision duration is greater than 30 seconds
        //if (isSeriouslyCollided && collisionDuration > 30)
        //{
        //    // Destroy the current car GameObject
        //    Destroy(gameObject);

        //    // Respawn the car at a new position
        //    RespawnCar();
        //}

        //every frame is 0.0003sec
        //float lastDebugTime = 0f;
        ////float debugInterval = 3600f; // Debug every hour (3600 seconds)
        //private float logInterval = 3600;
        //if (Time.realtimeSinceStartup - lastDebugTime >= debugInterval)
        //{
        //    Debug.Log("Debugging at " + Time.realtimeSinceStartup);
        //    // Add your debugging logic here

        //    lastDebugTime = Time.realtimeSinceStartup;
        //}
        //if (Mathf.Round(Time.timeSinceLevelLoad * 10f / 10f) % logInterval == 0.0 && !testing)
        if (Time.realtimeSinceStartup - lastDebugTime >= logInterval && !testing)//timeSinceLevelLoadresets at end of episode
        {
            CalculateMetrics();
            lastDebugTime = Time.realtimeSinceStartup;
        }

        else if (testing)
        {
            if ((int)(Time.timeSinceLevelLoad) > gameDuration && !gameEnded && !metricsCalculated)
            {
                Debug.Log("Simulation of Cars Ended");
                CalculateMetrics();
                gameEnded = true;
                metricsCalculated = true;
                //return; //this only stop the current iiteration of the method so if need to cancel the whole then:
                #if UNITY_EDITOR
                                UnityEditor.EditorApplication.isPlaying = false;  // Stops play mode in editor
                #else
                        Application.Quit();  // Quits the built application
                #endif

                //GameObject[] targetObjects = GameObject.FindGameObjectsWithTag("vehicle");   //changed

                //// Add the game objects to the list
                //EndvehicleList.AddRange(targetObjects);    //changed
                //Debug.Log("End of testing vehicle list count: " + EndvehicleList);
            }
        }
        frames++;
        //if ((int)(Time.timeSinceLevelLoad) > gameDuration && !gameEnded && !metricsCalculated && testing)
        //{
        //    Debug.Log("Simulation Ended");
        //    CalculateMetrics();
        //    gameEnded = true;
        //    metricsCalculated = true;
    }





    void CalculateMetrics()  //NO LOOP
    {
        //Debug.Log("vehicleList.Count: " + vehicleList.Count);
        totalTimeinminsoFar = Time.timeSinceLevelLoad / 60;
        //avgSpeedWhenNotStopped = speedWhenNotStopped / addedframe;SPEED CALCUALTION INTERVAL IS EXTREMELY ERRATIC AND INCONSISTENT, SO CANNOT DIVED BY ANYTHING RATIONALT TO GET AVERAGE SPEED
        //Debug.Log("avgSpeedWhenNotStopped: " + avgSpeedWhenNotStopped);
        RtotalTimeStopped = timeStopped / 60;//in min
        totalSpeed = totalSpeed / frames;

        //if (RtotalTimeStopped <0.1)//== 0.0003333333)
        //{
        //    Debug.Log("RtotalTimeStopped ==0.0003333333 is from vehicle: " + gameObject.name + " " +path);
        //}
        //if (RtotalTimeStopped >7)
        //{
        //    Debug.Log("RtotalTimeStopped >7min: " + gameObject.name + " " + path);
        //}

        //if (totalTimeinminsoFar <1)
        //{
        //    Debug.Log("Ended at a short time of : " + totalTimeinminsoFar +" "+ gameObject.name + " " + path);
        //}





        //    Debug.Log("Total time for speedrange per Car (min): " + totalSpeedRangeTimers[key] + ": " + totalAvgTimerperCar + " for totaltime(min) of: " + totalTimeinminsoFar);
        //    Debug.Log("Total time % for speedrange per Car: " + totalSpeedRangeTimers[key] + ": " + (totalAvgTimerperCar / totalTimeinminsoFar) * 100 + " for totaltime(min) of: " + totalTimeinminsoFar);

        //}

        //Debug.Log("Average Time Stopped per Car (min): " + AllVehicleAvgTotaltimeStoppedperCar + " for totaltime(min) of: " + totalTimeinminsoFar);
        //Debug.Log("Average Speed per Car when not stopped(mph): " + AllVehicleAvgSpeedWhenNotStoppedperCar + " for totaltime(min) of: " + totalTimeinminsoFar);
        //Debug.Log("vehicle-Vehicle CollisionCount: " + vehicleCollisionCount + " for totaltime(min) of: " + totalTimeinminsoFar);
        //Debug.Log("Non-Vehicle CollisionCount: " + nonVehicleCollisionCount + " for totaltime(min) of: " + totalTimeinminsoFar);


        // Format the metrics data
        string metricsData = $"{totalTimeinminsoFar},{Time.realtimeSinceStartup}, {RtotalTimeStopped},{totalDistance}, {totalSpeed}, {carSetting.limitSpeed}, {seriousCollisions},{vehicleCollisionCount}," +
    $"{nonVehicleCollisionCount},{nonCollisionDestroyed},{passThroughStop},{speedRangeTimers["speed0-5"] / 60}, {speedRangeTimers["speed5-10"] / 60}, {speedRangeTimers["speed10-15"] / 60}," +
    $"{speedRangeTimers["speed15-20"] / 60}, {speedRangeTimers["speed20-25"] / 60}, {speedRangeTimers["speed25-30"] / 60}, {speedRangeTimers["speed30-35"] / 60}";// +
                                                                                                                                                                  // $"{speedRangeTimers["speed35-40"] / 60}, {speedRangeTimers["speed40-45"] / 60}, {speedRangeTimers["speed45-50"] / 60}, {speedRangeTimers["speed50-55"] / 60},"+
                                                                                                                                                                  //$"{speedRangeTimers["speed55-60"] / 60},{ speedRangeTimers["speed60-65"] / 60}";

        // Write metrics data to the CSV file
        WriteToCSV(metricsData);  //TO CHANGE!!!!



        //Debug.Log("avgVehicleCollisionsPerCar: " + avgVehicleCollisionsPerCar);
        //Debug.Log("AllVehicleAvgTotalCollisionCount: " + AllVehicleAvgTotalCollisionCount);


    }
    void WriteToCSV(string data)//NO LOOP
    {
        // Check if the CSV file exists, if not, create it and write the header
        if (!File.Exists(csvFilePath))
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath))//  header
            {
                writer.WriteLine("totalTimeinminsoFar, realtimeSinceStartup, Total Time Stopped, Total Distance Travelled, Avg Speed, Speed Limit, Total Serious Collision Count," +
                "Total Vehicle-Vehicle Collision Count,Total Non-Vehicle Collision Count, Non-Collision Destroyed, No. Of Stops Passed Through, Avg Time in speed0-5mph,Avg Time in speed5-10mph,Avg Time in speed10-15mph," +
                "Avg Time in speed15-20mph, Avg Time in speed20-25mph, Avg Time in speed25-30mph, Avg Time in speed30-35mph");// +
                //"Avg Time in speed35-40mph, Avg Time in speed40-45mph, Avg Time in speed45-50mph, Avg Time in speed50-55mph, Avg Time in speed55-60mph,"+
                //"Avg Time in speed60-65mph"); 
            }
        }

        // Append the data to the CSV file
        using (StreamWriter writer = new StreamWriter(csvFilePath, true))
        {
            writer.WriteLine(data); // totalTimeinminsoFar, AllVehicleAvgTotaltimeStoppedperCar, AllVehicleAvgSpeedWhenNotStoppedperCar,
                                    //vehicleCollisionCount, nonVehicleCollisionCount);
                                    //writer.WriteLine(totalTimeinminsoFar, AllVehicleAvgTotaltimeStoppedperCar, AllVehicleAvgSpeedWhenNotStoppedperCar,
                                    //    vehicleCollisionCount, nonVehicleCollisionCount);
        }
    }


}




// Call this method to exit play mode
//public static void ExitPlayMode()
//{
//    EditorApplication.isPlaying = false;
//}

//void RespawnCar()
//{
//    // Find a new position to respawn the car
//    //Vector3 respawnPosition = initialPosition;

//    // Instantiate a new instance of the car GameObject at the respawn position
//    GameObject newCar = Instantiate(gameObject, initialPosition, Quaternion.identity);

//    // Initialize the new car
//    TrafficCar trafficCar = newCar.GetComponent<TrafficCar>();
//    trafficCar.Init(path); // Initialize the car with the path

//    // Reset collision flags and timers for the new car
//    isCollided = false;
//    isSeriouslyCollided = false;
//    collisionStartTime = 0f;
//}
//private void RunAgent()
//{
//    if (controllerAgent != null)
//    {
//        speed = controllerAgent.Cspeed;
//    }
//}

//void addSpeed()
//{ speedWhenNotStopped += speed; }



//void Update()
//{
//    //EACH FRAME IS 0.0005597 SEC
//    //Debug.Log("Update() called.");// to check how many times update is being called per frame
//    //Debug.Log("total frames: "+frames+" time so far: " + Time.timeSinceLevelLoad);// to check how many times update is being called per frame
//    //float deltaTime = Time.deltaTime; // Get the time elapsed since the last frame, this says it is 0.333sec but each frame is actually 0.0006sec
//    //Debug.Log("deltatime: " + deltaTime);always 0.333

//    // Calculate metrics for each vehicle
//    foreach (GameObject vehicle in vehicleList)
//    {
//        //CALCUALTE VALUES EACH FRAME PER CAR
//        // Trim float values to 2 decimal places

//        if (speed < 1)
//        {
//            timeStopped += 0.0006f;
//        }
//        totalTimeStopped += timeStoped;
//        //timeStopped = Mathf.Round(timeStopped * 100) / 100;

//        speed = Mathf.Round(speed * 100) / 100;
//        if (speed > 1)
//        {
//            speedWhenNotStopped += speed;
//            //wtdAvgSpeedWhenNotStopped += (speed * (1 - (timeStoped / deltaTime)));
//        }


//        // totalCollisions += totalCollisionCount;
//        // vehicleCollisions += vehicleCollisionCount;

//        // Determine the speed range for the current speed
//        int speedRange = (int)(speed / speedRangeInterval) * speedRangeInterval;
//        string rangeKey = "speed" + speedRange + "-" + (speedRange + speedRangeInterval);
//        //Debug.Log("speed: " + speed + "timeStoped: " + timeStoped + "rangekey: " + rangeKey);
//        // Increment the timer for the corresponding speed range
//        if (speedRangeTimers.ContainsKey(rangeKey))
//        {
//            speedRangeTimers[rangeKey] += 0.0006f;
//        }
//    }
//    frames++;
//    //// Reset collisionHandled flag at the end of frame
//    collisionHandled = false;
//    //3min in real time is 60sec for unity; every frame is 0.3sec
//    //if ((Mathf.Round(Time.timeSinceLevelLoad*10f/10f)) % logInterval == 0 && !metricsCalculated) // logInterval could be a variable indicating time interval
//    //{
//    //    CalculateMetrics();
//    //    metricsCalculated = true;
//    //}
//    if ((int)(Time.timeSinceLevelLoad) > gameDuration && !gameEnded && !metricsCalculated)
//    {
//        EndGame();
//        gameEnded = true;
//        metricsCalculated = true;
//    }

//}



//void CalculateMetrics()
//{

//    //CALCUALTE AVERAGE METRICS PER CAR
//    // Calculate metrics for each vehicle
//    foreach (GameObject vehicle in vehicleList)
//    {
//        avgSpeedWhenNotStopped = Mathf.Round(speedWhenNotStopped / frames) * 100 / 100;
//        avgtotalTimeStopped = Mathf.Round(totalTimeStopped / frames) * 100 / 100;
//        //avgvehicleCollisionCount = vehicleCollisionCount / frames;
//        //avgtotalCollisionCount = totalCollisionCount / frames;
//        //Calculate and log the average speed range timers
//        foreach (var kvp in speedRangeTimers)
//        {
//            string speedRange = kvp.Key;
//            float timer = kvp.Value;
//            //float avgTimer = timer / (Time.timeSinceLevelLoad+1); // frames; // Calculate the average timer for the speed range
//            // Add car's average statistics to total averages
//            if (totalSpeedRangeTimers.ContainsKey(speedRange))
//            {
//                totalSpeedRangeTimers[speedRange] += timer;// kvp.Value;
//            }
//            //float avgTimerPerCar = avgTimer / carStatsList.Count; // Divide by total cars
//            //Debug.Log($"Average Time for Speed Range {speedRange} per Car: {avgTimerPerCar}");
//        }

//        //add to total cars
//        AllVehicleAvgSpeedWhenNotStopped += avgSpeedWhenNotStopped;
//        AllVehicleAvgTotaltimeStopped += avgtotalTimeStopped;
//        //AllVehicleAvgvehicleCollisionCount += avgvehicleCollisionCount;
//        //AllVehicleAvgTotalCollisionCount += avgtotalCollisionCount;
//    }


//    // Calculate overall average statistics per Car
//    int totalNoOfCars = vehicleList.Count;
//    AllVehicleAvgTotaltimeStoppedperCar = (Mathf.Round((AllVehicleAvgTotaltimeStopped / totalNoOfCars / 60) * 100) / 100);
//    AllVehicleAvgSpeedWhenNotStoppedperCar = (Mathf.Round((AllVehicleAvgSpeedWhenNotStopped / totalNoOfCars) * 100) / 100);
//    //avgVehicleCollisionsPerCar = AllVehicleAvgvehicleCollisionCount / totalNoOfCars;
//    //avgTotalCollisionsPerCar = AllVehicleAvgTotalCollisionCount / totalNoOfCars;
//    // Calculate and log the average speed range timers

//    totalTimeinminsoFar = (Mathf.Round((Time.timeSinceLevelLoad / 60) * 100) / 100);
//    // Create a copy of the keys in the dictionary
//    var keys = totalSpeedRangeTimers.Keys.ToList();

//    foreach (var key in keys)
//    {
//        // Retrieve the value associated with the key
//        float totalTimer = totalSpeedRangeTimers[key];
//        float totalAvgTimerperCar = (Mathf.Round((totalTimer / totalNoOfCars / 60) * 100) / 100); // Calculate the average timer for the speed range
//                                                                                                  // Update the value in the dictionary with the calculated average timer per car
//        totalSpeedRangeTimers[key] = totalAvgTimerperCar;
//        Debug.Log("Total time for speedrange per Car (min): " + totalSpeedRangeTimers[key] + ": " + totalAvgTimerperCar + " for totaltime(min) of: " + totalTimeinminsoFar);
//        Debug.Log("Total time % for speedrange per Car: " + totalSpeedRangeTimers[key] + ": " + (totalAvgTimerperCar / totalTimeinminsoFar) * 100 + " for totaltime(min) of: " + totalTimeinminsoFar);

//    }

//    Debug.Log("Average Time Stopped per Car (min): " + AllVehicleAvgTotaltimeStoppedperCar + " for totaltime(min) of: " + totalTimeinminsoFar);
//    Debug.Log("Average Speed per Car when not stopped(mph): " + AllVehicleAvgSpeedWhenNotStoppedperCar + " for totaltime(min) of: " + totalTimeinminsoFar);
//    Debug.Log("vehicle-Vehicle CollisionCount: " + vehicleCollisionCount + " for totaltime(min) of: " + totalTimeinminsoFar);
//    Debug.Log("Non-Vehicle CollisionCount: " + nonVehicleCollisionCount + " for totaltime(min) of: " + totalTimeinminsoFar);


//    // Format the metrics data
//    string metricsData = $"{totalTimeinminsoFar},{AllVehicleAvgTotaltimeStoppedperCar},{AllVehicleAvgSpeedWhenNotStoppedperCar},{vehicleCollisionCount}," +
//        $"{nonVehicleCollisionCount},{totalSpeedRangeTimers["speed0-5"]}, {totalSpeedRangeTimers["speed5-10"]}, {totalSpeedRangeTimers["speed10-15"]}," +
//        $"{totalSpeedRangeTimers["speed15-20"]}, {totalSpeedRangeTimers["speed20-25"]}, {totalSpeedRangeTimers["speed25-30"]}, {totalSpeedRangeTimers["speed30-35"]}";

//    // Write metrics data to the CSV file
//    //WriteToCSV(metricsData);



//    //Debug.Log("avgVehicleCollisionsPerCar: " + avgVehicleCollisionsPerCar);
//    //Debug.Log("AllVehicleAvgTotalCollisionCount: " + AllVehicleAvgTotalCollisionCount);

//    //negative reward for too much time stopped/slowed by any vehicle
//    //MAY HAVE TO PUT IT IN PER FRAME IF TIME STOPPED PER CAR IS NOT IMPROVING
//    if (totalSpeedRangeTimers["speed5-10"] > 7f)
//    {
//        // Calculate the increase in negative reward
//        float increase = (float)((totalSpeedRangeTimers["speed5-10"] - 7f) * 0.01);

//        // Apply the negative reward to the agent
//        agent.AddReward(-(increase));
//    }

//}
// Function to write data to a CSV file




//void WriteToCSV(string data)
//{
//    // Check if the CSV file exists, if not, create it and write the header
//    if (!File.Exists(csvFilePath))
//    {
//        using (StreamWriter writer = new StreamWriter(csvFilePath))
//        {
//            writer.WriteLine("totalTimeinminsoFar, AllVehicleAvgTotaltimeStoppedperCar,AllVehicleAvgSpeedWhenNotStoppedperCar," +
//            "TotalvehicleCollisionCount,TotalnonVehicleCollisionCount,speed0-5mphTimer,speed5-10mphTimer,speed10-15mphTimer," +
//            "speed15-20mphTimer, speed20-25mphTimer, speed25-30mphTimer, speed30-35mphTimer"); //  header
//        }
//    }

//    // Append the data to the CSV file
//    using (StreamWriter writer = new StreamWriter(csvFilePath, true))
//    {
//        writer.WriteLine(data); // totalTimeinminsoFar, AllVehicleAvgTotaltimeStoppedperCar, AllVehicleAvgSpeedWhenNotStoppedperCar,
//                                //vehicleCollisionCount, nonVehicleCollisionCount);
//                                //writer.WriteLine(totalTimeinminsoFar, AllVehicleAvgTotaltimeStoppedperCar, AllVehicleAvgSpeedWhenNotStoppedperCar,
//                                //    vehicleCollisionCount, nonVehicleCollisionCount);
//    }
//}

//    //Debug.Log("totalTimeinminsoFar: " + Time.timeSinceLevelLoad / 60);
//    //Debug.Log("RtotalTimeStopped: " + timeStopped / 60);
//    //Debug.Log("vehicleCollisionCount: " + vehicleCollisionCount);
//    //Debug.Log("nonVehicleCollisionCount: " + nonVehicleCollisionCount);
//    //Debug.Log("speed0 - 5]Timer: " + speedRangeTimers["speed0-5"]);
//    //Debug.Log("speed5 - 10]Timer: " + speedRangeTimers["speed5-10"]);
//    //Debug.Log("speed10 - 15]Timer: " + speedRangeTimers["speed10-15"]);
//    //Debug.Log("speed15 - 20]Timer: " + speedRangeTimers["speed15-20"]);
//    //Debug.Log("speed20 - 25]Timer: " + speedRangeTimers["speed20-25"]);
//    //Debug.Log("speed25 - 30]Timer:" + speedRangeTimers["speed25-30"]);
//    //Debug.Log("speed30 - 35]Timer: " + speedRangeTimers["speed30-35"]);


