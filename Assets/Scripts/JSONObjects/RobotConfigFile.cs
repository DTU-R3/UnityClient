﻿using System;

[Serializable]
public class RobotConfigFile
{
    public int[] Campuses;
    public string RosMasterUri;
    public float WaypointDistanceThreshold;
    public float MaxLinearSpeed;
    public float MaxAngularSpeed;
    public float LinearSpeedParameter;
    public float AngularSpeedParameter;
}