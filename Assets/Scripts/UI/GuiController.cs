using System.Collections;
using UnityEngine;
using UnityEngine.UI;

//Collects and controls all GUI elements such as keyboard, command view and chat.
public class GuiController : MonoBehaviour
{
    public static GuiController Instance { get; private set; }

    [Header("Interface")] [SerializeField] private float _interfaceDistance = 4.5f;
    [SerializeField] private float _screenDiameter = 100;
    [SerializeField] private float _screenAspectRatio = 1.77777f;
    [SerializeField] private Vector2 _rearViewSizeMultiplier = new Vector2(1, 1);
    [SerializeField] private Vector2 _downViewSizeMultiplier = new Vector2(1, 1);
    [SerializeField] private Transform _seatControlsInterfaceContainer;
    [SerializeField] private Transform _staticControlsInterfaceContainer;
    [SerializeField] private GazeButton _toggleViewport;

    [Header("RobotControl")] [SerializeField] private float _robotControlTrackPadDiameter = 100;
    [SerializeField] private float _robotControlTrackPadAspectRatio = 1.77777f;
    [SerializeField] private float _robotControlsInterfaceHorAngleToScreen = 20;
    [SerializeField] private float _robotControlsInterfaceVerAngleToScreen = 10;
    [SerializeField] private float _maxTurnAmount = 0.4f;
    [SerializeField] private ParkingBrakeButton _robotParkingBrake;
    [SerializeField] private RobotControlTrackPad _robotControlTrackPad;
    [SerializeField] private GoToGazeSphere _goToGazeSphere;
    [SerializeField] private GazeButton _toggleControlOverlay;
    [SerializeField] private RectTransform _viewportDownCanvas;
    [SerializeField] private RectTransform _viewportRearCanvas;

    [Header("SeatControls")] [SerializeField] private float _seatControlsInterfaceAngleToScreen = 10;
    [SerializeField] private float _seatRotationSpeed = 1;
    [SerializeField] private Image _turnSeatLeft;
    [SerializeField] private Image _turnSeatRight;
    [SerializeField] private Color _arrowColor;
    [SerializeField] private Color _arrowColorActive;

    private CommandListController _cmdController;
    private KeyboardController _keyboardController;
    private ChatController _chatController;
    private RectTransform _viewportFrontCanvas;
    private bool _turningRobotLeft, _turningRobotRight, _drivingRobotForwards, _drivingRobotReverse;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _cmdController = CommandListController.Instance;
        _keyboardController = KeyboardController.Instance;
        _chatController = ChatController.Instance;
        _viewportFrontCanvas = Viewport.Instance.ViewportCanvas.GetComponent<RectTransform>();
        _viewportFrontCanvas = Viewport.Instance.ViewportCanvas.GetComponent<RectTransform>();
        PositionInterfaceElements();

        _robotParkingBrake.Activated += OnRobotParkingBrakeActivated;
        _toggleViewport.Activated += OnToggleViewportActivated;
        _robotControlTrackPad.Activated += OnTrackpadInteracted;
        _robotControlTrackPad.Unhovered += OnTrackpadUnhovered;
        _toggleControlOverlay.Unhovered += OnToggleControlOverlayActivated;

