#pragma warning disable 0618

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Server : MonoBehaviour
{
    private const int MAX_CLIENTS = 32;
    private const int PORT = 7777;
    private const int BYTE_SIZE = 1024;
    private bool isStarted;
    private byte error;
    private byte reliableChannel;
    private int hostId;

    private List<int> connectionIds = new List<int>();
    //private List<Player> players = new List<Player>();

    #region MonoBehaviour
    private void Start()
    {
        Application.targetFrameRate = 60;
        InitializeNetwork();
    }
    private void Update()
    {
        UpdateMessagePump();
    }
    #endregion

    #region Network
    private void InitializeNetwork()
    {
        NetworkTransport.Init();

        ConnectionConfig config = new ConnectionConfig();
        reliableChannel = config.AddChannel(QosType.Unreliable);

        HostTopology toplogy = new HostTopology(config, MAX_CLIENTS);

        hostId = NetworkTransport.AddHost(toplogy, PORT, null);

        Debug.Log(string.Format("Opening connection on {0}.", PORT));

        isStarted = true;
    }
    public void Shutdown()
    {
        isStarted = false;
        NetworkTransport.Shutdown();
    }
    public void UpdateMessagePump()
    {
        if (!isStarted)
            return;

        int recHostId;     // Is this from Web? Or standalone?
        int connectionId;  // Which user is sending me this?
        int channelId;     // Which lane are they sending the message?

        byte[] recBuffer = new byte[BYTE_SIZE];
        int dataSize;

        NetworkEventType type = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, recBuffer.Length, out dataSize, out error);
        switch (type)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                connectionIds.Add(connectionId);
                //players.Add(new Player(connectionId));
                Debug.Log(string.Format("User {0} has connected.", connectionId));
                break;
            case NetworkEventType.DisconnectEvent:
                connectionIds.Remove(connectionId);
                /*foreach (var player in players)
                    if (player.connectionId == connectionId)
                        players.Remove(player);*/
                Debug.Log(string.Format("User {0} has disconnected.", connectionId));
                break;
            case NetworkEventType.DataEvent:
                HandleData(connectionId, recBuffer);
                break;
            default:
            case NetworkEventType.BroadcastEvent:
                Debug.Log("Unexpected network event type.");
                break;
        }
    }
    private void HandleData(int connectionId, byte[] recBuffer)
    {
        // Check Message Type
        switch (recBuffer[0])
        {
            case 0: // Player joined data.
                // Unused / Deprecated
                break;
            case 1: // Player position data.
                // Data Recieved
                float x = BitConverter.ToSingle(recBuffer, 1);
                float y = BitConverter.ToSingle(recBuffer, 5);
                float z = BitConverter.ToSingle(recBuffer, 9);

                /*foreach (var player in players)
                    if (player.connectionId == connectionId)
                        player.UpdatePosition(new Vector3(x, y, z));*/

                byte[] buffer = new byte[BYTE_SIZE];
                buffer[0] = 1; // Of Type Position Data
                buffer[1] = (byte) connectionId;
                BitConverter.GetBytes(x).CopyTo(buffer, 2); // Position Data
                BitConverter.GetBytes(y).CopyTo(buffer, 6);
                BitConverter.GetBytes(z).CopyTo(buffer, 10);

                for (int i = 0; i < connectionIds.Count; i++)
                    if (connectionIds[i] != connectionId)
                        SendData(connectionIds[i], buffer);
                break;
            default:
                break;
        }
    }
    #endregion

    #region SendDataToClient
    private void SendData(int connectionId, byte[] buffer)
    {
        NetworkTransport.Send(hostId, connectionId, reliableChannel, buffer, BYTE_SIZE, out error);
    }
    #endregion
}
