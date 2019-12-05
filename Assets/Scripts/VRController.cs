using System.Collections;
using System.Collections.Generic;
using Fove.Managed;
using UnityEngine;

//Script attached to the player controlling VR HMD and its interfaces
public class VRController : MonoBehaviour
{
    public static VRController Instance { get; private set; }

    [SerializeField] private Transform _optimalHeadPosition;

    [Header("Cursor")] [SerializeField] private Transform _cursorCanvas;
    [SerializeField] private float _cursorDistance = 0.4f;

    [Header("Mouse Controls")] [SerializeField] private float _mouseRotationSpeed = 2;

    public Transform Head;

    private FoveInterface _foveInterface;
    private GazeObject _hoveredGazeObject;
    private StreamController.ControlType _selectedControlType;
    private bool _initialized;
    private Vector2 controlResult;
   
    void Awake()
    {
        Instance = this;
        _foveInterface = Head.GetComponent<FoveInterface>();
    }

    void Start()
    {
        //CenterHead();
        _cursorCanvas.position = Head.position + transform.forward * _cursorDistance;
        controlResult = new Vector2(-2,-2);
    }

    //Gets point where user is looking every frame and interacts with any intersecting gazeobjects if possible
    void FixedUpdate()
    {
        if (!_initialized) return;
        if (Input.GetKeyDown(KeyCode.H))
            CenterHead();
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
        

        Ray ray = new Ray();
        switch (_selectedControlType)
        {
            case StreamController.ControlType.Head:
                ray = new Ray(Head.position, Head.forward * 1000);
                break;

            case StreamController.ControlType.Eyes_Mouse:

            case StreamController.ControlType.Mouse:
                if (Input.GetMouseButtonDown(1))
                {
                }
                if (Input.GetMouseButton(1))
                {
                    Head.Rotate(Vector3.up, Input.GetAxis("Mouse X") * _mouseRotationSpeed, Space.Self);
                    Head.Rotate(Vector3.right, -Input.GetAxis("Mouse Y") * _mouseRotationSpeed, Space.Self);
                    Head.localRotation = Quaternion.Euler(Head.localEulerAngles.x, Head.localEulerAngles.y, 0);
                }

                if (Input.GetMouseButton(0) || _selectedControlType == StreamController.ControlType.Eyes_Mouse)
                {
                    ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                }
                else
                {
                    ResetHoveredObject();
                    return;
                }
                break;

            //both of the code for the two input cases was moved further down, since we want gaze data to be recorded for both inputs.
            case StreamController.ControlType.Eyes:
                
                //List<Vector3> eyeDirections = new List<Vector3>();
                //FoveInterfaceBase.EyeRays rays = _foveInterface.GetGazeRays();
                //EFVR_Eye eyeClosed = FoveInterface.CheckEyesClosed();
                //if (eyeClosed != EFVR_Eye.Both && eyeClosed != EFVR_Eye.Left)
                //    eyeDirections.Add(rays.left.direction);
                //if (eyeClosed != EFVR_Eye.Both && eyeClosed != EFVR_Eye.Right)
                //    eyeDirections.Add(rays.right.direction);
                //Vector3 direction = Vector3.zero;

                //foreach (Vector3 eyeDirection in eyeDirections)
                //{
                //    direction += eyeDirection;
                //}
                //direction = direction / eyeDirections.Count;

                //ray = new Ray(Head.transform.position, direction * 1000);
                break;

            case StreamController.ControlType.Joystick:
            {

                // //   Joystick input
                //Vector2 JoyInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

                ////if the virtual environment is on, send the command to the VirtualUnityController    
                //if (StreamController.Instance.VirtualEnvironment)
                //{
                //    if (VirtualUnityController.Instance.IsActive)
                //    {
                          
                //        VirtualUnityController.Instance.JoystickCommand(JoyInput);
                //    }
                //}
                //// Othewise send it to the robotinterface
                //else
                //{
                //    if (RobotInterface.Instance.IsConnected)
                //    {
                //        RobotInterface.Instance.DirectCommandRobot(JoyInput);
                //    }
                   
                //}
            
               break;
            }
           
        }

        //--Eye direction calculation for all occasions 
        List<Vector3> eyeDirections = new List<Vector3>();

        
        FoveInterfaceBase.EyeRays rays = _foveInterface.GetGazeRays();
        EFVR_Eye eyeClosed = FoveInterface.CheckEyesClosed();
        if (eyeClosed != EFVR_Eye.Both && eyeClosed != EFVR_Eye.Left)
            eyeDirections.Add(rays.left.direction);
        if (eyeClosed != EFVR_Eye.Both && eyeClosed != EFVR_Eye.Right)
            eyeDirections.Add(rays.right.direction);
        Vector3 direction = Vector3.zero;

        foreach (Vector3 eyeDirection in eyeDirections)
        {
            direction += eyeDirection;
        }
        direction = direction / eyeDirections.Count;

        ray = new Ray(Head.transform.position, direction * 1000);
        //---------------------------------------------------------

        //Positioning of the cursor
        _cursorCanvas.position = Head.position + ray.direction * _cursorDistance;

        Debug.DrawRay(ray.origin, ray.direction);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            /*//Test
            if (hit.collider.gameObject.name == "ViewportContainer")
            {
                BoxCollider col = (BoxCollider)hit.collider;
                Vector3 point = col.transform.InverseTransformPoint(hit.point);
                point = new Vector2((point.x + col.size.x / 2)/col.size.x, (point.y + col.size.y / 2) / col.size.y);
                Ray ray2 = viewportCam.ViewportPointToRay(point);
                RaycastHit hit2;
                if (Physics.Raycast(ray2, out hit2))
                {
                    GameObject.CreatePrimitive(PrimitiveType.Sphere).transform.position = hit2.point;
                }
            }*/

            GazeObject gazeObject = hit.collider.GetComponent<GazeObject>();
            if (gazeObject == null)
            {
                //ResetHoveredObject();
                return;
            }

            /*// For this reason we also check if the tag of the gazeobject is the correct one 
            RobotControlTrackPad robotControl = gazeObject.GetComponent<RobotControlTrackPad>();
            if (robotControl != null && gazeObject.CompareTag("EyeControlPanel"))
            {
                //Control result is provided on hit. This is updated for both cases of input
                controlResult = robotControl.GetControlResult(hit.point);
                
                //If the robotcontrols are activated and the eye tracking is used for motion then send the command to the appropriate controller
                if (robotControl.IsActivated & !robotControl.IsExternallyDisabled() && 
                    _selectedControlType==StreamController.ControlType.Eyes )
                {
                    if (StreamController.Instance.VirtualEnvironment)
                    {
                        
                        if (VirtualUnityController.Instance.IsActive)
                        {
                           // Debug.Log("Sending gaze command to robot");
                            VirtualUnityController.Instance.GazeCommand(controlResult);
                        }
                        else{Debug.Log("VirtualUnityController is not connected"); }

                    }
                    // Othewise send it to the robotinterface
                    else
                    {
                        if (RobotInterface.Instance.IsConnected)
                        {
                            RobotInterface.Instance.SendCommand(controlResult);
                        }
                        else { Debug.Log("RobotInterface controller is not connected"); }

                    }
                    //Instead of robotinterface here 
                }

                //---Joystick Input---
                else if (robotControl.IsActivated & !robotControl.IsExternallyDisabled() &&
                         _selectedControlType == StreamController.ControlType.Joystick)
                {
                    //   Joystick input
                    Vector2 JoyInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

                    //if the virtual environment is on, send the command to the VirtualUnityController    
                    if (StreamController.Instance.VirtualEnvironment)
                    {
                        if (VirtualUnityController.Instance.IsActive)
                        {

                            VirtualUnityController.Instance.JoystickCommand(JoyInput);
                        }
                    }
                    // Othewise send it to the robotinterface
                    else
                    {
                        if (RobotInterface.Instance.IsConnected)
                        {
                            RobotInterface.Instance.DirectCommandRobot(JoyInput);
                        }

                    }
                }

            }*/
            // For this reason we also check if the tag of the gazeobject is the correct one 
            /*GoToGazePlane gtgPlane = gazeObject.GetComponent<GoToGazePlane>();
            if (gtgPlane != null && gazeObject.CompareTag("GoToGaze"))
            {
                //Control result is provided on hit. This is updated for both cases of input
                controlResult = gtgPlane.GetControlResult(hit.point);

                //If the robotcontrols are activated and the eye tracking is used for motion then send the command to the appropriate controller
                if (gtgPlane.IsActivated & !gtgPlane.IsExternallyDisabled() &&
                    _selectedControlType == StreamController.ControlType.Eyes)
                {
                    if (StreamController.Instance.VirtualEnvironment)
                    {

                        if (VirtualUnityController.Instance.IsActive)
                        {
                            // Debug.Log("Sending gaze command to robot");
                            VirtualUnityController.Instance.GazeCommand(controlResult);
                            gtgPlane.MoveWaypoint(controlResult);
                        }
                        else { Debug.Log("VirtualUnityController is not connected"); }

                    }
                    // Othewise send it to the robotinterface
                    else
                    {
                        if (RobotInterface.Instance.IsConnected)
                        {
                            RobotInterface.Instance.SendCommand(controlResult);
                            gtgPlane.MoveWaypoint(controlResult);
                        }
                        else { Debug.Log("RobotInterface controller is not connected"); }

                    }
                    //Instead of robotinterface here 
                }

                //---Joystick Input---
                else if (gtgPlane.IsActivated & !gtgPlane.IsExternallyDisabled() &&
                         _selectedControlType == StreamController.ControlType.Joystick)
                {
                    //   Joystick input
                    Vector2 JoyInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

                    //if the virtual environment is on, send the command to the VirtualUnityController    
                    if (StreamController.Instance.VirtualEnvironment)
                    {
                        if (VirtualUnityController.Instance.IsActive)
                        {

                            VirtualUnityController.Instance.JoystickCommand(JoyInput);
                        }
                    }
                    // Othewise send it to the robotinterface
                    else
                    {
                        if (RobotInterface.Instance.IsConnected)
                        {
                            RobotInterface.Instance.DirectCommandRobot(JoyInput);
                        }

                    }
                }

            }*/
            GoToGazeSphere gtgSphere = gazeObject.GetComponent<GoToGazeSphere>();
            if (gtgSphere != null && gazeObject.CompareTag("GoToGaze"))
            {
                //Control result is provided on hit. This is updated for both cases of input
                controlResult = gtgSphere.GetControlResult(hit.point);

                //If the robotcontrols are activated and the eye tracking is used for motion then send the command to the appropriate controller
                if (gtgSphere.IsActivated & !gtgSphere.IsExternallyDisabled() &&
                    _selectedControlType == StreamController.ControlType.Eyes)
                {
                    if (StreamController.Instance.VirtualEnvironment)
                    {

                        if (VirtualUnityController.Instance.IsActive)
                        {
                            // Debug.Log("Sending gaze command to robot");
                            VirtualUnityController.Instance.GazeCommand(controlResult);
                            gtgSphere.MoveWaypoint(controlResult);
                        }
                        else { Debug.Log("VirtualUnityController is not connected"); }

                    }
                    // Othewise send it to the robotinterface
                    else
                    {
                        if (RobotInterface.Instance.IsConnected)
                        {
                            RobotInterface.Instance.SendCommand(controlResult);
                            //gtgSphere.MoveWaypoint(controlResult);
                        }
                        else { Debug.Log("RobotInterface controller is not connected"); }

                    }
                    //Instead of robotinterface here 
                }

                //---Joystick Input---
                else if (gtgSphere.IsActivated & !gtgSphere.IsExternallyDisabled() &&
                         _selectedControlType == StreamController.ControlType.Joystick)
                {
                    //   Joystick input
                    Vector2 JoyInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

                    //if the virtual environment is on, send the command to the VirtualUnityController    
                    if (StreamController.Instance.VirtualEnvironment)
                    {
                        if (VirtualUnityController.Instance.IsActive)
                        {

                            VirtualUnityController.Instance.JoystickCommand(JoyInput);
                        }
                    }
                    // Othewise send it to the robotinterface
                    else
                    {
                        if (RobotInterface.Instance.IsConnected)
                        {
                            RobotInterface.Instance.DirectCommandRobot(JoyInput);
                        }

                    }
                }

            }
            else
            {
                //this result means not staring at panel.
                controlResult = new Vector2(-2,-2);
                //TODO : SendStopCommandToRobot instead of a zero vector. The zero vector is filtered and still adds movemenet to the robot
                // RobotInterface.Instance.SendCommand(Vector2.zero);
            }
            if (gazeObject == _hoveredGazeObject) return;
            if (_hoveredGazeObject != null) _hoveredGazeObject.OnUnhover();
            gazeObject.OnHover();
            _hoveredGazeObject = gazeObject;
        }
        else
            ResetHoveredObject();
    }

    IEnumerator DelayCommand()
    {
        print(Time.time);
        yield return new WaitForSecondsRealtime(0.5f);
        print(Time.time);
    }

    private void ResetHoveredObject()
    {
        if (_hoveredGazeObject != null)
            _hoveredGazeObject.OnUnhover();
        _hoveredGazeObject = null;
        if (RobotInterface.Instance)
        RobotInterface.Instance.StopRobot();

        if (VirtualUnityController.Instance)
        VirtualUnityController.Instance.StopRobot();
    }

    /// <summary>
    /// Centers the player's head position so that they are looking forward
    /// </summary>
    private void CenterHead()
    {
        Quaternion qrot = Quaternion.Inverse(Head.rotation) * _optimalHeadPosition.rotation;
        Head.parent.rotation = Head.parent.rotation * qrot;
        Vector3 movementToCenter = _optimalHeadPosition.position - Head.position;
        Vector3 hcPos = Head.parent.position;
        Head.parent.position = hcPos + movementToCenter;
    }

    public void Initialize(StreamController.ControlType controlType)
    {
        _selectedControlType = controlType;
        _initialized = true;
    }

    public void RotateSeat(float deltaAngle)
    {
        transform.localEulerAngles += new Vector3(0, deltaAngle, 0);
    }

    public void CenterSeat()
    {
        transform.localEulerAngles = Vector3.zero;
    }

    public Vector2 GetPanelGazeCoordinates()
    {

        return controlResult;
    }
}