using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player
{
    public Vector3 position;
    public int connectionId;

    public Player(int connectionId)
    {
        this.connectionId = connectionId;
    }

    public void UpdatePosition(Vector3 position)
    {
        this.position = position;
    }
}
