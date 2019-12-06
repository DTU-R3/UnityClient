using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoToGazeSphere : GazeObject {
    [Header("GoToGaze Settings")]
    [SerializeField] private GameObject _wayPoint;
    [SerializeField] private float _sphereRadius = 12.75f;
    [SerializeField] [Range(0.0f, 1.0f)] private float _maxLinearVelocity;
    private List<Vector3> _gazePoints;

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


    protected override void Awake()
    {
        base.Awake();
        //_border.color = _borderColor;
        _orgDwellTime = _dwellTime;

    }

    protected override void Update()
    {

        if (DisconnectFromRobot)
        {
            RobotInterface.Instance.Quit();
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
            //_border.color = _borderColor;

            RobotInterface.Instance.StopRobot();
        }


        base.Update();
        if (!ExternallyDisabled)
        {
            if (!IsActivated && Gazed)
            {

                //_text.text = (_dwellTime - _dwellTimer).ToString("0.0");
            }
            else if (!Gazed)
            {
                //_text.text = _orgText;
                _grazeTimer += Time.deltaTime;
                if (_grazeTimer < _grazePeriod)
                {
                    _dwellTime = _grazePeriodTimer;
                    //_border.color = _borderGrazePeriodColor;
                }
                else if (_grazeTimer < _lowTimerPeriod)
                {
                    _dwellTime = _lowTimerPeriodTimer;
                    //_border.color = _borderLowPeriodColor;
                }
                else
                {
                    _dwellTime = _orgDwellTime;
                    //_border.color = _borderColor;
                }
            }
            else if (IsActivated)
            {
                //_text.text = "";
                //_border.color = _borderActiveColor;
            }
            //if (!_isEnabled)
            //_text.text = "";
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

        //Disable waypoint
        _wayPoint.transform.position = transform.position;
        _wayPoint.SetActive(false);
    }

    /// <summary>
    /// Calculates the correct output depending on gazepoint on the trackpad.
    /// </summary>
    /// <param name="worldPos"> World position of trackpad interaction.</param>
    public Vector2 GetControlResult(Vector3 worldPos)
    {

        if (!IsActivated) return Vector2.zero;

        //If the focuspoint is higher than the middle of the sphere, ignore it
        if (worldPos.y > transform.position.y - _sphereRadius/6f) return Vector2.zero;

        //Set the waypoint
        _wayPoint.transform.position = worldPos;
        _wayPoint.SetActive(true);

        //Instantiating our data
        Vector2 controlResult = new Vector2();
        Vector2 dirVector = new Vector2(_wayPoint.transform.position.x, _wayPoint.transform.position.z);
        float angle = Vector2.SignedAngle(Vector2.up, dirVector);
        float dist = SphericalDistance(
                         transform.position - Vector3.up * _sphereRadius,
                         _wayPoint.transform.position
                     ) * (_sphereRadius / 3.5f);

        //If the distance is too low, ignore and send a zero value todo: might be redundant
        if (dist < 0.1)
        {
            _wayPoint.SetActive(false);
            return Vector2.zero;
        }

        //Calcualte the corresponding linear and angular speeds
        controlResult.x = (dist > _maxLinearVelocity)? _maxLinearVelocity : dist;
        controlResult.y = angle * (1f / 45);

        Debug.Log("G@G says: controlresult = " + controlResult);

        return controlResult;
    }

    //    public override void SetSize(Vector2 sizeDelta)
    //    {
    //        base.SetSize(sizeDelta);
    //        _border.size = new Vector2(sizeDelta.x + 0.2f, sizeDelta.y + 0.2f);
    //    }

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

    /// <summary>
    /// Moves the waypoint as the robot moves
    /// </summary>
    /// <param name="controlResult">the previous control result</param>
    public void MoveWaypoint(Vector2 controlResult)
    {
        Vector2 dirVector = new Vector2(_wayPoint.transform.position.x, _wayPoint.transform.position.z);

        //Move the _waypoint
        float ds = ((controlResult.y / dirVector.magnitude) * 180) / Mathf.PI;
        _wayPoint.transform.RotateAround(_wayPoint.transform.position, Vector3.up, ds);
        _wayPoint.transform.Translate(new Vector3(-dirVector.x, 0, -dirVector.y) * controlResult.x);
    }

    /// <summary>
    /// Returns the normalized spherical distance between two vectors
    /// </summary>
    /// <param name="position1"></param>
    /// <param name="position2"></param>
    /// <returns></returns>
    float SphericalDistance(Vector3 position1, Vector3 position2)
    {
        return Mathf.Acos(Vector3.Dot(position1.normalized, position2.normalized));
    }
}
