using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class MazeMapController : MonoBehaviour
{
    public float FloorHeightAboveGround { get; private set; }
    public float FloorHeightBelowGround { get; private set; }
    public int CurrentActiveLevel { get; private set; }
    public bool CampusLoaded { get; private set; }
    public int CampusId { get; private set; }

    [Space(5)] [SerializeField] private Transform _triggerFloor;
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private bool _shouldBackupMazemapData;

    public static MazeMapController Instance;

    //Key: BuildingId, Value: Building data
    public Dictionary<int, Building> Buildings;

    public delegate void WasFinishedGeneratingCampus(int campusId);
    public event WasFinishedGeneratingCampus OnFinishedGeneratingCampus;

    public delegate void StartedGeneratingCampus();
    public event StartedGeneratingCampus OnStartedGeneratingCampus;

    private const string REST_BUILDING_SEARCH = "https://api.mazemap.com/api/buildings/?campusid={0}&srid=4326";
    private const string REST_FLOOROUTLINES = "https://api.mazemap.com/api/flooroutlines/?campusid={0}&srid=4326";
    private const string REST_FLOOROUTLINE = "https://api.mazemap.com/api/flooroutlines/?floorid={0}&srid=4326";
    private const string REST_POI_SEARCH = "http://api.mazemap.com/api/pois/{0}/?srid=4326";
    private const string REST_POI_SEARCH_BY_FLOORID = "https://api.mazemap.com/api/pois/?floorid={0}&srid=4326";
    private const string PATH_SEARCH = "https://api.mazemap.com/routing/path/?srid=4326&hc=false&sourcelat={0}&sourcelon={1}&targetlat={2}&targetlon={3}&sourcez=3&targetz=3&lang=en";

    //Key: Floor FloorIndex, Value: Floor Transform
    private Dictionary<int, List<Floor>> _floorsByZ;

    //Key: Floor FloorIndex, Value: If layer is currently shown
    private Dictionary<int, bool> _zLayerActive;

    private float _lineWidth;
    private MazemapBackupFile _mazemapBackup;
    private MazeMapMeasurements _mazeMapMeasurements;
    private string _backupPath;
    private bool _isUsingBackup;
    private Transform _campusParent;

    [SerializeField] private GameObject _colliderPrefab;

    private int _buildingsLeftToDraw;

    private int BuildingsLeftToDraw
    {
        get { return _buildingsLeftToDraw; }
        set
        {
            _buildingsLeftToDraw = value;
            PlayerUIController.Instance.UpdateLoadingProgress(1 - (float) _buildingsLeftToDraw / Buildings.Count);
        }
    }

    void Awake()
    {
        Application.runInBackground = true;
        CampusId = -1;
        Instance = this;
        Buildings = new Dictionary<int, Building>();
        _floorsByZ = new Dictionary<int, List<Floor>>();
        _zLayerActive = new Dictionary<int, bool>();
        CurrentActiveLevel = 2;
        FloorHeightAboveGround = ConfigManager.ConfigFile.FloorHeightAboveGround;
        FloorHeightBelowGround = ConfigManager.ConfigFile.FloorHeightBelowGround;
        _lineWidth = ConfigManager.ConfigFile.FloorLineWidth;

        _backupPath = Application.streamingAssetsPath + "/Backup/MazemapBackup{0}.json";

        string measurementsPath = Application.streamingAssetsPath + "/MazeMap/Measurements.json";
        _mazeMapMeasurements = File.Exists(measurementsPath) ? JsonUtility.FromJson<MazeMapMeasurements>(File.ReadAllText(measurementsPath)) : new MazeMapMeasurements();
    }

    /// <summary>
    /// Coroutine to get data from REST API
    /// </summary>
    /// <param name="url">REST Endpoint</param>
    /// <param name="whenDone">Which function to run with returned JSON data</param>
    private IEnumerator GetWWW(string url, Action<string> whenDone)
    {
        WWW www = new WWW(url);
        yield return www;
        if (whenDone != null)
            whenDone(www.text);
    }

    /// <summary>
    /// Parses GeoJSON into building objects and draws them
    /// </summary>
    /// <param name="text">GeoJSON string</param>
    private void GetBuildings(string text)
    {
        _mazemapBackup = File.Exists(string.Format(_backupPath, CampusId)) ? JsonUtility.FromJson<MazemapBackupFile>(File.ReadAllText(string.Format(_backupPath, CampusId))) : new MazemapBackupFile();

        if (string.IsNullOrEmpty(text))
        {
            CampusJson campus;

            if (_mazemapBackup.Campuses.TryGetValue(CampusId, out campus))
            {
                Debug.Log("Couldn't connect to Mazemap: Initialising from backup");
                text = campus.BuildingsJson;
                _isUsingBackup = true;
                _shouldBackupMazemapData = false;
            }
            else
            {
                Debug.LogError("Couldn't connect to Mazemap - Backup not found: Exiting");
                return;
            }
        }

        if (_shouldBackupMazemapData && _mazemapBackup != null)
        {
            if (_mazemapBackup.Campuses == null)
                _mazemapBackup.Campuses = new SerializableDictionaryCampusJson();
            if (_mazemapBackup.Campuses.ContainsKey(CampusId))
                _mazemapBackup.Campuses.Remove(CampusId);

            _mazemapBackup.Campuses.Add(CampusId, new CampusJson
            {
                BuildingsJson = text,
                Floors = new SerializableDictionaryIntString(),
                Rooms = new SerializableDictionaryIntString()
            });
        }

        CampusLoaded = false;
        JSONObject collection = new JSONObject(text);
        foreach (JSONObject building in collection["buildings"])
        {
            Building b = new Building
            {
                Name = building["buildingNames"][0]["name"].str,
                Id = (int) building["buildingNames"][0]["id"].f,
                BuildingId = (int) building["buildingNames"][0]["buildingId"].f,
                AccessLevel = (int) building["accessLevel"].f,
                CampusId = (int) building["campusId"].f
            };

            foreach (JSONObject floor in building["floors"])
            {
                Floor f = new Floor
                {
                    Id = (int) floor["id"].f,
                    Name = floor["name"].str,
                    AccessLevel = (int) floor["accessLevel"].f,
                    FloorIndex = (int) floor["z"].f,
                    FloorOutlineId = (int) floor["floorOutlineId"].f,
                };
                if (f.FloorIndex > 0)
                    f.FloorIndex--;
                b.Floors.Add(f.Id, f);
            }
            Buildings.Add(b.Id, b);
        }

        StartCoroutine(DrawAllBuildings());
    }

    private void CreateRouteFromPath(string responseText)
    {
        List<GeoPointWGS84> routePoints = new List<GeoPointWGS84>();
        JSONObject pathRoot = new JSONObject(responseText)["path"]["features"];
        bool first = true;
        foreach (JSONObject routeLeg in pathRoot)
        {
            JSONObject coordinates = routeLeg["geometry"]["coordinates"];
            //TODO: Hardcoded value. Change to dynamic altitude
            float alt = 8;
            foreach (JSONObject coordinate in coordinates)
            {
                routePoints.Add(new GeoPointWGS84() {longitude = coordinate[0].n, latitude = coordinate[1].n, altitude = alt});
            }
            if (first)
                first = false;
            else
                routePoints.RemoveAt(routePoints.Count - 2);
        }
        WaypointController.Instance.CreateRoute(routePoints.Select(point => point.ToUTM().ToUnity()).ToList());
    }

    private IEnumerator DrawAllBuildings()
    {
        _campusParent = new GameObject("Campus " + CampusId).transform;
        BuildingsLeftToDraw = Buildings.Count;
        foreach (KeyValuePair<int, Building> pair in Buildings)
        {
            StartCoroutine(DrawBuilding(pair.Value));
        }
        while (BuildingsLeftToDraw > 0)
        {
            yield return new WaitForEndOfFrame();
        }
        SetActiveLayer(CurrentActiveLevel);
        CampusLoaded = true;
        File.WriteAllText(string.Format(_backupPath, CampusId), JsonUtility.ToJson(_mazemapBackup));

        if (OnFinishedGeneratingCampus != null)
            OnFinishedGeneratingCampus(CampusId);

        foreach (KeyValuePair<int, Building> building in Buildings)
        {
            foreach (KeyValuePair<int, Floor> floor in building.Value.Floors)
            {
                CreateFloorColliders(floor.Value.RenderedModel);
            }
        }
    }

    /// <summary>
    /// Draws floor outlines of building
    /// </summary>
    private IEnumerator DrawBuilding(Building building)
    {
        GameObject buildingObject = new GameObject(building.Name);
        buildingObject.transform.parent = _campusParent;
        building.RenderedModel = buildingObject.transform;
        List<Transform> floorObjects = new List<Transform>();

        foreach (KeyValuePair<int, Floor> pair in building.Floors)
        {
            Floor floor = pair.Value;
            WWW www = new WWW(string.Format(REST_FLOOROUTLINE, floor.Id));
            yield return www;
            string wwwText = www.text;
            if (_isUsingBackup)
            {
                string json;
                if (_mazemapBackup.Campuses[CampusId].Floors.TryGetValue(floor.Id, out json))
                {
                    wwwText = json;
                }
                else
                {
                    yield break;
                }
            }

            GameObject floorObject = DrawFloorOutlines(wwwText);
            floorObject.SetActive(false);
            floorObject.name = floor.Name;
            floorObjects.Add(floorObject.transform);

            /* TODO: Implement again when we using individual building measurements
            bool exists = false;
            if (_mazeMapMeasurements.GetCampusMeasurement(CampusId) != null)
                if (_mazeMapMeasurements.GetCampusMeasurement(CampusId).GetBuildingMeasurement(building.BuildingId) != null)
                {
                    exists = true;
                    floorObject.transform.position = floorObject.transform.position +
                        new Vector3(0, _mazeMapMeasurements.GetCampusMeasurement(CampusId).GetBuildingMeasurement(building.BuildingId).FloorHeights[floor.FloorIndex], 0);
                }
            if (!exists)
            */
            float y = 0;
            if (floor.FloorIndex >= 1) y = floor.FloorIndex * FloorHeightAboveGround;
            if (floor.FloorIndex < 1) y = floor.FloorIndex * FloorHeightBelowGround;
            floorObject.transform.position = floorObject.transform.position + new Vector3(0, y, 0);
            floor.RenderedModel = floorObject.transform;

            if (!_floorsByZ.ContainsKey(floor.FloorIndex))
            {
                _floorsByZ.Add(floor.FloorIndex, new List<Floor>());
                _zLayerActive.Add(floor.FloorIndex, false);
            }
            _floorsByZ[floor.FloorIndex].Add(floor);

            if (_shouldBackupMazemapData)
            {
                _mazemapBackup.Campuses[CampusId].Floors.Add(pair.Key, wwwText);
            }
        }

        foreach (Transform t in floorObjects)
            t.SetParent(buildingObject.transform, true);

        //RenderBuilding(buildingObject);
        CenterParentOnChildren(buildingObject.transform);
        StartCoroutine(DrawRoomsInBuilding(building));
    }

    /// <summary>
    /// Parses GeoJSON and creates floor outline points
    /// </summary>
    /// <returns>GameObject that contains floor</returns>
    private GameObject DrawFloorOutlines(string text)
    {
        JSONObject collection = new JSONObject(text);
        GameObject floor = new GameObject();
        floor.transform.position = Vector3.zero;
        List<Transform> points = new List<Transform>();

        foreach (JSONObject feature in collection["features"])
        {
            try
            {
                floor.name = feature["properties"]["floorIds"][0].f.ToString();
                JSONObject geometry = feature["geometry"];
                foreach (JSONObject pos in geometry["coordinates"])
                {
                    foreach (JSONObject coord in pos)
                    {
                        if (geometry["type"].str == "MultiPolygon")
                        {
                            GameObject multiPolygon = new GameObject("mp");

                            List<Transform> mpPoints = new List<Transform>();
                            foreach (JSONObject multiCoord in coord)
                            {
                                GameObject pointObject = InstantiatePoint(multiCoord);
                                mpPoints.Add(pointObject.transform);
                            }

                            foreach (Transform t in mpPoints)
                                t.SetParent(multiPolygon.transform, true);

                            points.Add(multiPolygon.transform);
                        }
                        else
                        {
                            GameObject pointObject = InstantiatePoint(coord);
                            pointObject.transform.SetParent(floor.transform, true);
                            points.Add(pointObject.transform);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                continue;
            }
        }

        foreach (Transform t in points)
            t.SetParent(floor.transform, true);

        return floor;
    }

    /// <summary>
    /// Instantiates point object from WGS84 or Mercator coordinates, by converting them to UCS
    /// </summary>
    /// <param name="isMercator">Is coordinate in Mercator, otherwise WGS84</param>
    /// <returns>Point</returns>
    private GameObject InstantiatePoint(JSONObject coordinate)
    {
        GeoPointUTM geoPointUtm;
        var geoPointWGS84 = new GeoPointWGS84
        {
            longitude = Double.Parse(coordinate[0].ToString(), NumberStyles.Float),
            latitude = Double.Parse(coordinate[1].ToString(), NumberStyles.Float),
        };
        geoPointUtm = geoPointWGS84.ToUTM();

        GameObject pointObject = new GameObject("point");
        if (!GeoUtils.UtmOriginSet)
        {
            GeoUtils.UtmOrigin = geoPointUtm;
            pointObject.name = "Zero";
        }

        pointObject.transform.position = geoPointUtm.ToUnity();
        return pointObject;
    }

    /// <summary>
    /// Creates outline of building using LineRenderers
    /// </summary>
    /// <param name="building">Building GameObject</param>
    private void RenderBuilding(GameObject building)
    {
        for (int floorIndex = 0; floorIndex < building.transform.childCount; floorIndex++)
        {
            Transform floor = building.transform.GetChild(floorIndex);
            List<Vector3> points = new List<Vector3>();

            for (int floorPointIndex = 0; floorPointIndex < floor.childCount; floorPointIndex++)
            {
                Transform floorPoint = floor.GetChild(floorPointIndex);
                if (floorPoint.childCount > 0)
                {
                    LineRenderer lr = floorPoint.gameObject.AddComponent<LineRenderer>();
                    lr.positionCount = floorPoint.childCount;
                    lr.material = _lineMaterial;
                    lr.startWidth = lr.endWidth = _lineWidth;

                    for (int j = 0; j < floorPoint.childCount; j++)
                    {
                        lr.SetPosition(j, floorPoint.GetChild(j).position);
                    }
                }
                else
                {
                    points.Add(floorPoint.position);
                }
            }
            if (points.Count > 0)
            {
                LineRenderer lr = floor.gameObject.AddComponent<LineRenderer>();
                lr.positionCount = points.Count;
                lr.material = _lineMaterial;
                lr.startWidth = lr.endWidth = _lineWidth;
                lr.SetPositions(points.ToArray());
            }
        }
    }

    /// <summary>
    /// Creates outline of POI using LineRenderers
    /// </summary>
    /// <param name="poi">GameObject containing POI</param>
    private void RenderPoiOutline(GameObject poi)
    {
        foreach (Transform pointContainer in poi.transform)
        {
            LineRenderer lineRenderer = pointContainer.gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = _lineMaterial;
            lineRenderer.startWidth = lineRenderer.endWidth = _lineWidth;
            lineRenderer.positionCount = pointContainer.childCount;
            int i = 0;
            foreach (Transform point in pointContainer)
            {
                lineRenderer.SetPosition(i, point.position);
                i++;
            }
        }
    }

    private IEnumerator DrawRoomsInBuilding(Building building)
    {
        foreach (KeyValuePair<int, Floor> pair in building.Floors)
        {
            WWW www = new WWW(string.Format(REST_POI_SEARCH_BY_FLOORID, pair.Value.Id));
            yield return www;
            string wwwText = www.text;

            if (_isUsingBackup)
            {
                string json;
                if (_mazemapBackup.Campuses[CampusId].Rooms.TryGetValue(pair.Value.Id, out json))
                {
                    wwwText = json;
                }
                else
                {
                    yield break;
                }
            }

            if (_shouldBackupMazemapData)
            {
                _mazemapBackup.Campuses[CampusId].Rooms.Add(pair.Value.Id, wwwText);
            }
            JSONObject pois = new JSONObject(wwwText)["pois"];
            if (pois == null)
                continue;

            foreach (JSONObject poi in pois)
            {
                DrawPoiOutline(poi);
            }
        }
        BuildingsLeftToDraw--;
    }

    private IEnumerator DrawPoiOutline(int poiId)
    {
        WWW www = new WWW(string.Format(REST_POI_SEARCH, poiId));
        yield return www;
        JSONObject poi = new JSONObject(www.text);
        DrawPoiOutline(poi);
    }

    /// <summary>
    /// Creates POI object from JSONObject and makes outline
    /// </summary>
    private void DrawPoiOutline(JSONObject poi)
    {
        int z = (int) poi["z"].f;
        if (z > 0) z--;
        string roomName = "Unnamed";
        if (poi["infos"].Count > 0)
            roomName = poi["infos"][0]["name"].str;
        string buildingName = poi["buildingName"].str;
        int floorId = (int) poi["floorId"].f;
        GameObject roomObject = new GameObject("Room " + roomName);
        Room room = new Room
        {
            Id = (int) poi["poiId"].f,
            Name = roomName,
            RenderedModel = roomObject.transform
        };

        foreach (JSONObject pos in poi["geometry"]["coordinates"])
        {
            GameObject roomContainer = new GameObject("points");
            foreach (JSONObject coord in pos)
            {
                GameObject pointObject = InstantiatePoint(coord);
                float y = GetBuildingByName(buildingName).Floors[floorId].RenderedModel.position.y;
                pointObject.transform.position = pointObject.transform.position + new Vector3(0, y, 0);
                pointObject.transform.SetParent(roomContainer.transform, true);
            }
            roomContainer.transform.SetParent(roomObject.transform, true);
        }
        roomObject.transform.SetParent(GetBuildingByName(buildingName).Floors[floorId].RenderedModel, true);
        GetBuildingByName(buildingName).Floors[floorId].Rooms.Add(room.Id, room);
        if (z != CurrentActiveLevel)
            roomObject.SetActive(false);

        //RenderPoiOutline(roomObject);
        CenterParentOnChildren(roomObject.transform);
    }

    private void SetLayerVisibility(int z, bool isVisible)
    {
        foreach (Floor floor in _floorsByZ[z])
            floor.RenderedModel.gameObject.SetActive(isVisible);
        _zLayerActive[z] = isVisible;
    }

    private void SetFloorRoomsVisibility(int z, bool isVisible)
    {
        foreach (Floor floor in _floorsByZ[z])
        {
            foreach (KeyValuePair<int, Room> pair in floor.Rooms)
            {
                pair.Value.RenderedModel.gameObject.SetActive(isVisible);
            }
        }
    }

    /// <summary>
    /// Recursively center parents on children (bottom up)
    /// </summary>
    /// <param name="parent">Root transform</param>
    private void CenterParentOnChildren(Transform parent)
    {
        Vector3 avrg = Vector3.zero;
        int childcount = parent.childCount;
        List<Transform> children = new List<Transform>();
        for (int i = 0; i < childcount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.childCount > 0)
            {
                CenterParentOnChildren(child);
            }
            children.Add(child);
            avrg += child.position;
        }

        foreach (Transform c in children)
            c.SetParent(null, true);

        avrg /= childcount;
        parent.position = avrg;

        foreach (Transform c in children)
            c.SetParent(parent, true);
    }

    private void CreateFloorColliders(Transform floor)
    {
        List<Vector3> floorOutlinePoints = new List<Vector3>();
        for (int i = 0; i < floor.childCount; i++)
        {
            Transform a = floor.GetChild(i);
            if (a.childCount > 0)
            {
                List<Vector3> points = new List<Vector3>();
                for (int j = 0; j < a.childCount; j++)
                {
                    Transform b = a.GetChild(j);
                    if (b.childCount > 0)
                    {
                        List<Vector3> points2 = new List<Vector3>();
                        foreach (Transform point in b)
                        {
                            points2.Add(point.position);
                        }
                        InstantiateCollidersForPointCollection(points2, b);
                    }
                    else
                    {
                        points.Add(b.position);
                    }
                }
                if (points.Count > 1)
                    InstantiateCollidersForPointCollection(points, a);
            }
            else
            {
                floorOutlinePoints.Add(a.position);
            }
        }

        if (floorOutlinePoints.Count > 1)
            InstantiateCollidersForPointCollection(floorOutlinePoints, floor);
    }

    private void InstantiateCollidersForPointCollection(List<Vector3> points, Transform parent)
    {
        for (int mp = 0; mp < points.Count - 1; mp++)
        {
            int targetIndex = mp + 1;
            if (mp == points.Count - 2)
                targetIndex = 0;
            Transform wallCollider = Instantiate(_colliderPrefab, parent).transform;
            wallCollider.position = points[mp];
            wallCollider.LookAt(points[targetIndex], Vector3.up);
            wallCollider.localScale = new Vector3(1, 1,
                Vector3.Distance(points[mp], points[targetIndex]));
        }
    }

    public void SetActiveLayer(int z)
    {
        if (!_floorsByZ.ContainsKey(z))
            return;
        SetFloorRoomsVisibility(CurrentActiveLevel, false);
        CurrentActiveLevel = z;
        SetFloorRoomsVisibility(CurrentActiveLevel, true);

        //Set all layers above active layer to not render
        for (int i = z + 1; _floorsByZ.ContainsKey(i); i++)
        {
            if (_zLayerActive[i])
                SetLayerVisibility(i, false);
        }

        //Set all layers below active layer to render
        for (int i = z; _floorsByZ.ContainsKey(i); i--)
        {
            if (!_zLayerActive[i])
                SetLayerVisibility(i, true);
        }
        float y = 0;
        if (CurrentActiveLevel >= 1) y = CurrentActiveLevel * FloorHeightAboveGround;
        if (CurrentActiveLevel < 1) y = CurrentActiveLevel * FloorHeightBelowGround;
        _triggerFloor.position = new Vector3(_triggerFloor.position.x, y, _triggerFloor.position.z);
    }

    public void ChangeActiveLayer(bool up)
    {
        if (up)
            SetActiveLayer(CurrentActiveLevel + 1);
        else
            SetActiveLayer(CurrentActiveLevel - 1);
    }

    public void GenerateCampus(int id)
    {
        if (OnStartedGeneratingCampus != null)
            OnStartedGeneratingCampus();
        CampusId = id;
        StartCoroutine(GetWWW(string.Format(REST_BUILDING_SEARCH, id), GetBuildings));
    }

    public Building GetBuildingByName(string buildingName)
    {
        foreach (KeyValuePair<int, Building> pair in Buildings)
            if (pair.Value.Name == buildingName)
                return pair.Value;
        return null;
    }

    /// <summary>
    /// Destroys all buildings to allow generation of new campus
    /// </summary>
    public void ClearAll()
    {
        foreach (KeyValuePair<int, Building> building in Buildings)
        {
            Destroy(building.Value.RenderedModel.gameObject);
        }
        Buildings = new Dictionary<int, Building>();
        _floorsByZ = new Dictionary<int, List<Floor>>();
        _zLayerActive = new Dictionary<int, bool>();
        GeoUtils.UtmOrigin = default(GeoPointUTM);
        GeoUtils.UtmOriginSet = false;
    }

    public void GetPath(GeoPointWGS84 from, GeoPointWGS84 to)
    {
        string url = string.Format(PATH_SEARCH, from.latitude, from.longitude, to.latitude, to.longitude).Replace(',', '.');
        StartCoroutine(GetWWW(url, CreateRouteFromPath));
    }

    public float GetHeightAboveFloor(float height)
    {
        for (int i = 0; i < _floorsByZ.Count; i++)
        {
            if (height < i * FloorHeightAboveGround)
            {
                return height - (i-1) * FloorHeightAboveGround;
            }
        }
        return 0;
    }
}