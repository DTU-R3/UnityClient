using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using EZCameraShake;
using UnityEngine;


/*
 *This controller is directly attached to the Unity virtual arlobot without connecting through ROS interface or 360 camera.
 *Simple velocity commands and rotation received from the VRController depending on the input.
 * Any change that you want to do for driving, logic for when the padbot collides with somethings, sound effects etc do it here.
 * This was copied from the RobotInterface script and modified to fit the needs.
     
 */
public class VirtualUnityController : MonoBehaviour {

    public static VirtualUnityController Instance { get; private set; }

    [SerializeField] private float CommandDelay = 0.5f;

    [SerializeField] private float MaxLinearVelocity = 0.4f;
    [SerializeField] private float MinLinearVelocity = 0.05f;
    [SerializeField] private float MaxAngularVelocity = 0.8f;
    [SerializeField] private float BackwardsVelocity = 0.3f;

    [HideInInspector] public bool IsActive = false;
    private Rigidbody VirtualBot;

    //these should be in robotcontrolpad maybe , not here as filters. They are part of the UI.
    [SerializeField] private float MaxVelocityHorizon = 0;
    [SerializeField] public float UpperDeadZoneLimit = -0.5f;
    [SerializeField] public float LowerDeadZoneLimit = -0.7f;
    [SerializeField] public float RightDeadZoneLimit = 0.2f;
    [SerializeField] public float LeftDeadZoneLimit = -0.2f;

    [SerializeField] private float LeftBackZoneLimit = -0.2f;
    [SerializeField] private float UpperBackZoneLimit = -0.8f;


    [Header("Audiosources")]
    public AudioSource DriveSoundSource;
    public AudioSource ObstacleSoundSourceCont;
    public AudioSource ObstacleSoundSourceOnce;

    [Space(10)]
    [Header("Soundclips")]
    public AudioClip DriveSlow;
    public AudioClip DriveFast;
    public AudioClip OnCollisionObstacle;
    public AudioClip DuringCollisionObstacle;
    public AudioClip OnCollisionWall;
    public AudioClip DuringCollisionWall;


    private Vector2 InitRange;
    private Vector2 NewRange;
    private float TargetRotLow;
    private float TargetRotHigh;
    private float TargetRot;
    private float InitRot;
    private bool InitialShake;
    bool balancing = false;
    private float val = 0.0f;
    private float rotateSpeed = 0.01f;
    private bool JoystickStopped;
    private bool cmdStarted,delayEvaluated;
    private List<Vector2> cmdList;
    private bool Colliding;
    private bool DrivingFast;

    void Awake()
    {
        //DriveSoundSource = gameObject.GetComponent<AudioSource>();
        Instance = this;
        Colliding = false;
        DrivingFast = false;
        cmdList = new List<Vector2>();
        cmdStarted = delayEvaluated = JoystickStopped = false;
    }

    // Use this for initialization
    void Start ()
    {
        InitRange = new Vector2(UpperDeadZoneLimit, MaxVelocityHorizon);
        NewRange = new Vector2(0, MaxLinearVelocity);
        VirtualBot = this.gameObject.GetComponent<Rigidbody>();
        TargetRotLow = 0.0f;
        TargetRotHigh = 359.0f;
        InitRot =  0.0f;
        InitialShake = false;
        

        StartCoroutine("EvaluateCommand");
    }
	
	// Update is called once per frame
	void Update ()
	{

        //Insert the following in a function for clarity and readability
	    float CurrentAngle = gameObject.transform.eulerAngles.x;
	    //Debug.Log(CurrentAngle);

        //if the list is populated from not being populated before let the script know
	    if (cmdList.Count > 0 && !cmdStarted && !delayEvaluated)
	    {
	        cmdStarted = true;
	    }
        

	    if (CurrentAngle > 2.0f && CurrentAngle < 10.0f && !balancing)
	    {
	        balancing = true;
	        val = 0.0f;
	        InitRot = CurrentAngle;
	        TargetRot = TargetRotLow;
	    }
	    else if (CurrentAngle < 358.0f && CurrentAngle > 340.0f && !balancing)
	    {
	        balancing = true;
	        val = 0.0f;
	        InitRot = CurrentAngle;
	        TargetRot = TargetRotHigh;
	    }

        if (balancing)
	    {
	        val = val + rotateSpeed;
	        float angle = Mathf.Lerp(InitRot, TargetRot, val);
            //rotate only x axis, ugly but it works
	        transform.rotation = Quaternion.Euler(angle, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z);

	        if (val >= 1.0f)
	        {
	            balancing = false;
	        }
        }
        //--------------------------------- Balancing logic end---------------

	   



	}

