using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using ROSBridgeLib;
using UnityEngine;

//Controls the player experience and camera input
public class StreamController : MonoBehaviour
{
    public enum ControlType
    {
        Head,
        Mouse,
        Eyes,
        Eyes_Mouse,
        Joystick
    }

    public static StreamController Instance { get; private set; }

    [SerializeField] public ControlType _selectedControlType = ControlType.Head;
    [SerializeField] public bool VirtualEnvironment = false;

    //public VirtualRobot VirtualRobotController;

    [Header("Cameras and Projection")]
    [SerializeField] private bool FeedbackFromCamera = true;
    [SerializeField] private ThetaWebcamStream _cameraStreamUSB;
    [SerializeField] private QuestionManager _queryManager;
    [SerializeField] private MeshRenderer _icosphere;
    [SerializeField] private MeshRenderer _icosphereDissolve;
    [SerializeField] private MeshRenderer _projectionSphere;
    [SerializeField] private float _sphereDissolveSpeed = 1;
    [SerializeField] private GameObject _projectionCamera;
    [SerializeField] private Vector3 _drivingModeRotationalOffset;
    [SerializeField] private FollowObject _projectionCameraFrontFollowObject;

    [Header("Chair Variables")]
    [SerializeField] private Transform _chair;
    [SerializeField] private Transform _chairFOVE;
    [SerializeField] private Transform _chairEndPosition;
    [SerializeField] private FollowObject _rotatingControlsFollow;
    [SerializeField] private FollowObject _seatInterfaceFollow;

    [Space(5)] [Header("Lights")]
    [SerializeField] private List<Light> _domePerimiterLights;
    [SerializeField] private Light _domeTopLight;

    public Transform ActiveChair { get; private set; }

    private enum CockpitState
    {
        Loading,
        Ready
    }


    private ROSBridgeWebSocketConnection rosbridge;
    private bool _isLooping;
    private bool _isConnected;

    private bool _useFOVE;
    private float _currentChairSpeed;
    private float _accelTimer;
    private CockpitState _currentCockpitState = CockpitState.Ready;

    void Awake()
    {
        Instance = this;
        //still use the FOVE even with joystick
        if (_selectedControlType == ControlType.Eyes || _selectedControlType == ControlType.Head || _selectedControlType == ControlType.Joystick) _useFOVE = true;
        ActiveChair = (_useFOVE) ? _chairFOVE : _chair;
        _chair.gameObject.SetActive(!_useFOVE);
        _chairFOVE.gameObject.SetActive(_useFOVE);
        _rotatingControlsFollow.ObjectToFollow = ActiveChair;
        _seatInterfaceFollow.ObjectToFollow = ActiveChair;
        VRController.Instance.Initialize(_selectedControlType);
    }

    void Start()
    {
        //Viewport.Instance.SetFollowTarget(VRController.Instance.Head);
    }

    private void SetupVirtualRobot()
    {

        string _configPath = Application.streamingAssetsPath + "/Config/Robots/";
        string[] paths = Directory.GetFiles(_configPath, "VirtualRobot_Arlobot.json");
        string path = paths[0];


        string robotName = Path.GetFileNameWithoutExtension(path);
        Debug.Log(robotName);
        string robotFileJson = File.ReadAllText(path);
        RobotConfigFile robotFile = JsonUtility.FromJson<RobotConfigFile>(robotFileJson);


        if (!robotFile.RosBridgeUri.StartsWith("ws://"))
            robotFile.RosBridgeUri = "ws://" + robotFile.RosBridgeUri;


        // rosbridge = new ROSBridgeWebSocketConnection(robotFile.RosBridgeUri, robotFile.RosBridgePort, robotName);

        //VirtualRobotController.InitialiseRobot(RobotInterface.Instance._rosBridge, robotFile, robotName);
        // VirtualRobotController.OverridePositionAndOrientation(VirtualRobotController.gameObject.transform.position, VirtualRobotController.gameObject.transform.rotation);
    }

