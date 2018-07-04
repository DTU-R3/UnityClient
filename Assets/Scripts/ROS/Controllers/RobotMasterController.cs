using System.Collections.Generic;
using System.IO;
using System.Linq;
using ROSBridgeLib;
using UnityEngine;

public class RobotMasterController : MonoBehaviour
{
    public static RobotMasterController Instance { get; private set; }

    private static ROSController _selectedRobot;

    public static ROSController SelectedRobot
    {
        get { return _selectedRobot; }
        private set
        {
            if (_selectedRobot == null)
            {
                //PlayerUIController.Instance.SetDriveMode();
            }

            _selectedRobot = value;
        }
    }

    //Master RobotName, corresponding Robot
    public static Dictionary<string, Robot> Robots { get; private set; }
    public static Dictionary<string, ROSController> ActiveRobots { get; private set; }

    private string _configPath;
    private string _robotPrefabPath;
    private bool _hasRobotLostConnection;
    private ROSController _robotLostConnection;

    void Awake()
    {
        Instance = this;
        Robots = new Dictionary<string, Robot>();
        ActiveRobots = new Dictionary<string, ROSController>();
        _configPath = Application.streamingAssetsPath + "/Config/Robots/";
        _robotPrefabPath = "Prefabs/Robots/";
    }

    void Start()
    {
        MazeMapController.Instance.OnFinishedGeneratingCampus += LoadRobotsFromCampus;
    }

    void Update()
    {
        if (_hasRobotLostConnection)
        {
            _hasRobotLostConnection = false;
            CleanUpDisconnectedRobot(_robotLostConnection);
        }

        foreach (KeyValuePair<string, Robot> bridge in Robots)
        {
            if (bridge.Value.IsActive)
                bridge.Value.RosBridge.Render();
        }
    }

    void OnApplicationQuit()
    {
        foreach (KeyValuePair<string, Robot> bridge in Robots)
        {
            if (bridge.Value.IsActive)
                bridge.Value.RosBridge.Disconnect();
        }
    }

    private void LoadRobotsFromCampus(int campusId)
    {
        if (ActiveRobots != null)
        {
            foreach (KeyValuePair<string, ROSController> pair in ActiveRobots)
            {
                pair.Value.Destroy();
            }
        }

        ActiveRobots = new Dictionary<string, ROSController>();
        Robots = new Dictionary<string, Robot>();
        string[] robotConfigPaths = Directory.GetFiles(_configPath, "*.json");
        foreach (string path in robotConfigPaths)
        {
            string robotName = Path.GetFileNameWithoutExtension(path);
            string robotFileJson = File.ReadAllText(path);
            RobotConfigFile robotFile = JsonUtility.FromJson<RobotConfigFile>(robotFileJson);

            if (!robotFile.Campuses.Contains(campusId)) continue;

            if (!robotFile.RosBridgeUri.StartsWith("ws://"))
                robotFile.RosBridgeUri = "ws://" + robotFile.RosBridgeUri;
            ;

            ROSBridgeWebSocketConnection rosBridge = new ROSBridgeWebSocketConnection(robotFile.RosBridgeUri, robotFile.RosBridgePort, robotName);

            Robot robot = new Robot(robotFile.Campuses, rosBridge, robotName, robotFile.RosBridgeUri, robotFile.RosBridgePort, false, robotFile);
            Robots.Add(robotName, robot);
        }

        PlayerUIController.Instance.LoadRobots(Robots.Select(robot => robot.Value).ToList());
    }

    private void ConnectionResult(string robotName, bool isAlive)
    {
        Robots[robotName].IsActive = isAlive;
        PlayerUIController.Instance.RobotRefreshed();
        if (isAlive)
            Robots[robotName].RosBridge.Disconnect();
    }

