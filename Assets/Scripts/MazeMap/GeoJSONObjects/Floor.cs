﻿using System.Collections.Generic;
using UnityEngine;

public class Floor
{
    public int Id;
    public string Name;
    public int AccessLevel;
    public int FloorIndex;
    public int FloorOutlineId;
    public Transform RenderedModel;
    public Dictionary<int, Room> Rooms = new Dictionary<int, Room>();

    public override string ToString()
    {
        return string.Format("Floor - Id:{0}, Name:{1}, AccessLevel:{2}, FloorIndex:{3}, FloorOutlineId:{4}", Id, Name, AccessLevel, FloorIndex, FloorOutlineId);
    }
}