﻿using System.Collections.Generic;
using Messages;
using Messages.sensor_msgs;
using Messages.std_msgs;
using UnityEngine;
using UnityEngine.UI;
using Quaternion = UnityEngine.Quaternion;
using String = Messages.std_msgs.String;
using Vector3 = UnityEngine.Vector3;

public class ArlobotROSController : ROSController {

    [SerializeField] private int _waypointStartIndex = 0;
    [SerializeField] private RawImage _cameraImage;

    public enum RobotLocomotionState { MOVING, STOPPED }
    public enum RobotLocomotionType { WAYPOINT, DIRECT }

    public static ArlobotROSController Instance { get; private set; }
    public RobotLocomotionState CurrentRobotLocomotionState { get; private set; }
    public RobotLocomotionType CurrenLocomotionType { get; private set; }

    private ROSLocomotionDirect _rosLocomotionDirect;
    private ROSLocomotionWaypoint _rosLocomotionWaypoint;
    private ROSLocomotionWaypointState _rosLocomotionWaypointState;
    private ROSUltrasound _rosUltrasound;
    private ROSTransformPosition _rosTransformPosition;
    private ROSTransformHeading _rosTransformHeading;
    private ROSLocomotionState _rosLocomotionState;
    private ROSLocomotionLinearSpeed _rosLocomotionLinear;
    private ROSLocomotionSpeedParams _rosLocomotionSpeedParams;
    private ROSCamera _rosCamera;

    private bool _hasPositionDataToConsume;
    private Vector3 _positionDataToConsume;
    private bool _hasHeadingDataToConsume;
    private float _headingDataToConsume;
    private float _oldMaxLinearSpeed;
    private float _oldLinearSpeedParam;
    private float _oldAngularSpeedParam;
    private CompressedImage _cameraDataToConsume;
    private CameraInfo _cameraInfoToConsume;
    private bool _hasCameraDataToConsume;

    //Navigation
    private Vector3 _currentWaypoint;
    private int _waypointIndex;
    private float _waypointDistanceThreshhold = 0.1f;
    private float _maxLinearSpeed = 3;
    private float _linearSpeedParam = 3;
    private float _angularSpeedParam = 1;
    private List<GeoPointWGS84> _waypoints;

    void Awake()
    {
        Instance = this;
        _waypointDistanceThreshhold = ConfigManager.ConfigFile.WaypointDistanceThreshold;
        _maxLinearSpeed = ConfigManager.ConfigFile.MaxLinearSpeed;
        _linearSpeedParam = ConfigManager.ConfigFile.LinearSpeedParameter;
        _angularSpeedParam = ConfigManager.ConfigFile.AngularSpeedParameter;
        CurrenLocomotionType = RobotLocomotionType.DIRECT;
        CurrentRobotLocomotionState = RobotLocomotionState.STOPPED;
    }

    void Start()
    {
        StartROS(ConfigManager.ConfigFile.RosMasterUri);
    }

    void Update()
    {
        //Direct control of robot
        float linear = 0;
        float angular = 0;
        
        if (Input.GetKey(KeyCode.UpArrow))
        {
            linear = 1f;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            linear = -1f;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            angular = 1f;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            angular = -1f;
        }
        if (linear == 0 && angular == 0 && CurrentRobotLocomotionState != RobotLocomotionState.STOPPED && CurrenLocomotionType == RobotLocomotionType.DIRECT)
        {
            StopRobot();
        }
        else if (linear != 0 || angular != 0)
        {
            MoveDirect(new Vector2(angular, linear));
        }
        
        //Navigation to waypoint
        if (CurrenLocomotionType != RobotLocomotionType.DIRECT && CurrentRobotLocomotionState != RobotLocomotionState.STOPPED)
        {
            //Waypoint reached
            if (Vector3.Distance(transform.position, _currentWaypoint) < _waypointDistanceThreshhold)
            {
                if (_waypointIndex < _waypoints.Count - 1)
                    MoveToNextWaypoint();
                else
                {
                    EndWaypointPath();
                }
            }
        }

        if (_hasHeadingDataToConsume)
        {
                transform.rotation = Quaternion.Euler(0, _headingDataToConsume, 0);
            _hasHeadingDataToConsume = false;
        }
        if (_hasPositionDataToConsume)
        {
            transform.position = _positionDataToConsume;
            _hasPositionDataToConsume = false;
        }
        if (_hasCameraDataToConsume)
        {
            lock (_cameraDataToConsume)
            {
                _cameraImage.texture = ROSCamera.ConvertToTexture2D(_cameraDataToConsume, _cameraInfoToConsume);
                _hasCameraDataToConsume = false;
            }
        }

        if (_maxLinearSpeed != _oldMaxLinearSpeed)
        {
            _oldMaxLinearSpeed = _maxLinearSpeed;
            _rosLocomotionLinear.PublishData(_maxLinearSpeed);
        }
        if (_angularSpeedParam != _oldAngularSpeedParam || _linearSpeedParam != _oldLinearSpeedParam)
        {
            _oldAngularSpeedParam = _angularSpeedParam;
            _oldLinearSpeedParam = _linearSpeedParam;

            _rosLocomotionSpeedParams.PublishData(_linearSpeedParam, _angularSpeedParam);
        }
    }

