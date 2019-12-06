using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoToGazeButton : GazeObject
{
    [SerializeField] private Vector2 _command;

    public Vector2 GetCommand()
    {
        return _command;

    }

}
