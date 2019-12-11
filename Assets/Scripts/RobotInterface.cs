using System.IO;
using ROSBridgeLib;
using UnityEngine;
using Debug = UnityEngine.Debug;

//The control interface to the robot
//TODO: To be changed to ROS
public class RobotInterface : MonoBehaviour
{
    public enum CommandType
    {
        TurnSeatLeft,
        TurnSeatRight,
        TurnRobotLeft,
        TurnRobotRight,
        DriveRobotForwards,
        DriveRobotReverse,
        ParkingBrakeEngaged,
        ParkingBrakeDisengaged,
        StopRobot
    }

    private string _telerobotConfigPath;

    public static RobotInterface Instance { get; private set; }
    public bool Parked { get; private set; }

    //not quite nice, I have broken the enables/disables in multiple scripts 
    private bool AllowRobotCommands = true;
    public bool IsConnected { get; private set; }
    private bool IsDriving;
    public AnimationCurve SpeedCurve;

    [SerializeField] private int _motorMaxSpeed = 255;
    [SerializeField] private int _motorMinSpeed = 60;
    [SerializeField] private float _commandTimer = 50;

    [SerializeField] private float MaximumLinearVelocity = 0.4f;
    [SerializeField] private float MinimumLinearVelocity = 0.05f;
    [SerializeField] private float MaximumAngularVelocity= 0.8f;
    [SerializeField] private float BackwardsVelocity = 0.1f;

    //these should be in robotcontrolpad maybe , not here as filters. They are part of the UI.
    [SerializeField] private float MaxVelocityHorizon = 0;
    [SerializeField] public float UpperDeadZoneLimit = -0.5f;
    [SerializeField] public float LowerDeadZoneLimit = -0.7f;
    [SerializeField] public float RightDeadZoneLimit = 0.2f;
    [SerializeField] public float LeftDeadZoneLimit = -0.2f;

    [SerializeField] private float LeftBackZoneLimit = -0.2f;
    [SerializeField] private float UpperBackZoneLimit = -0.8f;
    [SerializeField] private string RobotConfigurationFile = "Telerobot_ThetaS.json";

    private string RobotName;
    private float _timer = 0;
    private Vector2 InitRange;
    private Vector2 NewRange;
    private string _portName;
    private bool _left, _right, _forward, _reverse;
    private bool _isStopped;
    private ROSLocomotionDirect _rosLocomotionDirect;
    [HideInInspector]
    public ROSBridgeWebSocketConnection _rosBridge;
    private Telerobot_ThetaFile _telerobotConfigFile;

   

    void Awake()
    {
        Instance = this;
        //The connection details of the robot to drive, retrieved from a json file in the config path.
        _telerobotConfigPath = Application.streamingAssetsPath + "/Config/" + RobotConfigurationFile;
    }

    void Start()
    {
        string robotFileJson = File.ReadAllText(_telerobotConfigPath);
        _telerobotConfigFile = JsonUtility.FromJson<Telerobot_ThetaFile>(robotFileJson);
        IsDriving = false;
        InitRange = new Vector2(UpperDeadZoneLimit , MaxVelocityHorizon);
        NewRange = new Vector2(0,MaximumLinearVelocity);
      
    }

    //changed this from OnApplicationQuit
    public void Quit()
    {
        if (_rosBridge != null)
            _rosBridge.Disconnect();
    }

    private string GetMotorSpeedString(float speed)
    {
        int intIntensity = (int) (SpeedCurve.Evaluate(Mathf.Abs(speed)) * (_motorMaxSpeed - _motorMinSpeed) + _motorMinSpeed);
        if (speed == 0)
            return "000";
        else
            return intIntensity.ToString("000");
    }

    //Commands with eye tracking using the track pad as control
    private void SendTrackpadCommandToRobot(Vector2 controlOutput)
    {
       // Debug.Log("Sending command to robot");
       // Debug.Log("ControlOutput is :" + controlOutput.x +"  " +  controlOutput.y);

        IsDriving = true;
        Vector2 movement = new Vector2(controlOutput.y, -controlOutput.x);

      //  Debug.Log("Intial Linear speed was :" + movement.x + "Initial Angular speed was : " + movement.y);
        //if you are not at the dead zone 
        if (!InsideDeadZone(movement.x, movement.y))
        {
            //normalize speed and send data
            movement = new Vector2(FilterLinearVelocity(movement.x), FilterAngularVelocity(movement.y));
           // Debug.Log("Normalized Linear speed was :" + movement.x + "Normalized Initial Angular speed was : " +  movement.y);

            _rosLocomotionDirect.PublishData(movement.x, movement.y);
            _isStopped = false;
            
        }
        else
        {
          //  Debug.Log("Inside Dead Zone");
           // Debug.Log("Linear speed was :" + 0 + "Angular speed was : " + 0);
            _rosLocomotionDirect.PublishData(0, 0);
            _isStopped = false;
        }

    }

    //Command using linear and angular values 
    private void SendCommandToRobot(Vector2 movement)
    {
        IsDriving = movement != Vector2.zero;

        if (movement.x > MaximumLinearVelocity) movement.x = MaximumLinearVelocity;
        else if (movement.x < 0) movement.x = -BackwardsVelocity;


        if (Mathf.Abs(movement.y) > MaximumAngularVelocity)
        {
            if (movement.y < 0) movement.y = -MaximumAngularVelocity;
            else movement.y = MaximumAngularVelocity;

        }

        _rosLocomotionDirect.PublishData(movement.x, movement.y);
        _isStopped = false;

    }