    public override void MoveDirect(Vector2 command)
    {
        if (CurrenLocomotionType != RobotLocomotionType.DIRECT)
            _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.STOP);
        _rosLocomotionDirect.PublishData(command);
        CurrenLocomotionType = RobotLocomotionType.DIRECT;
        CurrentRobotLocomotionState = RobotLocomotionState.MOVING;
    }

    private void StartWaypointRoute()
    {
        _waypointIndex = _waypointStartIndex;
        CurrenLocomotionType = RobotLocomotionType.WAYPOINT;
        _currentWaypoint =_waypoints[_waypointIndex].ToUTM().ToUnity();
        Move(_currentWaypoint);
    }

    private void MoveToNextWaypoint()
    {
        _waypointIndex++;
        _currentWaypoint = _waypoints[_waypointIndex].ToUTM().ToUnity();
        Move(_currentWaypoint);
    }

    private void EndWaypointPath()
    {
        StopRobot();
        PlayerUIController.Instance.SetDriveMode(false);
    }

    private void HandleImage(ROSAgent sender, CompressedImage compressedImage, CameraInfo info)
    {
        lock (_cameraDataToConsume) 
        {
            _cameraInfoToConsume = info;
            _cameraDataToConsume = compressedImage;
            _hasCameraDataToConsume = true;
        }
    }

    public override void StopRobot()
    {
        CurrentRobotLocomotionState = RobotLocomotionState.STOPPED;
        _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.STOP);
        _rosLocomotionDirect.PublishData(Vector2.zero);
    }

    public override void StartROS(string uri) {
        base.StartROS(uri);
        _rosLocomotionDirect = new ROSLocomotionDirect();
        _rosLocomotionDirect.StartAgent(ROSAgent.AgentJob.Publisher);
        _rosLocomotionWaypoint = new ROSLocomotionWaypoint();
        _rosLocomotionWaypoint.StartAgent(ROSAgent.AgentJob.Publisher);
        _rosLocomotionWaypointState = new ROSLocomotionWaypointState();
        _rosLocomotionWaypointState.StartAgent(ROSAgent.AgentJob.Publisher);
        _rosLocomotionSpeedParams = new ROSLocomotionSpeedParams();
        _rosLocomotionSpeedParams.StartAgent(ROSAgent.AgentJob.Publisher);
        _rosLocomotionLinear = new ROSLocomotionLinearSpeed();
        _rosLocomotionLinear.StartAgent(ROSAgent.AgentJob.Publisher);
        //_rosUltrasound = new ROSUltrasound();
       //_rosUltrasound.StartAgent(ROSAgent.AgentJob.Subscriber, _clientNamespace);
        _rosTransformPosition = new ROSTransformPosition();
        _rosTransformPosition.StartAgent(ROSAgent.AgentJob.Subscriber);
        _rosTransformPosition.DataWasReceived += ReceivedPositionUpdate;
        _rosTransformHeading = new ROSTransformHeading();
        _rosTransformHeading.StartAgent(ROSAgent.AgentJob.Subscriber);
        _rosTransformHeading.DataWasReceived += ReceivedHeadingUpdate;
        _rosLocomotionState = new ROSLocomotionState();
        _rosLocomotionState.StartAgent(ROSAgent.AgentJob.Subscriber);
        _rosLocomotionState.DataWasReceived += ReceivedLocomotionStateUpdata;
        //_rosCamera = new ROSCamera();
        //_rosCamera.StartAgent(ROSAgent.AgentJob.Subscriber);
        //_rosCamera.DataWasReceived += HandleImage;
    }

    private void Move(Vector3 position)
    {
        Debug.Log(position);
        GeoPointWGS84 point = position.ToUTM().ToWGS84();
        _rosLocomotionWaypoint.PublishData(point);
        _currentWaypoint = position;
        CurrenLocomotionType = RobotLocomotionType.WAYPOINT;
        _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.RUNNING);
        CurrentRobotLocomotionState = RobotLocomotionState.MOVING;
    }

    public override void MoveToPoint(GeoPointWGS84 point)
    {
        _waypoints.Clear();
        _waypoints.Add(point);
        _waypointIndex = 0;
    }

    public override void MovePath(List<GeoPointWGS84> waypoints) 
    {
        _waypoints = waypoints;
        StartWaypointRoute();
    }

    public override void PausePath()
    {
        _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.PARK);
    }

    public override void ResumePath()
    {
        _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.RUNNING);
    }

    public void ReceivedPositionUpdate(ROSAgent sender, IRosMessage position)
    {
        //In WGS84
        NavSatFix nav = (NavSatFix) position;
        GeoPointWGS84 geoPoint = new GeoPointWGS84
        {
            latitude = nav.latitude,
            longitude = nav.longitude,
            altitude = nav.altitude,
        };
        if (GeoUtils.UtmOriginSet)
        {
            _positionDataToConsume = geoPoint.ToUTM().ToUnity();

            _hasPositionDataToConsume = true;
            
        }
    }

    public void ReceivedHeadingUpdate(ROSAgent sender, IRosMessage heading)
    {
        Float32 f = (Float32) heading;
        _headingDataToConsume = f.data;
        _hasHeadingDataToConsume = true;
    }

    //TODO: Not yet implemented
    public void ReceivedLocomotionStateUpdata(ROSAgent sender, IRosMessage state)
    {
        //TODO: Not implemented yet

        String s = (String) state;
        //_currentRobotLocomotionState = (RobotLocomotionState) Enum.Parse(typeof(RobotLocomotionState), s.data);
    }

}