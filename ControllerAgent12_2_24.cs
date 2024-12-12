using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
//using System.Text;
//using System.Threading.Tasks;
using System.Linq;
//using System.Diagnostics;
//to make car directionToNode into a single observation instead of 3 observation vector:

//Total observations : 879x2 = 1758 observations from cars + 46traffic lights x 3(138)= 1896 observations
//    Continuous actions are 879 cars +46 lights
//IMPORTANT had set the decision interval to max of 20 steps and no actions inbetween in the unity editor ( as the observations for 879 cars can take
//if decision requester is set to 20, the time between each observation-action step is average 400milliseconds (0.4s), if it was 5, it was avg 100millisec.
//if decision requester is set to 1, the time between each observation-action step is average 20millisec(buon trt more volatility here). the differnce ebtween observation and action was only 0 or 1 millisec..
// testing:with 5 observation vector space and stacked vector 50, it is taking between 13-40milliseconds per step.(not much different from (5vector space, 1 stacked vector, and 1 decision requester)
//on training with above setttings, each etep more consistent averageing around 20millisec, but extending from 17-29millisec, so around 650 cars will take 20 seconds, but the last 50 step observations are saved.
//In inference mode timescale is 1; but learning mode timescale is 20 sometimes ( gametime is20xfaster than normal time)
//decision requester =1 means steps are 0.02s; if =20, then steps are 0.4sec, regardless of whether timescale is 1 or 20 or 100
// Time.time ( supposedly game time) and Time. timesincelevelload( supposedly real time since episode begins) are both same and do not reset with episode, regardless of whether timescale is 1 or 100; so there is no episode timer!
// do not MESS with decision requester script in unity editor for agent.
// NEEDS DECISION REQUESTER COMPONENT IN UNITY EDITOR
//1.STUCK AT EPISODE RESETTING OF TRAFFICCARS; inactive state(vs destroy on collisions) is messing up all metrics
//2. Will have to do manual request decision() (with remove decision requester component of agent) to allow for longer traffic light durations upto 100 than decision requester component of 20(0.4sec intervals). But this will need very few observations as it is easily overwhelming system (maybe use stacked vector or bufferesensors)
// right when you do cntrlC during training, all the continuous actions become 0 ( instead of -1 to 1 range)
//The built application will typically run at a time scale of 1 (real-time), though unity engine can run at gametime of 20 if specified so in the .yaml file

public class ControllerAgent : Agent
{
    public TrafficLights[] trafficLights;// this is an array whereas public TrafficLights trafficLight; is a single light
    public TrafficCar[] trafficCars;
    //public TrafficSystem trafficSystem; // Reference to the TrafficSystem component
    //public TrafficCarManager trafficCarManager;

    //VehicleGenerator vehicleGenerator;
    //private Vector3[] originalPositions;
    //private Quaternion[] originalRotations;

    internal float Cstep1Duration;
    //internal float Cstep2Duration;
    internal float Cspeed;
    //private GameObject[] vehicles;
    //public List<GameObject> inactiveCars = new List<GameObject>();
    public Dictionary<GameObject, (Vector3, Quaternion)> initialPositionsAndRotations = new Dictionary<GameObject, (Vector3, Quaternion)>();
    private int currentSceneIndex; // Declare currentSceneIndex as a member variable


    private void Awake()
    {
        // Get the current scene index
        currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        TrafficCar[] trafficCars = FindObjectsOfType<TrafficCar>();
        TrafficLights[] trafficLights = FindObjectsOfType<TrafficLights>();

        //// Find all active TrafficCar objects and store their original positions
        //TrafficCar[] trafficCars = FindObjectsOfType<TrafficCar>();
        //foreach (TrafficCar car in trafficCars)
        //{
        //    initialPositionsAndRotations.Add(car.gameObject, (car.initialPosition, car.initialRotation));
        //}
    }

    
    public override void OnEpisodeBegin()
    {

        base.OnEpisodeBegin();

        TrafficCar[] trafficCars = FindObjectsOfType<TrafficCar>();
        TrafficLights[] trafficLights = FindObjectsOfType<TrafficLights>();
        Debug.Log("Initial Time.timeScale is" + Time.timeScale);
        Debug.Log("Initial No. of Traffic Cars" + trafficCars.Length);
        Debug.Log("No. of Traffic Lights" + trafficLights.Length);

        foreach (TrafficLights light in trafficLights)
        {
            light.step1Duration = Random.Range(5f, 60f); //10f; // Random.Range(0f, 100f);if you want more exploration but takes longer training; 10f reflects the initial testing environment
            //Debug.Log("light reset with light.step1Duration: " + light.step1Duration);                                              //light.step2Duration = 3f; // Random.Range(0f, 100f); if more explroation needed but takes longer training
                                                          //Debug.Log("lights reset with light.step1Duration: " + light.step1Duration);
        }
        foreach (TrafficCar car in trafficCars)
        {
            if (car != null)
            {
                car.carSetting.limitSpeed = Random.Range(20f, 35f); //30f;
                //Debug.Log("car speedlimit reset with car.carSetting.limitSpeed: " + car.carSetting.limitSpeed);
            }
        }
        // Set the maximum episode duration to 600 seconds (game time)
        //Academy.Instance.EpisodeOverDuration = 600f / Time.timeScale;
        StartCoroutine(EndEpisodeAfterTime(602f));// 602 to allow for compeltion fo calculatemetrics in traffic lights and cars scripts if in testing period
    }

    private IEnumerator EndEpisodeAfterTime(float timeInSeconds)
    {
        //Debug.Log("Starting episode timer..." + Time.timeSinceLevelLoad);
        yield return new WaitForSeconds(timeInSeconds);
        //Debug.Log("Ended episode timer in... " + Time.timeSinceLevelLoad);
        //StopCoroutine(ProcessActionsWithDelay());
        //Debug.Log("Action coroutine ended in " + Time.timeSinceLevelLoad);

        EndEpisode();

        // Reload the current scene //placement before or after endepisode() does not matter; Time.timeSinceLevelLoad gets reset to 0
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneIndex);
        //Debug.Log("Scene reloaded after episode ended in " + Time.timeSinceLevelLoad);//NEVER PUT THIS IN EPISODE BEGIN> IT CRASHES UNITY!