        iTween.MoveTo(gameObject, new Hashtable {{"oncomplete", "HideUI"}, {"time", 2}});
    }

    void Update()
    {
        CheckControls();
    }

    void OnRobotParkingBrakeActivated(GazeObject sender)
    {
        RobotInterface.Instance.SetParkingBrake(sender.IsActivated);
    }

    void OnToggleViewportActivated(GazeObject sender)
    {
        StreamController.Instance.SetViewportVisibility(sender.IsActivated);
    }

    void OnToggleControlOverlayActivated(GazeObject sender)
    {
        _robotControlTrackPad.SetOverlayVisibility(sender.IsActivated);
    }

    void OnTrackpadInteracted(GazeObject sender)
    {
        if (sender.IsActivated)
            StreamController.Instance.EnableDrivingMode();
    }

    void OnTrackpadUnhovered(GazeObject sender)
    {
        StreamController.Instance.DisableDrivingMode();
    }

    /// <summary>
    /// Positions the screens and all UI elements that are positioned relative to the screen. 
    /// </summary>
    private void PositionInterfaceElements()
    {
        //Calculate size of screen and robot controls
        float h = Mathf.Pow(_screenDiameter, 2) / (Mathf.Pow(_screenAspectRatio, 2) + 1);
        h = Mathf.Sqrt(h);
        float w = Mathf.Sqrt(Mathf.Pow(_screenDiameter, 2) - Mathf.Pow(h, 2));
        Vector2 screenSize = new Vector2(w, h);
        _viewportFrontCanvas.sizeDelta = screenSize;
        _viewportDownCanvas.sizeDelta = Vector2.Scale(screenSize, _downViewSizeMultiplier);
        _viewportDownCanvas.position = _viewportFrontCanvas.position + new Vector3(0, _viewportFrontCanvas.sizeDelta.y / 2, 0);
        _viewportRearCanvas.sizeDelta = Vector2.Scale(screenSize, _rearViewSizeMultiplier);
        _viewportRearCanvas.position = _viewportFrontCanvas.position - new Vector3(0, _viewportFrontCanvas.sizeDelta.y / 2, 0);

//        h = Mathf.Pow(_robotControlTrackPadDiameter, 2) / (Mathf.Pow(_robotControlTrackPadAspectRatio, 2) + 1);
//        h = Mathf.Sqrt(h);
//        w = Mathf.Sqrt(Mathf.Pow(_robotControlTrackPadDiameter, 2) - Mathf.Pow(h, 2));
//        _robotControlTrackPad.SetSize(new Vector2(w, h));
//        _robotControlTrackPad.SetOverlayVisibility(false);

        Quaternion orgRot = _viewportFrontCanvas.root.rotation;

        //Positions controls elements relating to different screen positions
        _viewportFrontCanvas.root.localEulerAngles = orgRot.eulerAngles + new Vector3(0, -_seatControlsInterfaceAngleToScreen, 0);
        Vector3 seatLeftSide = _viewportFrontCanvas.position + (-_viewportFrontCanvas.right * _viewportFrontCanvas.sizeDelta.x / 2);
        seatLeftSide = (seatLeftSide - _viewportFrontCanvas.root.position).normalized * _interfaceDistance;

        _viewportFrontCanvas.root.localEulerAngles = orgRot.eulerAngles + new Vector3(0, _seatControlsInterfaceAngleToScreen, 0);
        Vector3 seatRightSide = _viewportFrontCanvas.position + (_viewportFrontCanvas.right * _viewportFrontCanvas.sizeDelta.x / 2);
        seatRightSide = (seatRightSide - _viewportFrontCanvas.root.position).normalized * _interfaceDistance;

        _viewportFrontCanvas.root.rotation = orgRot;

        _turnSeatLeft.transform.parent.position = _viewportFrontCanvas.root.position + seatLeftSide;
        _turnSeatLeft.transform.parent.LookAt(2 * seatLeftSide - _viewportFrontCanvas.root.position);
        _turnSeatRight.transform.parent.position = _viewportFrontCanvas.root.position + seatRightSide;
        _turnSeatRight.transform.parent.LookAt(2 * seatRightSide - _viewportFrontCanvas.root.position);
    }

    /// <summary>
    /// Checks player's head orientation to rotate seat
    /// </summary>
    private void CheckControls()
    {
        if (!RobotInterface.Instance.Parked) return;

        float headDirHor = AngleDir(VRController.Instance.Head.forward.normalized, VRController.Instance.transform.forward.normalized, transform.up);

        bool rotateSeatLeft, rotateSeatRight;
        rotateSeatLeft = rotateSeatRight = false;

        Vector3 seatLeftHeading = (_turnSeatLeft.transform.parent.position - _viewportFrontCanvas.root.position).normalized;
        float directionToLeft = AngleDir(seatLeftHeading, VRController.Instance.transform.forward.normalized, transform.up);
        Vector3 seatRightHeading = (_turnSeatRight.transform.parent.position - _viewportFrontCanvas.root.position).normalized;
        float directionToRight = AngleDir(seatRightHeading, VRController.Instance.transform.forward.normalized, transform.up);

        if (headDirHor > directionToLeft)
        {
            VRController.Instance.RotateSeat((headDirHor - directionToLeft) * Time.deltaTime * headDirHor * -_seatRotationSpeed);
            rotateSeatLeft = true;
        }
        else if (headDirHor < directionToRight)
        {
            VRController.Instance.RotateSeat((headDirHor - directionToRight) * Time.deltaTime * headDirHor * _seatRotationSpeed);
            rotateSeatRight = true;
        }

        _turnSeatLeft.color = rotateSeatLeft ? _arrowColorActive : _arrowColor;
        _turnSeatRight.color = rotateSeatRight ? _arrowColorActive : _arrowColor;
    }

    private float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up)
    {
        Vector3 perp = Vector3.Cross(fwd, targetDir);
        float dir = Vector3.Dot(perp, up);
        return dir;
    }

    public void HideUI()
    {
        _cmdController.HideCommandView();
        _keyboardController.HideKeyboard();
        _chatController.HideChat();
        SetInterfaceVisibility(false);
        SetRobotControlVisibility(false);
        SetDrivingControlsVisibility(false);
    }

    public void ShowUI()
    {
        _cmdController.ShowCommandView();
        _keyboardController.ShowKeyboard();
        _chatController.ShowChat();
        SetInterfaceVisibility(true);
    }

    public void SetRobotControlVisibility(bool isVisible)
    {
        _goToGazeSphere.gameObject.SetActive(isVisible);
//        _robotControlTrackPad.gameObject.SetActive(isVisible);
    }

    public void SetSeatControlVisibility(bool isVisible)
    {
        _turnSeatLeft.transform.parent.gameObject.SetActive(isVisible);
        _turnSeatRight.transform.parent.gameObject.SetActive(isVisible);
    }

    public void SetInterfaceVisibility(bool isVisible)
    {
        SetSeatControlVisibility(isVisible);
    }

    public void SetDrivingControlsVisibility(bool isVisible)
    {
        _viewportRearCanvas.gameObject.SetActive(isVisible);
        _viewportDownCanvas.gameObject.SetActive(isVisible);
    }

    public void ToggleKeyboard()
    {
        _keyboardController.ToggleKeyboard();
        _robotControlTrackPad.SetEnabled(_keyboardController.IsActive);
    }
}