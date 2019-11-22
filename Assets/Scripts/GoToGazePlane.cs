using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoToGazePlane : MonoBehaviour {

    [SerializeField] private GameObject _wayPoint;
    [SerializeField] private int _minNumOfPoints = 40;
    [SerializeField] private float _verificationRadius = 10f;
    private List<Vector3> _gazePoints;


    // Use this for initialization
    void Start () {
		_gazePoints = new List<Vector3>();
	}
	
	// Update is called once per frame
	void Update () {
		
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
                float process = (float)_gazePoints.Count / _minNumOfPoints;

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
                _wayPoint.SetActive(false);

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
}
