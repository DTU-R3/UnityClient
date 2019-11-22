using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoToGazePlane : MonoBehaviour {

    [SerializeField] private GameObject _wayPoint;


    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void UpdatePoint(RaycastHit hit)
    {
        _wayPoint.transform.position = hit.point;
    }
}