    public void JoystickCommand(Vector2 input)
    {
        if (IsActive)
        {
            TimeLogger.Instance.Start(TimeLogger.TimeLogType.Virtual); //Start time logging

            Vector2 movement = new Vector2(input.y, input.x);

           

            //correct
            if (movement.Equals(new Vector2(0, 0)) && !JoystickStopped){
                cmdList.Add(Vector2.zero);
                JoystickStopped = true;
                InitialShake = false;
               // DriveSoundSource.Stop();
            }

            if (!movement.Equals(new Vector2(0, 0)))
            {
                JoystickStopped = false;
                if (!InitialShake)
                {
                   // CameraShaker.Instance.ShakeOnce(1f, 1f, 0.2f, 0.2f);
                    InitialShake = true;
                }
                cmdList.Add(new Vector2(FilterJoystickLinearVelocity(movement.x),FilterJoystickAngularVelocity(movement.y)));
            }

        }
    }


    //collisions with objects alternate between sound clips and also shake the screen and slow the object.
    void OnCollisionEnter(Collision collision)
    {

        if (collision.gameObject.CompareTag("Obstacle") )
        {
            CameraShaker.Instance.ShakeOnce(2f, 2f, 0.5f, 0.5f);
            VirtualBot.drag = 10.0f;
           // ChangePlayOnceClip(0);
            ChangeObstacleClip(0);
        }
        else if (collision.gameObject.CompareTag("Wall"))
        {
            CameraShaker.Instance.ShakeOnce(2f, 2f, 0.5f, 0.5f);
            VirtualBot.drag = 10.0f;
            ChangePlayOnceClip(1);
            //ChangeObstacleClip(1);
        }

    }