    private void CleanUpDisconnectedRobot(ROSController robot)
    {
        Debug.LogError("Robot [" + robot.gameObject.name + "] lost connection!");
        DisconnectRobot(robot.RobotName);
        PlayerUIController.Instance.UpdateRobotList();
        PlayerUIController.Instance.RobotWasDisconnected(Robots[robot.RobotName]);
        if (ActiveRobots.Count > 0)
            SelectRobot(ActiveRobots.First().Value);
        else
        {
            WaypointController.Instance.ClearAllWaypoints();
        }
    }

    public Robot GetRobotFromName(string robotName)
    {
        Robot robot = null;

        if (Robots.TryGetValue(robotName, out robot))
        {
            if (robot.Campuses.Contains(MazeMapController.Instance.CampusId))
                return robot;
        }
        return null;
    }

    public ROSController GetRosControllerFromName(string robotName)
    {
        ROSController rosController = null;
        if (ActiveRobots.TryGetValue(robotName, out rosController))
        {
            return rosController;
        }
        else
            return null;
    }

    public List<string> GetRobotNames(int campusId)
    {
        return Robots.Where(pair => pair.Value.Campuses.Contains(campusId)).Select(pair => pair.Value.Name).ToList();
    }

    public void RobotLostConnection(ROSController robot)
    {
        _hasRobotLostConnection = true;
        _robotLostConnection = robot;
    }

    public void RefreshRobotConnections()
    {
        foreach (KeyValuePair<string, Robot> pair in Robots)
        {
            pair.Value.RosBridge.Connect(ConnectionResult);
        }
    }

    public void ConnectToRobot(string robotName)
    {
        Robots[robotName].Connected = true;
        Robots[robotName].RosBridge.Connect();
        Robot robot = Robots[robotName];
        GameObject robotObject = Instantiate(Resources.Load(_robotPrefabPath + robot.Name)) as GameObject;
        ROSController rosController = robotObject.GetComponent<ROSController>();
        rosController.InitialiseRobot(robot.RosBridge, robot.RobotConfig, robotName);
        ActiveRobots.Add(robotName, rosController);

        PlayerUIController.Instance.AddRobotToList(robot.Name);
    }

    public void DisconnectRobot(string robotName)
    {
        Robots[robotName].Connected = false;
        Robots[robotName].IsActive = false;
        ActiveRobots[robotName].Destroy();
        ActiveRobots.Remove(robotName);

        PlayerUIController.Instance.RemoveRobotFromList(Robots[robotName].Name);

        if (SelectedRobot != null)
            if (SelectedRobot.RobotName == robotName)
            {
                SelectedRobot = null;
                WaypointController.Instance.ClearAllWaypoints();
            }
    }

    public void DisconnectAllRobots()
    {
        foreach (KeyValuePair<string, Robot> pair in Robots)
        {
            ROSController rosController = null;
            pair.Value.Connected = false;

            if (ActiveRobots.TryGetValue(pair.Key, out rosController))
            {
                ActiveRobots[pair.Key].Destroy();
                ActiveRobots.Remove(pair.Key);
            }
        }
    }

    public void SelectRobot(ROSController rosController)
    {
        if (SelectedRobot != null)
            SelectedRobot.OnDeselected();

        if (rosController == null)
        {
            SelectedRobot = null;
            WaypointController.Instance.ClearAllWaypoints();
        }
        else
        {
            SelectedRobot = rosController;
            SelectedRobot.OnSelected();
            PlayerController.Instance.FocusCameraOn(SelectedRobot.transform);
        }
        PlayerUIController.Instance.UpdateUI(rosController);
    }

    public class Robot
    {
        public int[] Campuses;
        public ROSBridgeWebSocketConnection RosBridge;
        public string Name;
        public string Uri;
        public int Port;
        public bool IsActive;
        public bool Connected;
        public RobotConfigFile RobotConfig;

        public Robot(int[] campuses, ROSBridgeWebSocketConnection rosBridge, string name, string uri, int port, bool isActive, RobotConfigFile robotConfig)
        {
            Campuses = campuses;
            RosBridge = rosBridge;
            Name = name;
            Uri = uri;
            Port = port;
            IsActive = isActive;
            RobotConfig = robotConfig;
        }
    }
}