    public void Update()
    {
        //manual connect to robot and ascend chair instead of gaze track the button
        if (!_isConnected)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                Debug.Log("Test");
                ConnectToRobot();
                //SetupVirtualRobot();

            }
        }



    }

    /// <summary>
    /// Runs connecting animation until connection is established.
    /// Then moves the chair into the cockpit.
    /// </summary>
    private IEnumerator LoadCockpit()
    {
        while (_currentCockpitState != CockpitState.Ready)
        {

            if (_isConnected)
            {
                //TODO: fade anim so it's not as abrupt
                _currentCockpitState = CockpitState.Ready;
                ActiveChair.position = _chairEndPosition.position;
                StartCoroutine(StartUpSequence());
            }
            else
            {
                //TODO: connecting anim
                Debug.Log("Connecting...");
            }

            yield return new WaitForEndOfFrame();
        }
    }

    /// <summary>
    /// Starts the startup animation for lights and screens
    /// </summary>
    private IEnumerator StartUpSequence()
    {
        yield return new WaitForSeconds(1f);
        _domeTopLight.enabled = true;

        foreach (Light light in _domePerimiterLights)
        {
            yield return new WaitForSeconds(0.5f);
            light.enabled = true;
        }

        yield return new WaitForSeconds(0.75f);
        Viewport.Instance.SetEnabled(true);
        GuiController.Instance.SetRobotControlVisibility(true);
        yield return new WaitForSeconds(0.75f);
    }

    /// <summary>
    /// Plays animation of lights and screens for parking robot
    /// </summary>
    private IEnumerator ParkedModeSequence()
    {
        foreach (Light light in _domePerimiterLights)
        {
            light.enabled = false;
        }
        _domeTopLight.enabled = false;
        yield return new WaitForSeconds(1f);
        _icosphereDissolve.enabled = true;
        _icosphereDissolve.material.SetFloat("_Cutoff", 0);
        _icosphere.enabled = false;
        _projectionSphere.enabled = true;

        float dissolve = 0;
        while (dissolve < 1)
        {
            dissolve += Time.deltaTime * _sphereDissolveSpeed;
            _icosphereDissolve.material.SetFloat("_Cutoff", dissolve);
            yield return new WaitForEndOfFrame();
        }
        RobotInterface.Instance.DoneEnableParkMode();
    }

    /// <summary>
    /// Plays animation of lights and screes for disabling parked mode
    /// </summary>
    private IEnumerator DriveModeSequence()
    {
        float dissolve = 1;
        while (dissolve > 0)
        {
            dissolve -= Time.deltaTime * _sphereDissolveSpeed;
            _icosphereDissolve.material.SetFloat("_Cutoff", dissolve);
            yield return new WaitForEndOfFrame();
        }
        _projectionSphere.enabled = false;
        _icosphere.enabled = true;
        _icosphereDissolve.enabled = false;
        yield return new WaitForSeconds(1f);
        _domeTopLight.enabled = true;
        foreach (Light light in _domePerimiterLights)
        {
            light.enabled = true;
        }

        RobotInterface.Instance.DoneEnableDrivingMode();
    }

    private void StartVideoPlayer()
    {
    }

    /// <summary>
    /// Connects to USB 360 camera and starts up animations
    /// </summary>
    public void ConnectToRobot()
    {
        if (FeedbackFromCamera)
            _cameraStreamUSB.StartStream();
        else { }

        //_currentChairState = ChairState.Accelerating;
        //StartCoroutine(AscendChair());
        //TODO: delete anything related to the ascending chair anim
        _currentCockpitState = CockpitState.Loading;
        StartCoroutine(LoadCockpit());

        //connect to the appropriate controller
        if (VirtualEnvironment)
        {
            Debug.Log("connecting to the appropriate controller");
            VirtualUnityController.Instance.Connect();
        }
        else
        {
            Debug.Log("NOT connecting to the appropriate controller");
            RobotInterface.Instance.Connect();
        }

        //what about older projects?
        if (_queryManager)
        {
            _queryManager.EnableManager();
        }

        if (GazeTrackingDataManager.Instance)
            GazeTrackingDataManager.Instance.EnableManager();

        //TODO: When online connection is put in, uncomment this
        //_isLooping = true;
        _isConnected = true;
    }

    public void RotateChair(float deltaAngle)
    {
        ActiveChair.eulerAngles = new Vector3(0, ActiveChair.eulerAngles.y + deltaAngle, 0);
    }


    public void EnableParkedMode()
    {
        StartCoroutine(ParkedModeSequence());
        SetViewportVisibility(false);
    }

    public void DisableParkedMode()
    {
        StartCoroutine(DriveModeSequence());
    }

    public void SetViewportVisibility(bool isVisible)
    {
        Viewport.Instance.SetEnabled(isVisible);
    }

    public void EnableDrivingMode()
    {
        Viewport.Instance.LockScreenToCenter();
        _projectionCameraFrontFollowObject.SetRotationalOffset(_drivingModeRotationalOffset);
        GuiController.Instance.SetDrivingControlsVisibility(true);
    }

    public void DisableDrivingMode()
    {
        Viewport.Instance.UnlockScreen();
        _projectionCameraFrontFollowObject.SetRotationalOffset(Vector3.zero);
        GuiController.Instance.SetDrivingControlsVisibility(false);
    }
}