    void OnCollisionStay(Collision collision)
    {

        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Wall"))
        {
            Colliding = true;
        }
        

        //CameraShaker.Instance.ShakeOnce(2f, 2f, 0.5f, 0.5f);
        //Debug.Log("Collided!");
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            CameraShaker.Instance.ShakeOnce(2f, 2f, 0.5f, 0.5f);
            VirtualBot.drag = 0.0f;
            ObstacleSoundSourceCont.Stop();
            Colliding = false;
        }
        else if (collision.gameObject.CompareTag("Wall"))
        {
            CameraShaker.Instance.ShakeOnce(2f, 2f, 0.5f, 0.5f);
            VirtualBot.drag = 10.0f;
            ObstacleSoundSourceCont.Stop();
            Colliding = false;
        }


    }

    public void GazeCommand(Vector2 input)
    {

        if (IsActive)
        {
            TimeLogger.Instance.Start(TimeLogger.TimeLogType.Virtual); //Start time logging

            //map correctly x axis to angular and y axis to linear from the input of the gazepad.
            // no need to reverse 
            Vector2 command = new Vector2(input.y, input.x);
            //StartCoroutine(DelayCommand());
            if (!InsideDeadZone(command.x, command.y))
            {
                cmdList.Add(new Vector2(FilterLinearVelocity(command.x), FilterAngularVelocity(command.y)));
                //normalize speed and send data

                
            }
            else
            {
                cmdList.Add(new Vector2(0,0));
            }
        }
    }


    //TODO This can probably be optimized without using boolean flags but with issuing events. 
    //No time to fix it further though
    // We have a functional feature
    IEnumerator EvaluateCommand()
    {
        //while there are commands in the list
        while (true)
        {
            //when the list has at least one element, delay executing commands for a brief time
            if (cmdStarted && !delayEvaluated)
            {
                yield return new WaitForSeconds(CommandDelay);
                delayEvaluated = true;
                CameraShaker.Instance.ShakeOnce(1f, 1f, 0.2f, 0.2f);
                //Play continous sound here
               
                DriveSoundSource.Play();
                if (Colliding) ObstacleSoundSourceCont.Play();
                //shake here
            }
            //start evaluating now after the initial delay
            if (delayEvaluated) { 

                Vector2 command = cmdList[0];
                cmdList.RemoveAt(0);
               
                ChangeDriveClip(command.x);
                //if the command list emptied
                if (cmdList.Count == 0)
                {
                    cmdStarted = false;
                    delayEvaluated = false;
                    DriveSoundSource.Stop();
                    ObstacleSoundSourceCont.Stop();
                }


                VirtualBot.velocity = this.gameObject.transform.right * command.x;
               
                this.gameObject.transform.Rotate(this.gameObject.transform.up,
                    Mathf.Rad2Deg * Time.deltaTime * command.y, Space.World);
            }

            yield return null; 
        }
    }

    private void ChangeDriveClip(float velocity)
    {
        //Issue with changing the clip too much or pressing play, needs flags to check if states change
        if (velocity > 0.2 && !DrivingFast)
        {
            DrivingFast = true;
            DriveSoundSource.clip = DriveFast;
            DriveSoundSource.Play();
        }
        else if (velocity < 0.2 && DrivingFast)
        {
            DriveSoundSource.clip = DriveSlow;
            DrivingFast = false;
            DriveSoundSource.Play();
        }
        
    }

    private void ChangePlayOnceClip(int obstacletype)
    {
        switch (obstacletype)
        {
            //Obstacle
            case (0):
                ObstacleSoundSourceOnce.PlayOneShot(OnCollisionObstacle);
               // ObstacleSoundSourceOnce.clip = OnCollisionObstacle;
                break;
             //wall
            case (1):
                ObstacleSoundSourceOnce.PlayOneShot(OnCollisionWall);
               // ObstacleSoundSourceOnce.clip = OnCollisionWall;
                break;
            case (2):
                break;
            default:
                ObstacleSoundSourceOnce.PlayOneShot(OnCollisionObstacle);
                break;

        }
       
    }

    private void ChangeObstacleClip(int obstacletype)
    {
        switch (obstacletype)
        {
            //Obstacle
            case (0):
                ObstacleSoundSourceCont.clip = DuringCollisionObstacle;
                break;
            //wall
            case (1):
                ObstacleSoundSourceCont.clip = DuringCollisionWall;
                break;
            case (2):
                break;
            default:
                ObstacleSoundSourceCont.clip = DuringCollisionObstacle;
                break;

        }
        ObstacleSoundSourceCont.Play();
    }

    private float FilterLinearVelocity(float vel)
    {
        //From this point and upwards, you move with maximum velocity
        if (vel >= MaxVelocityHorizon)
        {
            // Debug.Log("Maximum Velocity");
            return MaxLinearVelocity;
        }
        //between that point and until the dead zone move with adjusted speed
        else if (vel < MaxVelocityHorizon && vel > UpperDeadZoneLimit)
        {
            float velnorm = NormalizeValues(vel);

            //Return minimum velocity instead of something too close to 0
            if (velnorm < MinLinearVelocity)
            {
                return MinLinearVelocity;
            }
            return velnorm;
        }
        else if (vel < LowerDeadZoneLimit)
        {
            return 0;
        }

        return 0;
    }

    private float FilterAngularVelocity(float vel)
    {
        if (Mathf.Abs(vel) > MaxAngularVelocity)
        {
            return vel > 0 ? MaxAngularVelocity : -MaxAngularVelocity;

        }

        //TODO normalize here a bit more?
        return vel;
    }

    private float FilterJoystickLinearVelocity(float vel)
    {
        if (vel > MaxLinearVelocity)
            return  MaxLinearVelocity;
        else if (vel < 0)
            return -BackwardsVelocity;

        else return vel;
    }

    private float FilterJoystickAngularVelocity(float vel)
    {
        if (Mathf.Abs(vel) > MaxAngularVelocity)
        {
            if (vel < 0) return -MaxAngularVelocity;
            else return MaxAngularVelocity;
        }

        return vel;
    }

    public void StopRobot()
    {
        VirtualBot.velocity = Vector3.zero;
        VirtualBot.angularVelocity = Vector3.zero;
    }

    public float NormalizeValues(float value)
    {

        return (((NewRange.y - NewRange.x) * (value - InitRange.x)) / (InitRange.y - InitRange.x)) + NewRange.x;

    }
    //allow the commands to control the virtual robot
    public void Connect()
    {
        IsActive = true;
    }
    //remove control over virtual robot
    public void Disconnect()
    {
        IsActive = false;
    }

    //returns true if we are in the bounding box of the dead zone
    private bool InsideDeadZone(float x, float y)
    {
        if (y > LeftDeadZoneLimit && y <= RightDeadZoneLimit
            && x > LowerDeadZoneLimit && x < UpperDeadZoneLimit)
        {
            return true;
        }


        return false;
    }

}
