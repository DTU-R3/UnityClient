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

    public bool IsConnected { get; private set; }
    public AnimationCurve SpeedCurve;

    [SerializeField] private int _motorMaxSpeed = 255;
    [SerializeField] private int _motorMinSpeed = 60;
    [SerializeField] private float _commandTimer = 50;

    private float _timer = 0;
    private string _portName;
    private bool _left, _right, _forward, _reverse;
    private bool _isStopped;
    private ROSLocomotionDirect _rosLocomotionDirect;
    private ROSBridgeWebSocketConnection _rosBridge;
    private Telerobot_ThetaFile _telerobotConfigFile;

    void Awake()
    {
        Instance = this;
        _telerobotConfigPath = Application.streamingAssetsPath + "/Config/Telerobot_ThetaS.json";
    }

    void Start()
    {
        string robotFileJson = File.ReadAllText(_telerobotConfigPath);
        _telerobotConfigFile = JsonUtility.FromJson<Telerobot_ThetaFile>(robotFileJson);

       // Debug.log(_telerobotConfigFile);
    }

    void OnApplicationQuit()
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

    private void SendCommandToRobot(Vector2 controlOutput)
    {
        Vector2 movement = new Vector2(controlOutput.y, -controlOutput.x);
        _rosLocomotionDirect.PublishData(movement.x, movement.y);
        _isStopped = false;
    }

    public void StopRobot()
    {
        if (!IsConnected || _isStopped) return;
        _rosLocomotionDirect.PublishData(0, 0);
        _isStopped = true;
    }

    public void SendCommand(Vector2 controlOutput)
    {
        if (!IsConnected) return;
        if (_timer < _commandTimer / 1000f)
        {
            _timer += Time.deltaTime;
            return;
        }
        _timer = 0;

        SendCommandToRobot(controlOutput);
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

        _rosBridge = new ROSBridgeWebSocketConnection(_telerobotConfigFile.RosBridgeUri, _telerobotConfigFile.RosBridgePort, "Telerobot_ThetaS");
        _rosLocomotionDirect = new ROSLocomotionDirect(ROSAgent.AgentJob.Publisher, _rosBridge, "/cmd_vel");
        _rosBridge.Connect(((s, b) => { Debug.Log(s + " - " + b); }));
        IsConnected = true;
    }
}