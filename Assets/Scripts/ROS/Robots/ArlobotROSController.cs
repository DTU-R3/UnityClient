﻿using System.Collections;
using System.Collections.Generic;
using ROSBridgeLib.geometry_msgs;
using ROSBridgeLib.nav_msgs;
using ROSBridgeLib.std_msgs;
using UnityEngine;
using UnityEngine.UI;

public class ArlobotROSController : ROSController
{
    [SerializeField] private RawImage _cameraImage;

    private ROSLocomotionDirect _rosLocomotionDirect;
    private ROSLocomotionWaypoint _rosLocomotionWaypoint;
    private ROSLocomotionWaypointState _rosLocomotionWaypointState;
    private ROSGenericPublisher _rosLocomotionLinear;
    private ROSGenericPublisher _rosLocomotionAngular;
    private ROSLocomotionControlParams _rosLocomotionControlParams;
    private ROSGenericSubscriber<StringMsg> _rosUltrasound;
    private ROSGenericSubscriber<OdometryMsg> _rosOdometry;
    private ROSGenericPublisher _rosOdometryOverride;
    private ROSGenericPublisher _rosReset;
    private ROSGenericPublisher _rosLogger;

    private bool _hasOdometryDataToConsume;

    private OdometryData _odometryDataToConsume;
    //private CompressedImageMsg _cameraDataToConsume;
    //private CameraInfo _cameraInfoToConsume;
    //private bool _hasCameraDataToConsume;

    //Navigation
    private Vector3 _currentWaypoint;

    private float _maxLinearSpeed;
    private float _maxAngularSpeed;

    protected override void Awake()
    {
        base.Awake();
        CurrenLocomotionType = RobotLocomotionType.DIRECT;
        CurrentRobotLocomotionState = RobotLocomotionState.STOPPED;
        _robotLogger = GetComponent<RobotLogger>();
    }

    protected override void Update()
    {
        base.Update();
        //Navigation to waypoint
        if (CurrenLocomotionType != RobotLocomotionType.DIRECT && CurrentRobotLocomotionState != RobotLocomotionState.STOPPED)
        {
            //Waypoint reached
            if (Vector3.Distance(transform.position, _currentWaypoint) < Waypoints[0].ThresholdZone.Threshold)
            {
                if (Waypoints.Count > 1)
                    MoveToNextWaypoint();
                else
                {
                    EndWaypointPath();
                }
            }
        }

        if (_hasOdometryDataToConsume)
        {
            //I believe this is what I need to have for the Virtual robot as well
            // The commands being received by the virtual robot and changing it in space
            //Probably requires and inverse filter here?
            transform.rotation = _odometryDataToConsume.Orientation;
            transform.position = _odometryDataToConsume.Position.ToUTM().ToUnity();
            _hasOdometryDataToConsume = false;
        }
        /*
        if (_hasCameraDataToConsume)
        {
            lock (_cameraDataToConsume)
            {
                _cameraImage.texture = ROSCamera.ConvertToTexture2D(_cameraDataToConsume, _cameraInfoToConsume);
                _hasCameraDataToConsume = false;
            }
        }*/
    }

