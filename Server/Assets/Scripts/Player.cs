using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player
{
    private static List<Player> players = new List<Player>();
    public Vector3 position;
    public int connectionId;

    public Player(int connectionId)
    {
        this.connectionId = connectionId;
    }

    public static void Add(int connectionId)
    {
        players.Add(new Player(connectionId));
    }

    public static void Remove(int connectionId)
    {
        // We copy the players to a new array so we don't modify the old enumeration during processing.
        foreach (var player in players.ToArray())
            if (player.connectionId == connectionId)
                players.Remove(player);
    }

    public static void UpdatePosition(int connectionId, Vector3 position)
    {
        foreach (var player in players)
            if (player.connectionId == connectionId)
                player.position = position;
    }

    public static List<Player> GetPlayers()
    {
        return players;
    }
}
