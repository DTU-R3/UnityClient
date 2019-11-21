using UnityEngine;
using UnityEngine.UI;

//Trackpad GUI Element that is used to control robot
class RobotControlTrackPad : GazeObject
{
    [SerializeField] private Image _background;
    [SerializeField] private SpriteRenderer _border;
    [SerializeField] private Text _text;
    [SerializeField] private Color _backgroundColor;
    [SerializeField] private Color _borderColor;
    [SerializeField] private Color _borderActiveColor;
    [SerializeField] private Color _borderGrazePeriodColor;
    [SerializeField] private Color _borderLowPeriodColor;

    [Header("Interface")] [SerializeField] private Vector2 _zeroOffset;
    [SerializeField] private Vector2 _deadZone;
    [SerializeField] private float _centerZoneSize = 0.1f;

    [Header("Overlay")] [SerializeField] private RectTransform _overlayContainer;
    [SerializeField] private RectTransform _horizontalBar;
    [SerializeField] private RectTransform _verticalBar;

    [Header("Timers")] [SerializeField] private float _grazePeriod = 2f;
    [SerializeField] private float _lowTimerPeriod = 5f;
    [SerializeField] private float _grazePeriodTimer = 0;
    [SerializeField] private float _lowTimerPeriodTimer = 1.5f;
    [SerializeField] private float MaxUnhoverTimer = 1.0f;

    public bool DisconnectFromRobot = false;

    
    private string _orgText;
    private float _orgDwellTime;
    private float _grazeTimer = 10;
    
    //change names for these
    private float unhoveredTimer = 0;
    private bool ExitingDriveMode = false;

    //Test with gaze vector
    [SerializeField] private float dist_limit_z1 = 0.1f;
    [SerializeField] private float dist_limit_z2 = 5f;
    [SerializeField] private float dist_limit_x1 = 0.3f;
    [SerializeField] private float dist_limit_x2 = 3f;

    [SerializeField] private float speed_limit_x1 = 0.001f;
    [SerializeField] private float speed_limit_x2 = 1f;
    // Exponential function parameters for horizontal acceleration
    private float fx_a;
    private float fx_b;


    protected override void Awake()
    {
        base.Awake();
        _background.color = _backgroundColor;
        _border.color = _borderColor;
        _orgText = _text.text;
        _orgDwellTime = _dwellTime;


        // Exponential function parameters for horizontal acceleration
        fx_a = Mathf.Pow(speed_limit_x2 / speed_limit_x1, 1 / (dist_limit_x2 - dist_limit_x1));
        fx_b = speed_limit_x1 / (Mathf.Pow(fx_a, dist_limit_x1));
    }

    protected override void Update()
    {

        if (DisconnectFromRobot) {  RobotInterface.Instance.Quit();
            DisconnectFromRobot = false;
        }

        //if the user quickly unhovered start 
        if (ExitingDriveMode)
        {
            unhoveredTimer += Time.deltaTime;
        }
        //call functionality of unhover if the user stared out of the panel for longer than max unhover time
        if (unhoveredTimer > MaxUnhoverTimer)
        {
            base.OnUnhover();
            _border.color = _borderColor;
           
            RobotInterface.Instance.StopRobot();
        }


        base.Update();
        if (!ExternallyDisabled)
        { 
            if (!IsActivated && Gazed)
            {
                
                _text.text = (_dwellTime - _dwellTimer).ToString("0.0");
            }
            else if (!Gazed)
            {
                _text.text = _orgText;
                _grazeTimer += Time.deltaTime;
                if (_grazeTimer < _grazePeriod)
                {
                    _dwellTime = _grazePeriodTimer;
                    _border.color = _borderGrazePeriodColor;
                }
                else if (_grazeTimer < _lowTimerPeriod)
                {
                    _dwellTime = _lowTimerPeriodTimer;
                    _border.color = _borderLowPeriodColor;
                }
                else
                {
                    _dwellTime = _orgDwellTime;
                    _border.color = _borderColor;
                }
            }
            else if (IsActivated)
            {
                _text.text = "";
                _border.color = _borderActiveColor;
            }
            if (!_isEnabled)
                _text.text = "";
        }
    }

    protected override void Activate()
    {
        base.Activate();
        _grazeTimer = 0;
    }

