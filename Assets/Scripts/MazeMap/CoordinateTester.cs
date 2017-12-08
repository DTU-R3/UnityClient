﻿using System.Collections;
using System.Collections.Generic;
using Assets.Scripts;
using UnityEngine;

public class CoordinateTester : MonoBehaviour
{
    [SerializeField] private Transform _testPoint;
    [SerializeField] private bool _test;

    [SerializeField] private double lon = 0;
    [SerializeField] private double lat = 0;
    [SerializeField] private bool _testCoord;

    [SerializeField] private Transform _testDistancePointA;
    [SerializeField] private Transform _testDistancePointB;
    [SerializeField] private bool _testDistance;

    private int i = 10;
    // Update is called once per frame
    void Update () {
	    if (Input.GetKeyDown(KeyCode.Space))
	    {
            ArlobotROSController.Instance.Move(transform.position);
        }

	    if (_test)
	    {
	        GeoPointMercator coordinatesMercator = _testPoint.transform.position.ToMercator();
	        Debug.Log("Test point - Mercator: " + coordinatesMercator);
	        GeoPointWGS84 wgs84 = coordinatesMercator.ToWGS84();
            Debug.Log("Test point - WGS84: " + wgs84);
	        GeoPointUTM utm = wgs84.ToUTM();
            Debug.Log("Test point - UTM: " + utm);

            _test = false;
	    }
	    if (_testCoord) {
	        GeoPointWGS84 wgs84 = new GeoPointWGS84 {
	            latitude = lat,
	            longitude = lon,
	            altitude = 0,
	        };

	        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
	        point.transform.position = wgs84.ToMercator().ToUnity();
	        _testCoord = false;
	    }
        if (_testDistance)
        {
            Debug.Log(Vector3.Distance(_testDistancePointA.position, _testDistancePointB.position) + "meters");
            _testDistance = false;
        }
        Debug.DrawRay(transform.position, Vector3.forward * 100);
    }
}