        yield break;//otherwise coroutine keeps running regardless of endepisode
    }



    public override void CollectObservations(VectorSensor sensor)//1896 observations at first; but updates every step; default environment operates at a rate of 60 steps per second(varies basedon computation needs and availability) ; resets every episode
    {
        //as the scene relaods the first frame's observations are empty regardless of where scene relaod is ineepisode end or begin
        TrafficCar[] trafficCars = FindObjectsOfType<TrafficCar>();
        TrafficLights[] trafficLights = FindObjectsOfType<TrafficLights>();
        // Define a list to store active car indices
        List<int> activeCarIndices = new List<int>();
        //Debug.Log("trafficCars.Length Observations: " + trafficCars.Length);
        // Populate the list with indices of active cars
        for (int i = 0; i < trafficCars.Length; i++)
        {
            if (trafficCars[i] != null)
            {
                activeCarIndices.Add(i);
            }
        }

        // Shuffle the list of active car indices
        //*Shuffle(activeCarIndices);
        //Debug.Log("activeCarIndices: " + activeCarIndices.Count);
        // Take observations from a subset of 50 randomly selected active cars
        //*int numCarsToObserve = Mathf.Min(activeCarIndices.Count, 100);//CHANGE/ UNCOMMENT BACK!
        //int numCarsToObserve = activeCarIndices.Count;
        //*for (int i = 0; i < numCarsToObserve; i++)
        for (int i = 0; i < activeCarIndices.Count; i++)
        {
            int carIndex = activeCarIndices[i];
            TrafficCar car = trafficCars[carIndex];
            //Rigidbody carRigidbody = car.GetComponent<Rigidbody>();
            //Vector3 velocity = carRigidbody.velocity;
            //sensor.AddObservation(velocity.x);//y is height
            //sensor.AddObservation(velocity.z);
            //Vector3 ndirectionToNode = car.directionToNode;
            //Vector3 forwardDirection = ndirectionToNode.normalized;
            //sensor.AddObservation(forwardDirection);
            sensor.AddObservation(car.directionToNode.x);
            sensor.AddObservation(car.directionToNode.z);
            //Debug.Log("velocityX: " + velocity.x + " velocityZ: " + velocity.z);
        }
        //Debug.Log("no of cars: " + numCarsToObserve);
        // Method to shuffle a list
        //*void Shuffle<T>(List<T> list)
        //{
        //    int n = list.Count;
        //    while (n > 1)
        //    {
        //        n--;
        //        int k = Random.Range(0, n + 1);
        //        T value = list[k];
        //        list[k] = list[n];
        //        list[n] = value;
        //    }
        //}

        foreach (var light in trafficLights)
        {

            //sensor.AddObservation(new Vector2(light.transform.position.x, light.transform.position.z));//Vector2 3 is counted as 3 different observations by ml agents but not vector 2
            sensor.AddObservation(light.transform.localPosition.z);//theser are x, y, z, threee observations; y is constant at 0.15
            //sensor.AddObservation(light.currentStatus);
            sensor.AddObservation(light.transform.localPosition.x);
            sensor.AddObservation(light.step1Duration);
            //Debug.Log("lightPositionX: " + light.transform.localPosition.x + " lightPositionZ: " + light.transform.localPosition.z + " greenLightDuration): " + light.step1Duration);
        }
        //Debug.Log("all observations done in :" + Time.timeSinceLevelLoad + " with current non-destroyed carcount: " + activeCarIndices.Count);

    }

    //private bool canRequestDecision = true; // Flag to control decision requests
    private float decisionTimer = 0f; // Timer to track decision intervals
    private const float decisionInterval = 60f; // Interval between decision requests in seconds as game time is 20x, so hopefully it is approx decisionInterval/20 sec


    private void FixedUpdate()//have to remove decision requester componenet from unity editor under agent; manual decision requester to allow 0-100f in traffic light duration
    {
        // Update the decision timer
        decisionTimer += Time.fixedDeltaTime;

        // Check if it's time to request a decision
        //if (canRequestDecision && decisionTimer >= decisionInterval)
        if (decisionTimer >= decisionInterval)
        {
            // Reset the decision timer
            decisionTimer = 0f;

            // Request decision from the agent
            RequestDecision();
            //Debug.Log("RequestDecision done in scaled time: " + Time.timeScale +" realtime: "+ Time.timeSinceLevelLoad);
            //Debug.Log("Post RequestDecision() Time.timeScale is" + Time.timeScale);
            //foreach (var kvp in TrafficCar.nonCollDestroyedCarDict)
            //{
            //    string carName = kvp.Key;
            //    List<float> destroyTimes = kvp.Value;
            //    string timesString = string.Join(", ", destroyTimes);
            //    Debug.Log(carName + ": " + timesString);
            //}
            //foreach (var kvp in TrafficCar.collDestroyedCarDict)
            //{
            //    string carName = kvp.Key;
            //    List<float> destroyTimes = kvp.Value;
            //    string timesString = string.Join(", ", destroyTimes);
            //    Debug.Log(carName + ": " + timesString);
            //}
            //Debug.Log("List of noncollided cars destroyed: " + string.Join(", ", TrafficCar.nonCollDestroyedCarDict.Select(kvp => $"{kvp.Key}: [{string.Join(", ", kvp.Value)}]")));
            //Debug.Log("List of collided cars destroyed: " + string.Join(", ", TrafficCar.collDestroyedCarDict.Select(kvp => $"{kvp.Key}: [{string.Join(", ", kvp.Value)}]")));

            //Debug.Log("List of noncollided cars destroyed: " + TrafficCar.nonCollDestroyedCarDict.Count + " "+ string.Join(", ", TrafficCar.nonCollDestroyedCarDict));
            //Debug.Log("List of Collided cars destroyed: " + TrafficCar.collDestroyedCarDict.Count + " " + string.Join(", ", TrafficCar.collDestroyedCarDict));
            // Set the flag to prevent further requests until the next interval
            //canRequestDecision = false;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)//CHANGE/ UNCOMMENT BACK! continuous actions output as -1 to 1.
    {
        TrafficCar[] trafficCars = FindObjectsOfType<TrafficCar>();
        TrafficLights[] trafficLights = FindObjectsOfType<TrafficLights>();
        var lightActions = new List<float>();
        var carActions = new List<float>();
        for (int i = 0; i < (trafficCars.Length + trafficLights.Length); i++)
        {
            if (i < trafficLights.Length)
            {
                //lightActions.Add(actions.ContinuousActions[i]);
                lightActions.Add(actions.ContinuousActions[i]);
            }
            else
            {
                carActions.Add(actions.ContinuousActions[i]);
            }
        }
        //Debug.Log("actions distributed to lights action and caractions in " + Time.timeSinceLevelLoad);
        // Debugging: Log received action values
        //Debug.Log("Light Actions: " + string.Join(", ", lightActions));
        //Debug.Log("Car Actions: " + string.Join(", ", carActions));
        // Clamp each value in lightActions between 5f and 100f
        //var clampedActions = lightActions.Select(action => Mathf.Clamp(action, 5f, 100f)).ToArray();
        //Debug.Log("Light durations: " + string.Join(", ", clampedActions));
        //var clampedSpeeds = carActions.Select(action => Mathf.Clamp(action, 20f, 35f)).ToArray();
        //Debug.Log("speed limits: " + string.Join(", ", clampedSpeeds));

        float minSpeed = 20f;
        float maxSpeed = 35f;
        // Assign actions to cars
        for (int i = 0; i < trafficCars.Length; i++)
        {
            TrafficCar car = trafficCars[i];
            if (car != null && i < carActions.Count)
            {
                //float desiredSpeed = Mathf.Lerp(20f, 35f, Mathf.Clamp(carActions[i], 0f, 1f));
                //float desiredSpeed = Mathf.Clamp(carActions[i], 20f, 35f);chosing the lowest value
                //(carActions[i] + 1.0f) shifts the action value to the range of [0, 2].average value * (maxSpeed - minSpeed) / 2.0f scales the shifted action value to the range of [0, 15]. 
                
                float desiredSpeed = minSpeed + (carActions[i] + 1.0f) * (maxSpeed - minSpeed) / 2.0f;
                car.carSetting.limitSpeed = desiredSpeed;
                //Debug.Log("Car " + i + " desiredSpeedLimit: " + desiredSpeed);
                //Debug.Log("Car " + i + " carSetting.limitSpeed: " + car.carSetting.limitSpeed);
            }
        }
        //Debug.Log("879 car actions done in " + Time.timeSinceLevelLoad);

        float minLiDuration = 5f;
        float maxLiDuration = 60f;
    
        // Assign actions to traffic lights
        for (int i = 0; i < trafficLights.Length; i++)
        {
            TrafficLights light = trafficLights[i];
            if (i < lightActions.Count)
            {
                //float desiredLightDuration = Mathf.Lerp(5f, 100f, Mathf.Clamp(lightActions[i], 0f, 1f));
                //float desiredLightDuration = Mathf.Clamp(lightActions[i], 5f, 100f); //continuous actions are populating well (-1,1)but all clamping down to lowest number such as 5
                float desiredLightDuration = minLiDuration + (lightActions[i] + 1.0f) * (maxLiDuration - minLiDuration) / 2.0f;//(lightActions[i] + 1.0f) shifts the action value to the range of [0, 2].* 47.5f scales the shifted action value to the desired range of [0, 95]
                light.step1Duration = desiredLightDuration;
                //Debug.Log("ControllerScript Traffic Light " + i + " desiredLightDuration: " + desiredLightDuration);
                //Debug.Log("Traffic Light " + i + " light.step1Duration in ControllerScript : " + light.step1Duration + " " + Time.timeSinceLevelLoad);
            }

        }
        
        //Debug.Log("46 light actions done in timeSinceLevelLoad " + Time.timeSinceLevelLoad);
        //Debug.Log("46 light actions done in realtimeSinceStartup" + Time.realtimeSinceStartup);
        //Debug.Log("46 light actions done in game time" + Time.time);
        //canRequestDecision = true;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;

        for (int i = 0; i < actions.Length; i++)
        {
            if (i < 46) // Assuming the first 46 actions are for traffic lights
            {
                actions[i] = 10f;
            }
            else
            {
                actions[i] = 30f;
            }
        }
    }


}
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ENDS










//public override async void OnActionReceived(ActionBuffers actions)





//public override void OnActionReceived(ActionBuffers actions)
//    {
//        var rActions = actions.ContinuousActions.Array;

//        //await Task.Delay(5000); //NOT WORKINGc# technique to delay in real time ( since 5 seconds in game time is equivalent to 100 sec in real time due to timescale of 20)
//        //Debug.Log("finsihed waiting 104 seconds in " + Time.timeSinceLevelLoad);
//        // Start a coroutine to wait for 100 seconds before continuing
//        StartCoroutine(WaitBeforeNextAction(rActions));//not working
//    }

//    private IEnumerator WaitBeforeNextAction(float[] rActions)
//    {
//        while (true)
//        {

//            // Wait for 100 seconds
//            yield return new WaitForSeconds(100f);

//            // Process actions
//            ProcessActions(rActions);

//            // Call RequestDecision to trigger the next set of actions
//            RequestDecision();

//        }
//    }

//    private void ProcessActions(float[] rActions)
//    {

//        var lightActions = new List<float>();
//        var carActions = new List<float>();
//        for (int i = 0; i < totalNoActions; i++)
//        {
//            if (i < trafficLights.Length)
//            {
//                //lightActions.Add(actions.ContinuousActions[i]);
//                lightActions.Add(rActions[i]);
//            }
//            else
//            {
//                carActions.Add(rActions[i]);
//            }
//        }
//        Debug.Log("actions distributed to lights action and caractions in " + Time.timeSinceLevelLoad);

//        // Assign actions to cars
//        for (int i = 0; i < trafficCars.Length; i++)
//        {
//            TrafficCar car = trafficCars[i];
//            if (car != null && i < carActions.Count)
//            {
//                float desiredSpeed = Mathf.Lerp(20f, 35f, Mathf.Clamp(carActions[i], 0f, 1f));
//                car.carSetting.limitSpeed = desiredSpeed;
//                //Debug.Log("Car " + i + " carSetting.limitSpeed: " + car.carSetting.limitSpeed);
//            }
//        }
//        Debug.Log("879 car actions done in " + Time.timeSinceLevelLoad);

//        // Assign actions to traffic lights
//        for (int i = 0; i < trafficLights.Length; i++)
//        {
//            TrafficLights light = trafficLights[i];
//            if (i < lightActions.Count)
//            {
//                float desiredLightDuration = Mathf.Lerp(5f, 100f, Mathf.Clamp(lightActions[i], 0f, 1f));
//                light.step1Duration = desiredLightDuration;
//                //Debug.Log("ControllerScript Traffic Light " + i + " desiredLightDuration: " + desiredLightDuration);
//                Debug.Log("Traffic Light " + i + " light.step1Duration in ControllerScript : " + light.step1Duration + " " + Time.timeSinceLevelLoad);
//            }

//        }

//        Debug.Log("46 light actions done in timeSinceLevelLoad " + Time.timeSinceLevelLoad);
//        Debug.Log("46 light actions done in realtimeSinceStartup" + Time.realtimeSinceStartup);
//        //canRequestDecision = true;
//        Debug.Log("Time.timeScale is" + Time.timeScale);


//    }



//private IEnumerator WaitBeforeNextAction(float waitActionTime)
//{
//    Debug.Log("starting coroutine wait period in " + Time.timeSinceLevelLoad);
//    Debug.Log("Time.timeScale is" + Time.timeScale); //If Time.timeScale is set to 20, it means that time in your game is passing 20 times faster than normal.
//    //that means any waitseconds we code in have to be 20 times longer.
//    yield return new WaitForSecondsRealtime(waitActionTime);// WaitForSecondsRealtime;/ max step1duration +3sec for yellow light and 1 sec for red-red lights.
//    Debug.Log("2nd Time.timeScale is" + Time.timeScale);
//    Debug.Log("waited for "+ waitActionTime+" in overall "+ Time.timeSinceLevelLoad);
//}


///////END of TO_3
///
/// begin TO_2
//// Collect observations for the current car
//// Loop until a valid Rigidbody component is found
//while (currentCarIndex < trafficCars.Length)
//{
//    TrafficCar car = trafficCars[currentCarIndex];

//    if (car != null)
//    {
//        Rigidbody carRigidbody = car.GetComponent<Rigidbody>();
//        // Add observations for the current car
//        Vector3 velocity = carRigidbody.velocity;
//        //sensor.AddObservation(velocity);//3
//        sensor.AddObservation(velocity.x); // Add velocity vector x,y as observation
//        sensor.AddObservation(velocity.y);
//        //Debug.Log("Valid carIndex: " + currentCarIndex);
//        currentCarIndex++;
//        if (currentCarIndex > (trafficCars.Length - 1))
//        {
//            // If all traffic lights' observations have been collected, reset the index and mark observations as collected
//            currentCarIndex = 0;
//        }
//        break; // Exit the while loop if a valid car is found
//    }
//    else
//    {
//        //Debug.Log("Non-Valid carIndex: " + currentCarIndex);
//        // Increment the index to check for the next car
//        currentCarIndex++;
//        if (currentCarIndex > (trafficCars.Length - 1))
//        {
//            // If all traffic lights' observations have been collected, reset the index and mark observations as collected
//            currentCarIndex = 0;
//        }
//    }
//}




//TrafficLights light = trafficLights[currentLightIndex];
//sensor.AddObservation(light.currentStatus);
//sensor.AddObservation(light.transform.localPosition.x);
//sensor.AddObservation(light.transform.localPosition.y);

//// Move to the next traffic light in the next step
//currentLightIndex++;
//if (currentLightIndex > (trafficLights.Length - 1))
//{
//    // If all traffic lights' observations have been collected, reset the index and mark observations as collected
//    currentLightIndex = 0;
//}
// Log the duration of the current step
//UnityEngine.Debug.Log("Step duration till observations done: " + stepStopwatch.ElapsedMilliseconds + " milliseconds");
// end of TO_2
//}

//public override void OnEpisodeBegin()
//    {
//        // Reset the environment for a new episode
//        trafficLights = FindObjectsOfType<TrafficLights>();
//        trafficCars = FindObjectsOfType<TrafficCar>();
//        //trafficLightPositionsCollected = false;
//        //Change the durations of traffic lights
//        foreach (TrafficLights light in trafficLights)
//        {

//            light.step1Duration = 10f; // Random.Range(0f, 100f);if you want more exploration but takes longer training; 10f reflects the initial testing environment
//            //light.step2Duration = 3f; // Random.Range(0f, 100f); if more explroation needed but takes longer training

//        }
//        foreach (TrafficCar car in trafficCars)
//        { 
//            if (car != null)
//            {
//                car.limitSpeed = 30f;
//            }

//        }


//                // Initialize stopwatch for measuring step duration
//        //stepStopwatch = new Stopwatch();
//        // Start the stopwatch
//        //stepStopwatch.Start();
//        // Other initialization code...
//        //// Start coroutines for changing speeds of traffic cars
//        //StartCoroutine(ChangeSpeedsAndLightDurations());

//        // End the episode after 10 minutes (600 seconds)
//        StartCoroutine(EndEpisodeAfterTime(600f));
//    }

//    private IEnumerator EndEpisodeAfterTime(float timeInSeconds)
//    {
//        yield return new WaitForSeconds(timeInSeconds);
//        EndEpisode();
//    }

//    public override void OnActionReceived(ActionBuffers actions)
//    {
//        var lightActions = new List<float>();
//        var carActions = new List<float>();
//        for (int i = 0; i < actions.ContinuousActions.Count; i++)
//        {
//            if (i < trafficLights.Length)
//            {
//                lightActions.Add(actions.ContinuousActions[i]);
//            }
//            else
//            {
//                carActions.Add(actions.ContinuousActions[i]);
//            }
//        }

//        // Assign actions to cars
//        for (int i = 0; i < trafficCars.Length; i++)
//        {
//            TrafficCar car = trafficCars[i];
//            if (car != null)
//            {
//                float desiredSpeed = Mathf.Lerp(20f, 35f, Mathf.Clamp(carActions[i][1], 0f, 1f));
//                car.limitSpeed = desiredSpeed;
//                Debug.Log("Car " + i + " desiredSpeed: " + desiredSpeed);
//            }
//        }

//        // Assign actions to traffic lights
//        for (int i = 0; i < trafficLights.Length; i++)
//        {
//            TrafficLights light = trafficLights[i];
//            float desiredLightDuration = Mathf.Lerp(0f, 100f, Mathf.Clamp(lightActions[i][0], 0f, 1f));
//            light.step1Duration = desiredLightDuration;
//            Debug.Log("Traffic Light " + i + " desiredLightDuration: " + desiredLightDuration);
//        }
//    }

//    public override void Heuristic(in ActionBuffers actionsOut)
//    {
//        var actions1 = actionsOut.ContinuousActions;

//        actions1[0] = 10f;
//        actions1[1] = 30f;
//        //actions1[2] = 30f;
//    }


//}

//public override void OnActionReceived(ActionBuffers actions)
//    {
//        // Receive actions from the model
//        var action = actions.ContinuousActions; //action is an array , in this case onde dimensionsional of two numbers
//        foreach (TrafficCar car in trafficCars)
//        {
//            if (car != null)
//            {
//                //float carSpeedAction = Mathf.Clamp(action[0], 0f, 1f); // Assuming action[0] represents desired car speed

//                // Map the continuous action to the desired speed range
//                //float desiredSpeed = Mathf.Lerp(20f, 35f, Cspeed);
//                Cspeed = Mathf.Clamp(action[1], 0f, 1f);
//                float desiredSpeed = Mathf.Lerp(20f, 35f, Cspeed);
//                //Cspeed = Mathf.Clamp(action[1], 20f, 35f);
//                car.limitSpeed = desiredSpeed;
//                Debug.Log("desiredSpeed: " + desiredSpeed);
//                Debug.Log("car.limitSpeed: " + car.limitSpeed);
//            }
//        }
//        foreach (TrafficLights light in trafficLights)
//        {
//            //Cstep1Duration = Mathf.Clamp(action[0], 0f, 100f);
//            //light.step1Duration = Cstep1Duration;
//            //Debug.Log("Cstep1Duration " + Cstep1Duration);

//            Cstep1Duration = Mathf.Clamp(action[0], 0f, 1f);
//            float desiredLightduration = Mathf.Lerp(0f, 100f, Cstep1Duration);
//            //Cspeed = Mathf.Clamp(action[1], 20f, 35f);
//            light.step1Duration = desiredLightduration;
//            Debug.Log("desiredLightduration: " + desiredLightduration);
//            Debug.Log("light.step1Duration: " + light.step1Duration);
//        }
//    }


//public override void OnActionReceived(ActionBuffers actions)
//{
//    // Receive actions from the model
//    var action = actions.ContinuousActions; //action is an array , in this case onde dimensionsional of two numbers
//    //Cstep1Duration = Mathf.Clamp(action[0], 0f, 100f);
//    //Cstep2Duration = Mathf.Clamp(action[1], 0f, 100f);
//    //Cspeed = Mathf.Clamp(action[1], 20f, 35f);
//    //StartCoroutine(ChangeSpeedsAndLightDurations());
//    //while (isEpisodeActive)
//    //{
//    // Randomly change the speeds of traffic cars
//    foreach (TrafficCar car in trafficCars)
//    {
//        if (car != null)
//        {
//            Cspeed = Mathf.Clamp(action[1], 20f, 35f);
//            car.limitSpeed = Cspeed;
//        }// Wait for staggered update
//        //yield return new WaitForSeconds(Random.Range(0.01f, 0.1f));

//        // Randomly change the speed of the car
//        //float speed = Random.Range(0f, 30f);
//        //car.limitSpeed = Cspeed;

//        // Ensure the change lasts at least for 15 seconds
//        //yield return new WaitForSeconds(15f);
//    }

//    // Randomly change the duration of traffic lights
//    foreach (TrafficLights light in trafficLights)
//    {
//        // Wait for staggered update
//        //yield return new WaitForSeconds(Random.Range(0.02f, 0.04f));

//        // Randomly change the duration of the light
//        //float duration = Random.Range(2f, 100f); // Adjust the range as needed
//        Cstep1Duration = Mathf.Clamp(action[0], 0f, 100f);
//        light.step1Duration = Cstep1Duration;
//        //light.step2Duration = Cstep2Duration;

//        // Ensure the change lasts at least for 2 seconds
//        //yield return new WaitForSeconds(2f);
//    }

//    // Log the duration of the current step
//    //UnityEngine.Debug.Log("Step duration till action: " + stepStopwatch.ElapsedMilliseconds + " milliseconds");
//    // Reset the stopwatch to measure the next step
//    //stepStopwatch.Reset();
//    //stepStopwatch.Start();

//}

/// <summary>
/// //////////////////////////////////////////////////////////////////////////////////////
/// </summary>
//public override void OnEpisodeBegin()
//    {
//        isEpisodeActive = true;
//        // Reset the environment for a new episode
//        trafficCar = GameObject.FindAnyObjectByType<TrafficCar>();
//        trafficLight = GameObject.FindAnyObjectByType<TrafficLights>();

//        // Initialize step durations and speed with random values
//        Cstep1Duration = Random.Range(0f, 100f);
//        Cstep2Duration = Random.Range(0f, 100f);
//        Cspeed = Random.Range(0f, 30f);
//        // End the episode after 10 minutes (600 seconds)
//        StartCoroutine(EndEpisodeAfterTime(600f));
//    }
//    public void EndEpisode()
//    {
//        isEpisodeActive = false;
//    }

//    private IEnumerator EndEpisodeAfterTime(float timeInSeconds)
//    {
//        yield return new WaitForSeconds(timeInSeconds);
//        EndEpisode();
//    }
//    //public override void OnEpisodeBegin()
//    //{

//    //    // End the episode after 10 minutes (600 seconds)
//    //    yield return new WaitForSeconds(600f);
//    //    EndEpisode();
////}
//public override void OnActionReceived(ActionBuffers actions)
//{
//    // Receive actions from the model
//    var action = actions.ContinuousActions;
//    Cstep1Duration = Mathf.Clamp(action[0], 0f, 100f);
//    Cstep2Duration = Mathf.Clamp(action[1], 0f, 100f);
//    Cspeed = Mathf.Clamp(action[2], 0f, 30f);

//    // Apply actions to control traffic lights
//    trafficLight.step1Duration = Cstep1Duration;// SetStepDurations(step1Duration, step2Duration);
//    trafficLight.step2Duration = Cstep2Duration;

//    // Update speed of the traffic car
//    trafficCar.speed = Cspeed;

//    if (Time.realtimeSinceStartup - lastDebugTime >= logInterval)//timeSinceLevelLoadresets at end of episode
//    {
//        Debug.Log("Cstep1Duration: " + Cstep1Duration);
//        Debug.Log("Cstep2Duration: " + Cstep2Duration);
//        lastDebugTime = Time.realtimeSinceStartup;
//    }
//    //var action = actions.ContinuousActions;
//    //float speedAction = Mathf.Clamp(action[0], 0f, 4f);
//    //trafficCar.speedAction = speedAction;
//    //float timeAction = Mathf.Clamp(action[1], 0f, 5f);
//    //trafficLight.timeAction = timeAction;
//}

//sensor.AddObservation(trafficCar.vehicleCollisionCount);
//sensor.AddObservation(trafficCar.totalDistance);
//sensor.AddObservation(trafficCar.passThroughStop);
////sensor.AddObservation(trafficLight.step1Duration);
////sensor.AddObservation(trafficLight.step2Duration);
////sensor.AddObservation(trafficLight.currentStatus);
////sensor.AddObservation(trafficLight.elapsedTime);
////sensor.AddObservation(vehicleCounter.count);
//sensor.AddObservation(trafficCar.congestionTimer);
//List<TrafficCar> carsList = TrafficCar.GetAllTrafficCars();
//List<TrafficLights> lightsList = TrafficLights.GetAllTrafficLights();

//public int currentNode { get; private set; }

//private readonly int currentNode;

//private int currentNode;

//private void Awake()
//{

//    //vehicleCounter = GameObject.FindAnyObjectByType<VehicleCounter>();
//    //vehicleGenerator = GameObject.FindObjectOfType<VehicleGenerator>();
//}
//public override void OnEpisodeBegin()
//{
//    //vehicleGenerator = new VehicleGenerator();
//    //vehicleGenerator.ResetPoses();
//}
//List<TrafficCar> carsList = TrafficCar.GetAllTrafficCars();
// Add forward direction observation for the car
//Vector3 forwardDirection = trafficCar.transform.forward;

//public GameObject[] vehicles;
//public Vector3[] vehiclesPos;
//VehicleGenerator vehicleGenerator;
//public float speedAction, timeAction;
//VehicleCounter vehicleCounter;


//public override void CollectObservations(VectorSensor sensor)//total 9 observations
//{
//    foreach (var light in trafficLights)
//    {

//        sensor.AddObservation(light.transform.localPosition.x);
//        sensor.AddObservation(light.transform.localPosition.y);//theser are x, y, z, threee observations
//        sensor.AddObservation(light.currentStatus);
//    }

//    foreach (var car in trafficCar)
//    {
//        if (!car.isDestroyed)
//        {
//            // Add observations for active cars
//            sensor.AddObservation(car.transform.localPosition.x); // X, Y, Z //transform.forward vector also has 3 dimensions
//            sensor.AddObservation(car.transform.localPosition.y); // X, Y, Z
//            sensor.AddObservation(car.transform.localPosition.z); // X, Y, Z// to detect b,ridges.
//                                                                  //sensor.AddObservation(car.atualWay);//the road/waypoint that the car is on (TS10, TS-18 etc)same as local position, until it reaches the end of that road, then it becomes the next road/waypoint 
//            sensor.AddObservation(car.speed);
//        }
//    }
//}
//private IEnumerator ChangeSpeedsAndLightDurations()
//{

//    while (isEpisodeActive)
//    {
//        // Randomly change the speeds of traffic cars
//        foreach (TrafficCar car in carsList)
//        {
//            // Wait for staggered update
//            yield return new WaitForSeconds(Random.Range(0.01f, 0.1f));

//            // Randomly change the speed of the car
//            //float speed = Random.Range(0f, 30f);
//            car.limitSpeed = Cspeed;

//            // Ensure the change lasts at least for 15 seconds
//            //yield return new WaitForSeconds(15f);
//        }

//        // Randomly change the duration of traffic lights
//        foreach (TrafficLights light in trafficLights)
//        {
//            // Wait for staggered update
//            yield return new WaitForSeconds(Random.Range(0.02f, 0.04f));

//            // Randomly change the duration of the light
//            //float duration = Random.Range(2f, 100f); // Adjust the range as needed
//            light.step1Duration = Cstep1Duration;
//            light.step2Duration = Cstep2Duration;

//            // Ensure the change lasts at least for 2 seconds
//            //yield return new WaitForSeconds(2f);
//        }

//        // Wait for some time before changing again
//        yield return new WaitForSeconds(Random.Range(1f, 10f));
//    }
//}
//private IEnumerator CollectObservationsCoroutine(VectorSensor sensor)
//{
//    while (isEpisodeActive)
//    {

//        //Debug.Log("carsList.Count: " + carsList.Count);
//        //Debug.Log("trafficCars.Count: " + trafficCars.Length);
//        //Debug.Log("trafficLights.Length: " + trafficLights.Length);
//        // Loop through all the traffic cars
//        for (int i = 0; i < trafficCars.Length; i++)
//        {
//            // Wait for staggered update
//            yield return new WaitForSeconds(i * 0.01f);

//            // Add observations for a traffic car
//            // Get the traffic car at index i
//            TrafficCar car = trafficCars[i];

//            // Check if the car exists and is not null
//            if (car != null) //getting prepared list with onlya ctive cars is causing errors of not findign cars
//            {
//                sensor.AddObservation(car.transform.localPosition.x);
//                sensor.AddObservation(car.transform.localPosition.y);
//                //sensor.AddObservation(car.transform.localPosition.z); only a couple of bridges and not too many traffic lights there; decrease complexity by removing z 
//                sensor.AddObservation(car.limitSpeed);
//            }

//        }

//        // Loop through all the traffic lights
//        for (int i = 0; i < trafficLights.Length; i++)
//        {
//            // Wait for staggered update
//            yield return new WaitForSeconds(i * 0.02f);

//            // Add observations for a traffic light
//            TrafficLights light = trafficLights[i];
//            sensor.AddObservation(light.transform.localPosition.x);
//            sensor.AddObservation(light.transform.localPosition.y);
//            sensor.AddObservation(light.currentStatus);
//        }

//        // Wait for the next update
//        yield return new WaitForSeconds(0.01f);
//    }
//}
//private IEnumerator CollectObservationsCoroutine(VectorSensor sensor)
//{


//    //Debug.Log("carsList.Count: " + carsList.Count);
//    //Debug.Log("trafficCars.Count: " + trafficCars.Length);
//    //Debug.Log("trafficLights.Length: " + trafficLights.Length);
//    // Loop through all the traffic cars
//    for (int i = 0; i < trafficCars.Length; i++)
//    {
//        // Wait for staggered update
//        yield return new WaitForSeconds(i * 0.34f);

//        // Add observations for a traffic car
//        // Get the traffic car at index i
//        TrafficCar car = trafficCars[i];
//        Rigidbody carRigidbody = car.GetComponent<Rigidbody>();
//        if (carRigidbody != null)
//        {
//            Vector3 velocity = carRigidbody.velocity;
//            sensor.AddObservation(velocity.x); // Add velocity vector as observation
//            sensor.AddObservation(velocity.y);
//        }

//    }
//    // Loop through all the traffic lights
//    for (int i = 0; i < trafficLights.Length; i++)
//    {
//        //sensor.AddObservation(light.transform.localPosition.x);
//        //sensor.AddObservation(light.transform.localPosition.y);

//    }
//    // Loop through all the traffic lights
//    for (int i = 0; i < trafficLights.Length; i++)
//    {
//        // Wait for staggered update
//        yield return new WaitForSeconds(i * 0.02f);

//        // Add observations for a traffic light
//        TrafficLights light = trafficLights[i];
//        sensor.AddObservation(light.currentStatus);
//    }

//    // Wait for the next update
//    yield return new WaitForSeconds(0.01f);

//}
//for (int i = 0; i < trafficCars.Length; i++)
//{
//    // Wait for staggered update
//    yield return new WaitForSeconds(i * 0.01f);
//    TrafficCar car = trafficCars[i];
//    Rigidbody carRigidbody = car.GetComponent<Rigidbody>();
//    if (carRigidbody != null)
//    {
//        Vector3 velocity = carRigidbody.velocity;
//        sensor.AddObservation(velocity.x); // Add velocity vector as observation
//        sensor.AddObservation(velocity.y);
//    }
//    else
//    {
//        Debug.LogWarning("Traffic car does not have a Rigidbody component attached.");
//    }// Wait for staggered update
//yield return new WaitForSeconds(i * 0.01f);
//TrafficCar car = trafficCars[i];
// Check if the car exists and is not null
//if (car != null) //getting prepared list with onlya ctive cars is causing errors of not findign cars
//{
//    sensor.AddObservation(car.transform.localPosition.x);
//    sensor.AddObservation(car.transform.localPosition.y);
//    //sensor.AddObservation(car.transform.localPosition.z); only a couple of bridges and not too many traffic lights there; decrease complexity by removing z 
//    sensor.AddObservation(car.speed);
//}
//}

//    // Loop through all the traffic lights
//    for (int i = 0; i < trafficLights.Length; i++)
//    {
//        // Wait for staggered update
//        //yield return new WaitForSeconds(i * 0.02f);

//        // Add observations for a traffic light
//        TrafficLights light = trafficLights[i];
//        sensor.AddObservation(light.transform.localPosition.x);
//        sensor.AddObservation(light.transform.localPosition.y);
//        sensor.AddObservation(light.currentStatus);
//    }
//}
////NOT WORKING:
//List<float> lightActions = new List<float>();
//List<float> carActions = new List<float>();
//lightActions = actions.ContinuousActions.GetRange(0, trafficLights.Length); // Extract actions for traffic lights
//var carActions = actions.ContinuousActions.GetRange(trafficLights.Length, actions.ContinuousActions.Count - trafficLights.Length); // Extract actions for cars

//var lightActions = new List<float>(actions.ContinuousActions.ArraySlice.Slice(0, trafficLights.Length)); // Extract actions for traffic lights
//var carActions = new List<float>(actions.ContinuousActions.ArraySlice.Slice(trafficLights.Length, actions.ContinuousActions.ArraySlice.Count - trafficLights.Length)); // Extract actions for cars

//private float lastDebugTime = 0f;//float debugInterval = 3600f; // Debug every hour (3600 seconds)
//private float logInterval = 1800;
//private int carsObservedIndex = 0;
//private int lightsObservedIndex = 0;
//private bool trafficLightPositionsCollected;
//private int currentCarIndex = 0;
//private int currentLightIndex = 0;
//private Stopwatch stepStopwatch;

//List<TrafficCar> carsList = TrafficCar.GetAllTrafficCars();coming up with some 400 cars +

//List<float> lightActions = new List<float>();
//List<float> carActions = new List<float>();
//lightActions = actions.ContinuousActions.GetRange(0, trafficLights.Length); // Extract actions for traffic lights
//var carActions = actions.ContinuousActions.GetRange(trafficLights.Length, actions.ContinuousActions.Count - trafficLights.Length); // Extract actions for cars
//for (int i = 0; i < actions.ContinuousActions.Count; i++)
//{
//    if (i < trafficLights.Length)
//    {
//        lightActions.Add(actions.ContinuousActions[i]);
//    }
//    else
//    {
//        carActions.Add(actions.ContinuousActions[i]);
//    }
//}

//otherwise breaking if all car observations collected at once. even using coroutines, the delays in coroutines were discordant with tcollectobservations
//method which encased the coroutine
//if (!trafficLightPositionsCollected)//one time collection only, but also not retaining it
//{
//    // Collect traffic light positions
//    foreach (TrafficLights TrLi in trafficLights)
//    {
//        sensor.AddObservation(TrLi.transform.localPosition.x);
//        sensor.AddObservation(TrLi.transform.localPosition.y);
//    }
//    trafficLightPositionsCollected = true;
//}
/////////////begin TO_3
///
//string currentSceneIndex = null;

//Debug.Log("Starting new episode...");
// Access the TrafficCarManager instance
// = TrafficCarManager.instance;
//if (trafficCarManager == null)
//{
//    trafficCarManager = TrafficCarManager.instance;
//}

// Debug the number of cars in initialPositionsAndRotations
//Debug.Log("Number of cars in initialPositionsAndRotations: " + initialPositionsAndRotations.Count);

// Debug the number of inactive cars
//Debug.Log("Number of inactive cars: " + inactiveCars.Count);

// Reset the environment for a new episode

// 1. Make all cars inactive
//foreach (GameObject carGameObject in initialPositionsAndRotations.Keys)
//{
//    carGameObject.SetActive(false);
//}

// 2. Reset the positions and rotations of all traffic cars to their original values
//foreach (KeyValuePair<GameObject, (Vector3, Quaternion)> entry in initialPositionsAndRotations)
//{
//    GameObject carGameObject = entry.Key;
//    (Vector3 initialPosition, Quaternion initialRotation) = entry.Value;

//    // Reset position and rotation
//    carGameObject.transform.position = initialPosition;
//    carGameObject.transform.rotation = initialRotation;
//    // Set the car active
//    carGameObject.SetActive(true);
//}

// 3. Make previously inactive cars active again
//foreach (GameObject inactiveCar in trafficCarManager.inactiveCars)
//{
//    inactiveCar.SetActive(true);
//}

// Clear the inactiveCars list as they are now active
//trafficCarManager.inactiveCars.Clear();








//Debug.Log("TrafficCar.GlobalInactiveCarsList.inactiveCars.count: " + TrafficCar.GlobalInactiveCarsList.inactiveCars.Count);
// Restore inactive cars
//foreach (GameObject obj in TrafficCarManager.instance.inactiveCars)
//{
//    obj.SetActive(true);
//}

//// Reset active cars to their initial positions and rotations
//foreach (KeyValuePair<GameObject, (Vector3, Quaternion)> entry in TrafficCarManager.instance.initialPositionsAndRotations)
//{
//    GameObject carObject = entry.Key;
//    (Vector3 initialPosition, Quaternion initialRotation) = entry.Value;

//    // Reset position and rotation
//    carObject.transform.position = initialPosition;
//    carObject.transform.rotation = initialRotation;
//}
//// Check if the trafficSystem reference is not null //didnt work
//if (trafficSystem != null)
//{
//    // Call the LoadCars method with optional parameter intenseTraffic set to true
//    trafficSystem.LoadCars(true);
//}
//else
//{
//    Debug.LogError("TrafficSystem reference is null. Make sure to assign it in the Inspector.");
//}
//////////////////////////////
//foreach (GameObject obj in TrafficCar.GlobalInactiveCarsList.inactiveCars)//restore inactive cars
//{
//    obj.SetActive(true);
//}
//    //vehicles = GameObject.FindGameObjectsWithTag("vehicle");
//    //vehicleGenerator.ResetPoses();
//    // Reset the environment for a new episode
//trafficLights = FindObjectsOfType<TrafficLights>();
//trafficCars = FindObjectsOfType<TrafficCar>();// only has active game objects
//Debug.Log("trafficCars.Count at beginning of Episode: " + trafficCars.Length);

//for (int i = 0; i < trafficCars.Length; i++)
//{
//    trafficCars[i].transform.position = originalPositions[i];
//    trafficCars[i].transform.rotation = originalRotations[i];
//    trafficCars[i].ResetStateC();
//    trafficCars[i].carSetting.limitSpeed = Random.Range(20f, 35f); //30f;
//}
/////////////////////////////////////////////////   //CHANGE/ UNCOMMENT BACK!

//Debug.Log("trafficCars.Length in episode begin: " + trafficCars.Length);
//foreach (TrafficCar car in trafficCars)
//{
//    if (car != null)
//    {
//        car.ResetStateC();
//        car.carSetting.limitSpeed = Random.Range(20f, 35f); //30f;
//    }
//    //Debug.Log("episode begins with car.limitSpeed: " + car.carSetting.limitSpeed);

//}
////trafficLightPositionsCollected = false;
////Change the durations of traffic lights
////Debug.Log("trafficLights.Length in episode begin: " + trafficLights.Length);
//foreach (TrafficLights light in trafficLights)
//{
//    light.ResetStateL();
//    light.step1Duration = Random.Range(0f, 100f); //10f; // Random.Range(0f, 100f);if you want more exploration but takes longer training; 10f reflects the initial testing environment
//    //light.step2Duration = 3f; // Random.Range(0f, 100f); if more explroation needed but takes longer training
//    //Debug.Log("episode begins with light.step1Duration: " + light.step1Duration);
//}

/////////////////////////////////////////////////////////////////////////



//StartCoroutine(ProcessActionsWithDelay());
//}

//private IEnumerator ProcessActionsWithDelay()//while true was causing observations and actions at 100, 200, 300, 310, 400, 410; without it getting them at 100, 410, 720s
//{
//    Debug.Log("Wait for 100 seconds");
//    yield return new WaitForSeconds(100f);
//    Debug.Log("Finished Waiting for 100 seconds");
//    // Request decision for the next set of actions
//    RequestDecision();
//    Debug.Log("Request decision completed in  "+ Time.timeSinceLevelLoad);

//}







//    //Debug.Log("Starting episode timer..." + Time.timeSinceLevelLoad);
//    yield return new WaitForSeconds(timeInSeconds);
//    //Debug.Log("Ended episode timer in... " + Time.timeSinceLevelLoad);
//    //StopCoroutine(ProcessActionsWithDelay());
//    //Debug.Log("Action coroutine ended in " + Time.timeSinceLevelLoad);

//    EndEpisode();
//    // Reload the current scene //placemetn before or after endepisode() does not matter
//    UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneIndex);
//    //Debug.Log("Scene reloaded after episode ended in " + Time.timeSinceLevelLoad);//NEVER PUT THIS IN EPISODE BEGIN> IT CRASHES UNITY!
//                                                                                  //Debug.Log("End of episode in " + Time.timeSinceLevelLoad);

//    // Reset the environment for a new episode
//    //trafficLights = FindObjectsOfType<TrafficLights>();
//    //trafficCars = FindObjectsOfType<TrafficCar>();
//    ////trafficLightPositionsCollected = false;
//    ////Change the durations of traffic lights
//    //foreach (TrafficLights light in trafficLights)
//    //{
//    //    light.ResetStateL();
//    //    light.step1Duration = Random.Range(0f, 100f); //10f; // Random.Range(0f, 100f);if you want more exploration but takes longer training; 10f reflects the initial testing environment
//    //    //light.step2Duration = 3f; // Random.Range(0f, 100f); if more explroation needed but takes longer training
//    //    //Debug.Log("lights reset with light.step1Duration: " + light.step1Duration);
//    //}
//    //foreach (TrafficCar car in trafficCars)
//    //{
//    //    if (car != null)
//    //    {
//    //        car.ResetStateC();
//    //        car.carSetting.limitSpeed = Random.Range(20f, 35f); //30f;
//    //    }
//    //}
//    //Debug.Log("Reset Environment compeleted in "+ Time.timeSinceLevelLoad);
//    yield break;//otherwise coroutine keeps running regardless of endepisode
//}
//public override void OnEpisodeEnd() //onepisodeend never works, 
//{
//    base.OnEpisodeEnd();

//    // Reset the state of traffic cars
//    foreach (TrafficCar car in trafficCars)
//    {
//        if (car != null)
//        {
//            car.ResetStateC();
//        }
//    }

//    // Reset the state of traffic lights
//    foreach (TrafficLights light in trafficLights)
//    {
//        if (light != null)
//        {
//            light.ResetStateL();
//        }
//    }
//}
//public override void OnEpisodeEnd(bool interrupted)
//{
//    // Stop the ProcessActionsWithDelay coroutine when the episode ends
//    StopCoroutine(ProcessActionsWithDelay());
//}
//public void ResetScene()
//{
//    // Get the current scene index
//    int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;

//    // Reload the current scene
//    UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneIndex);
//}
//public override void Initialize()
//{

//    base.Initialize();

//    // Set the time scale to 1 or other
//    Time.timeScale = 100f;//causing interval of 0.4s timesinceloadinterval between observation-action step...
//    //...whether timescale is 1 or 20 or 100 as long as decision requester is 20
//}