    public override void MoveDirect(Vector2 command)
    {
        if (CurrenLocomotionType != RobotLocomotionType.DIRECT)
            _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.STOP);
        _rosLocomotionDirect.PublishData(command.y, command.x);
        CurrenLocomotionType = RobotLocomotionType.DIRECT;
        CurrentRobotLocomotionState = RobotLocomotionState.MOVING;
    }

    private void StartWaypointRoute()
    {
        if (Waypoints.Count == 0) return;
        CurrenLocomotionType = RobotLocomotionType.WAYPOINT;
        _currentWaypoint = Waypoints[0].Point.ToUTM().ToUnity();
        Move(_currentWaypoint);
    }

    private void MoveToNextWaypoint()
    {
        Waypoints = Waypoints.GetRange(1, Waypoints.Count - 1);
        _currentWaypoint = Waypoints[0].Point.ToUTM().ToUnity();
        Move(_currentWaypoint);
        WaypointController.Instance.CreateRoute(Waypoints);
    }

    private void EndWaypointPath()
    {
        StopRobot();
        if (RobotMasterController.SelectedRobot == this)
            PlayerUIController.Instance.UpdateUI(this);
        Waypoints = new List<WaypointController.Waypoint>();
        WaypointController.Instance.ClearAllWaypoints();
    }
    /*
    private void HandleImage(ROSAgent sender, CompressedImageMsg compressedImage, CameraInfo info)
    {
        lock (_cameraDataToConsume) 
        {
            _cameraInfoToConsume = info;
            _cameraDataToConsume = compressedImage;
            _hasCameraDataToConsume = true;
        }
    }*/

    protected override void StartROS()
    {
        _rosLocomotionDirect = new ROSLocomotionDirect(ROSAgent.AgentJob.Publisher, _rosBridge, "/cmd_vel");
        _rosLocomotionWaypoint = new ROSLocomotionWaypoint(ROSAgent.AgentJob.Publisher, _rosBridge, "/waypoint");
        _rosLocomotionWaypointState = new ROSLocomotionWaypointState(ROSAgent.AgentJob.Publisher, _rosBridge, "/waypoint/state");
        _rosLocomotionControlParams = new ROSLocomotionControlParams(ROSAgent.AgentJob.Publisher, _rosBridge, "/waypoint/control_parameters");
        _rosLocomotionLinear = new ROSGenericPublisher(_rosBridge, "/waypoint/max_linear_speed", Float32Msg.GetMessageType());
        _rosLocomotionAngular = new ROSGenericPublisher(_rosBridge, "/waypoint/max_angular_speed", Float32Msg.GetMessageType());
        _rosOdometryOverride = new ROSGenericPublisher(_rosBridge, "/odo_calib_pose", OdometryMsg.GetMessageType());
        _rosReset = new ROSGenericPublisher(_rosBridge, "arlobot/reset_motorBoard", BoolMsg.GetMessageType());
        _rosLogger = new ROSGenericPublisher(_rosBridge, "/debug_output", StringMsg.GetMessageType());

        _rosUltrasound = new ROSGenericSubscriber<StringMsg>(_rosBridge, "/ultrasonic_data", StringMsg.GetMessageType(), (msg) => new StringMsg(msg));
        _rosUltrasound.OnDataReceived += ReceivedUltrasoundUpdata;
        _rosOdometry = new ROSGenericSubscriber<OdometryMsg>(_rosBridge, "/robot_gps_pose", OdometryMsg.GetMessageType(), (msg) => new OdometryMsg(msg));
        _rosOdometry.OnDataReceived += ReceivedOdometryUpdate;

        _maxLinearSpeed = RobotConfig.MaxLinearSpeed;
        _maxAngularSpeed = RobotConfig.MaxAngularSpeed;

        _rosLocomotionLinear.PublishData(new Float32Msg(_maxLinearSpeed));
        _rosLocomotionAngular.PublishData(new Float32Msg(_maxAngularSpeed));
        _rosLocomotionControlParams.PublishData(RobotConfig.LinearSpeedParameter, RobotConfig.RollSpeedParameter, RobotConfig.PitchSpeedParameter, RobotConfig.AngularSpeedParameter);
    }

    private void Move(Vector3 position)
    {
        GeoPointWGS84 point = position.ToUTM().ToWGS84();
        _rosLocomotionWaypoint.PublishData(point);
        _currentWaypoint = position;
        CurrenLocomotionType = RobotLocomotionType.WAYPOINT;
        _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.RUNNING);
        CurrentRobotLocomotionState = RobotLocomotionState.MOVING;
    }

    public override void StopRobot()
    {
        Debug.Log("STOP!");
        CurrentRobotLocomotionState = RobotLocomotionState.STOPPED;
        _rosLocomotionWaypointState.PublishData(ROSLocomotionWaypointState.RobotWaypointState.STOP);
        _rosLocomotionDirect.PublishData(0, 0);
    }

    public override void OnSelected()
    {
        base.OnSelected();
        FiducialController.Instance.Register(_rosBridge);
    }

    public override void OnDeselected()
    {
        base.OnDeselected();
        FiducialController.Instance.Unregister(_rosBridge);
    }

    public override void MovePath(List<WaypointController.Waypoint> waypoints)
    {
        Waypoints = waypoints;
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

    public void ReceivedOdometryUpdate(ROSBridgeMsg data)
    {
        
        //In WGS84
        OdometryMsg nav = (OdometryMsg) data;

        GeoPointWGS84 geoPoint = new GeoPointWGS84
        {
            latitude = nav._pose._pose._position.GetY(),
            longitude = nav._pose._pose._position.GetX(),
            altitude = nav._pose._pose._position.GetZ(),
        };
        Quaternion orientation = new Quaternion(
            x: nav._pose._pose._orientation.GetX(),
            z: nav._pose._pose._orientation.GetY(),
            y: nav._pose._pose._orientation.GetZ(),
            w: nav._pose._pose._orientation.GetW()
        );
        _odometryDataToConsume = new OdometryData
        {
            Position = geoPoint,
            Orientation = orientation
        };
        _hasOdometryDataToConsume = true;
    }

    //TODO: Not yet implemented
    public void ReceivedLocomotionStateUpdata(ROSBridgeMsg data)
    {
        //TODO: Not implemented yet

        StringMsg s = (StringMsg) data;
        //_currentRobotLocomotionState = (RobotLocomotionState) Enum.Parse(typeof(RobotLocomotionState), s.data);
    }

    public void ReceivedUltrasoundUpdata(ROSBridgeMsg data)
    {
        //TODO: Not implemented yet

        StringMsg s = (StringMsg) data;
    }

    public override void OverridePositionAndOrientation(Vector3 newPosition, Quaternion newOrientation)
    {
        GeoPointWGS84 wgs84 = newPosition.ToUTM().ToWGS84();

        PoseWithCovarianceMsg pose = new PoseWithCovarianceMsg(
            new PoseMsg(
                new PointMsg(wgs84.longitude, wgs84.latitude, wgs84.altitude),
                new QuaternionMsg(newOrientation.x, newOrientation.z, newOrientation.y, newOrientation.w)
            ));

        OdometryMsg odometryOverride = new OdometryMsg(pose);
        _rosOdometryOverride.PublishData(odometryOverride);
    }

    public override void ResetRobot()
    {
        base.ResetRobot();
        _rosReset.PublishData(new BoolMsg(true));
    }

    public override List<RobotLog> GetRobotLogs()
    {
        return _robotLogger.GetLogs();
    }

    private struct OdometryData
    {
        public GeoPointWGS84 Position;
        public Quaternion Orientation;
    }
}