    //Commands with joystick -- This should not be called every frame for 0 input maybe
    public void DirectCommandRobot(Vector2 JoystickInput)
    {
        //Horizontal = rotation = y , Vertical = LinearMove = x
        Vector2 movement = new Vector2(JoystickInput.y, -JoystickInput.x);

        IsDriving = movement != Vector2.zero;
     
        if (movement.x > MaximumLinearVelocity) movement.x = MaximumLinearVelocity;
        else if (movement.x < 0) movement.x = -BackwardsVelocity;


        if (Mathf.Abs(movement.y) > MaximumAngularVelocity)
        {
            if (movement.y < 0) movement.y = -MaximumAngularVelocity;
            else movement.y = MaximumAngularVelocity;

        }
     
        //Debug.Log("Command move:" + movement);

        if (AllowRobotCommands) { 
            _rosLocomotionDirect.PublishData(movement.x, movement.y);
            _isStopped = false;
        }
    }


    public void StopRobot()
    {
        
        IsDriving = false;
        if (!IsConnected || _isStopped) return;
        _rosLocomotionDirect.PublishData(0, 0);
        _isStopped = true;
    }

    public bool IsRobotDriving()
    {
        return IsDriving;
    }
    public void SendCommand(Vector2 controlOutput)
    {
        
        if (!IsConnected)
        {
            Debug.Log("RobotInterace not connected");
            return;
        }
       
        //if (_timer < _commandTimer / 1000f)
        //{
        //    _timer += Time.deltaTime;
        //    return;
        //}
        _timer = 0;

        if (AllowRobotCommands)
        {
            if (StreamController.Instance.UsingGoToGaze)
                SendCommandToRobot(controlOutput);
            else
                SendTrackpadCommandToRobot(controlOutput);
        }
    }

    public void SetParkingBrake(bool isOn)
    {
        if (isOn)
        {
            GuiController.Instance.SetRobotControlVisibility(false);
            StreamController.Instance.EnableParkedMode();
        }
        else
        {
            StreamController.Instance.DisableParkedMode();
            GuiController.Instance.SetSeatControlVisibility(false);
            VRController.Instance.CenterSeat();
        }
        Parked = isOn;
    }

    public void DoneEnableDrivingMode()
    {
        GuiController.Instance.SetRobotControlVisibility(true);
        Viewport.Instance.SetEnabled(true);
    }

    public void DoneEnableParkMode()
    {
        GuiController.Instance.SetSeatControlVisibility(true);
    }

    public void Connect()
    {
        //adding the ws in the uri is essential : copied this from robotmastercontroller
        if (!_telerobotConfigFile.RosBridgeUri.StartsWith("ws://"))
            _telerobotConfigFile.RosBridgeUri = "ws://" + _telerobotConfigFile.RosBridgeUri;

        //"Telerobot_ThetaS"
        _rosBridge = new ROSBridgeWebSocketConnection(_telerobotConfigFile.RosBridgeUri, _telerobotConfigFile.RosBridgePort, "VirtualRobot_Arlobot");
        _rosLocomotionDirect = new ROSLocomotionDirect(ROSAgent.AgentJob.Publisher, _rosBridge, "/cmd_vel");
        
        _rosBridge.Connect(((s, b) => { Debug.Log(s + " - " + b); }));
        IsConnected = true;
    }

    public float NormalizeValues(float value)
    {

        return (((NewRange.y - NewRange.x) * (value - InitRange.x)) / (InitRange.y - InitRange.x) ) + NewRange.x;

    }

    public float FilterLinearVelocity(float InitValue)
    {
        //From this point and upwards, you move with maximum velocity
        if (InitValue >= MaxVelocityHorizon)
        {
           // Debug.Log("Maximum Velocity");
            return MaximumLinearVelocity;
        }
        //between that point and until the dead zone move with adjusted speed
        else if (InitValue < MaxVelocityHorizon && InitValue > UpperDeadZoneLimit)
        {
            float vel = NormalizeValues(InitValue);
            //Debug.Log("Normalized Velocity )");

            //Return minimum velocity instead of something too close to 0
            if (vel < MinimumLinearVelocity)
            {
                return MinimumLinearVelocity;
            }

            return vel;
        }
        else if (InitValue < LowerDeadZoneLimit)
        {
            return 0;
        }

        return 0;
    }

    public float FilterAngularVelocity(float InitValue)
    {
        if (Mathf.Abs(InitValue) > MaximumAngularVelocity)
        {
            return InitValue > 0 ? MaximumAngularVelocity : -MaximumAngularVelocity; 
           
        }

        //TODO normalize here a bit more?
        return InitValue;
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

    //returns true if we are in the bounding box of the dead zone
    private bool InsideBackZone(float x, float y)
    {
        //leftbackzone is negative
        if (x > LeftBackZoneLimit && x <= -LeftBackZoneLimit
            && y <UpperBackZoneLimit)
            return true;

        return false;
    }

    public void EnableRobotCommands(bool enable)
    {
        AllowRobotCommands = enable;
        StopRobot();
    }
}