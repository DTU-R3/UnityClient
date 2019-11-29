﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoToGazePlane : GazeObject {

    [SerializeField] private GameObject _wayPoint;
    [SerializeField] private int _minNumOfPoints = 40;
    [SerializeField] private float _verificationRadius = 10f;
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

        //Set the waypoint
        _wayPoint.transform.position = worldPos;
        _wayPoint.SetActive(true);

        //Instantiating our data
        Vector2 controlResult = new Vector2();
        Vector2 dirVector = new Vector2(_wayPoint.transform.position.x, _wayPoint.transform.position.z);
        float angle = Vector2.SignedAngle(Vector2.up, dirVector);
        float dist = dirVector.magnitude;

        //If the distance is too low, ignore and send a zero value todo: might be redundant
        if (dist < 0.1)
        {
            _wayPoint.SetActive(false);
            return Vector2.zero;
        }

        //Calcualte the corresponding linear and angular speeds
        controlResult.x = dist;
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

    public void UpdatePoint(RaycastHit hit)
    {
        //_wayPoint.transform.position = hit.point;

        _gazePoints.Add(hit.point);

        if (_gazePoints.Count > 1)
        {

            // Find max distance between points in list
            Vector3[] list = findMaxDist(_gazePoints);

            // If the 2 most distant points are within verification circle...
            if (list[0].x <= 2 * _verificationRadius)
            {
                //float process = (float)_gazePoints.Count / _minNumOfPoints;

                // Update Selection Radial - OBS: THIS IS FRAME-UPDATE DEPENDENT, MEANING THAT THE G2G RADIAL WILL FILL IN DIFFERENT TEMPOS DEPENDENT ON THE PC RUNNING THE GAME. 
                // A BETTER SOLUTION IS TO USE selectionRadial.HandleOver() & selectionRadial.HandleOut() and wait the OnSelectionComplete-event, as with VRInteractiveItems.
                // No time to implement this solution, but should definitely be implemented in future developments. It shouldn't be too complicated.
                //selectionRadial.Show();
                //selectionRadial.FillSelectionRadial(process);

                if (_gazePoints.Count >= _minNumOfPoints)
                {
                    Vector3 centerPoint = ((list[2] - list[1]) / 2) + list[1];  // Center point between 2 most distant points
                    //Debug.Log("Point verified. Center: "+centerPoint);
                    //agent.updateRotation = true;
                    //agent.SetDestination(hit.point);

                    //Set the waypoint
                    _wayPoint.transform.position = centerPoint;
                    _wayPoint.SetActive(true);

                    _gazePoints.Clear();
                    _gazePoints.TrimExcess();
                }
            }
            else
            {
                // Clear Selection Radial
                //selectionRadial.FillSelectionRadial(0f);
                //selectionRadial.Hide();

                //Hide waypoint
                //_wayPoint.SetActive(false);

                // Clear List
                _gazePoints.Clear();
                _gazePoints.TrimExcess();
            }
        }

        /*if (agent.hasPath)
        {
            targetMark.GetComponent<MeshRenderer>().enabled = true;
            targetMark.transform.position = agent.pathEndPosition;
            targetMark.transform.localScale.Set(verificationRadius, 0.01f, verificationRadius);
        }
        else targetMark.GetComponent<MeshRenderer>().enabled = false;*/
    }

    /**Taken from Jacopto's GoToGazeInterface
     * Finds the longest distance in a list of vectors
     * 
     */
    Vector3[] findMaxDist(List<Vector3> list)
    {
        float temp;
        float max = 0f;
        Vector3[] distantPoints = new Vector3[3];
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = 0; j < list.Count; j++)
            {
                if (i != j)
                {
                    temp = Vector3.Distance(list[i], list[j]); // Distance between points

                    if (temp > max)
                    {
                        max = temp;
                        distantPoints[1] = list[i];
                        distantPoints[2] = list[j];
                    }
                }
            }
        }
        distantPoints[0] = new Vector3(max, max, max);
        return distantPoints;
    }

    /// <summary>
    /// Calculates the correct output depending on gazepoint on the trackpad.
    /// </summary>
    public Vector2 GetControlResult()
    {
        if (!_wayPoint.activeSelf) return Vector2.zero;
        Vector2 controlResult = new Vector2();
        Vector2 dirVector = new Vector2(_wayPoint.transform.position.x, _wayPoint.transform.position.z);
        float angle = Vector2.SignedAngle(Vector2.up, dirVector);
        float dist = dirVector.magnitude;
        //Debug.Log("G2G says: angle = " + angle + ", dist = " + dist);

        //If the angle and distance is too low, ignore and send a zero value
        if (dist < 0.1)
        {
            _wayPoint.SetActive(false);
            return Vector2.zero;
        }

        //Calcualte the corresponding linear and angular speeds
        controlResult.x = dist;
        controlResult.y = angle * (1f / 45);

        Debug.Log("G@G says: controlresult = "+controlResult);

        return controlResult;
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
}
