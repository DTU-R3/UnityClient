﻿using UnityEngine;

class Floor
{
    public int Id;
    public string Name;
    public int AccessLevel;
    public int Z;
    public int FloorOutlineId;
    public Transform RenderedModel;

    public override string ToString()
    {
        return string.Format("Floor - Id:{0}, Name:{1}, AccessLevel:{2}, Z:{3}, FloorOutlineId:{4}", Id, Name, AccessLevel, Z, FloorOutlineId);
    }
}