    public override void OnHover()
    {
        base.OnHover();

        //reset the exiting drive mode period
        ExitingDriveMode = false;
        unhoveredTimer = 0.0f;
    }

    public override void OnUnhover()
    {
        if (IsActivated)
            _grazeTimer = 0;
        Gazed = false;
        _dwellTimer = 0;
        //instead of instantly calling unhover, start a timer
        ExitingDriveMode = true;
        
        
    }

    /// <summary>
    /// Calculates the correct output depending on gazepoint on the trackpad.
    /// </summary>
    /// <param name="worldPos"> World position of trackpad interaction.</param>
    public Vector2 GetControlResult(Vector3 worldPos)
    {
        if (!IsActivated) return Vector2.zero;

        Vector3 gazeVector = worldPos - transform.position;
        gazeVector = transform.InverseTransformDirection(gazeVector);
        Vector2 controlResult = new Vector2();

        // Limit horizontal control action to vector distances
        if (Mathf.Abs(gazeVector.x) < dist_limit_x1)
            controlResult.x = 0f;
        else if (gazeVector.x > dist_limit_x2)
            controlResult.x = 1f;
        else if (gazeVector.x < -dist_limit_x2)
            controlResult.x = -1f;
        // If vector is within limits, then use exponential functions to describe steering angle depending on how far away you look vertically
        else
        {
            // Gaze long-range vertically
            if (gazeVector.z > 1.0f)
            {
                if (gazeVector.x < 0)
                    controlResult.x = -fx_b * Mathf.Pow(fx_a, Mathf.Abs(gazeVector.x)); //controlVector.x = 1 / (dist_limit_x2-dist_limit_x1) * gazeVector.x; //--- Linear function
                if (gazeVector.x > 0)
                    controlResult.x = fx_b * Mathf.Pow(fx_a, gazeVector.x);
            }
            // Gaze mid-range vertically, slope of exponential function is more aggressive
            else if (gazeVector.z > 0.6f)
            {
                if (gazeVector.x < 0)
                    controlResult.x = -fx_b * Mathf.Pow(2f * fx_a, Mathf.Abs(gazeVector.x)); //controlVector.x = 1 / (dist_limit_x2-dist_limit_x1) * gazeVector.x; //--- Linear function
                if (gazeVector.x > 0)
                    controlResult.x = fx_b * Mathf.Pow(2f * fx_a, gazeVector.x);
            }
            // Gaze short-range, set steering angle to max to allow sharp turns with reduced speed
            else
            {
                if (gazeVector.x < 0)
                    controlResult.x = -1f;
                if (gazeVector.x > 0)
                    controlResult.x = 1f;
            }
        }

        //If gaze is outside vertical threshold, then no control action
        if (gazeVector.z > dist_limit_z2 || gazeVector.z  < dist_limit_z1)
        {
            controlResult = new Vector2(0, 0);
        }
        else
        { // linear function to describe vertical acceleration
            controlResult.y = 1 / (dist_limit_z2 - dist_limit_z1) * gazeVector.z;
        }

        /*Vector2 controlResult = new Vector2();
        Vector3 localSpace = transform.InverseTransformPoint(worldPos);
        Vector2 offsetLocalSpace = (Vector2) localSpace - _zeroOffset;
        controlResult = new Vector2(offsetLocalSpace.x / (_rect.sizeDelta.x / 2), offsetLocalSpace.y / (_rect.sizeDelta.y / 2));
        controlResult = new Vector2(Mathf.Abs(controlResult.x) < _centerZoneSize ? 0 : controlResult.x, Mathf.Abs(controlResult.y) < _centerZoneSize ? 0 : controlResult.y);
        */
        return controlResult;
    }

    public override void SetSize(Vector2 sizeDelta)
    {
        base.SetSize(sizeDelta);
        _border.size = new Vector2(sizeDelta.x + 0.2f, sizeDelta.y + 0.2f);
    }

    public void SetOverlayVisibility(bool isVisible)
    {
        _overlayContainer.gameObject.SetActive(isVisible);
    }

    public override void SetExternallyDisabled(bool isExtDisabled)
    {
        //call base unhover + the extra functionality for this unhover if you disable
        base.SetExternallyDisabled(isExtDisabled);

        if (isExtDisabled == true)
            this.OnUnhover();
    }
}