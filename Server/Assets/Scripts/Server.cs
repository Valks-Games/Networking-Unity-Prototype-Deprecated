#pragma warning disable 0618

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Networking;

public class Server : MonoBehaviour
{
    public GameObject consoleGameObject;

    private Console console;

    private const int MAX_CLIENTS = 32;
    private const int PORT = 7777;
    private const int REC_BYTE_SIZE = 1024;
    private const float SEND_DELAY = 0.1f;

    private byte error;
    private byte reliableChannel;
    private int hostId;

    private bool sendData;

    public bool isStarted { get; private set; }

    private List<int> connectionIds = new List<int>();

    private IEnumerator sendingData;

    private Dictionary<int, Vector3> prevPositions = new Dictionary<int, Vector3>();

    #region MonoBehaviour
    private void Awake()
    {
        console = consoleGameObject.GetComponent<Console>();
    }
    private void Start()
    {
        Application.targetFrameRate = 60;
        console.Print("Initializing Network");
        InitializeNetwork();
    }
    private void Update()
    {
        UpdateMessagePump();
    }
    #endregion

    private IEnumerator SendData()
    {
        sendData = true;
        while (sendData)
        {
            List<Player> players = Player.GetPlayers();

            int numPlayers = players.Count;

            List<int> exclude = new List<int>();

            foreach (Player player in players.ToArray())
            {
                if (prevPositions.ContainsKey(player.connectionId))
                {
                    if (player.position.Equals(prevPositions[player.connectionId]))
                    {
                        exclude.Add(player.connectionId);
                        numPlayers--;
                    }
                }
            }

            if (numPlayers > 0)
            {
                // Create the buffer.
                byte[] buffer = new byte[numPlayers * 16];
                buffer[0] = 1; // Of Type Position Data

                buffer[1] = (byte)players.Count;

                int i = 0; // Index
                foreach (Player player in players.ToArray())
                {
                    if (exclude.Contains(player.connectionId))
                        continue;

                    buffer[i + 2] = (byte)player.connectionId;

                    Vector3 pos = player.position;

                    BitConverter.GetBytes(pos.x).CopyTo(buffer, i + 3); // Position Data
                    BitConverter.GetBytes(pos.y).CopyTo(buffer, i + 7);
                    BitConverter.GetBytes(pos.z).CopyTo(buffer, i + 11);

                    i += 13;
                }

                // Finished creating the buffer.
                // Send the buffer to everyone.
                foreach (Player player in players.ToArray())
                {
                    if (exclude.Contains(player.connectionId))
                        continue;

                    SendData(player.connectionId, buffer);
                }

                foreach (Player player in players.ToArray())
                {
                    if (!prevPositions.ContainsKey(player.connectionId))
                    {
                        prevPositions.Add(player.connectionId, player.position);
                    }
                }
            }

            yield return new WaitForSeconds(SEND_DELAY);
        }
    }

    #region Network
    public void InitializeNetwork()
    {
        NetworkTransport.Init();

        ConnectionConfig config = new ConnectionConfig();
        reliableChannel = config.AddChannel(QosType.Unreliable);

        HostTopology toplogy = new HostTopology(config, MAX_CLIENTS);

        hostId = NetworkTransport.AddHost(toplogy, PORT, null);

        Debug.Log(string.Format("Opening connection on {0}.", PORT));
        console.Print("Opening connection on " + PORT + ".");

        isStarted = true;

        sendingData = SendData();
        StartCoroutine(sendingData);
    }
    public bool Kick(int connectionId)
    {
        NetworkTransport.Disconnect(hostId, connectionId, out error);
        if ((NetworkError)error != NetworkError.Ok)
        {
            return false;
        }
        return true;
    }
    public void Shutdown()
    {
        isStarted = false;
        sendData = false;
        StopCoroutine(sendingData);
        NetworkTransport.Shutdown();
    }
    public void UpdateMessagePump()
    {
        if (!isStarted)
            return;
        // Which lane are they sending the message?

        byte[] recBuffer = new byte[REC_BYTE_SIZE];

        NetworkEventType type = NetworkTransport.Receive(out int recHostId, out int connectionId, out int channelId, recBuffer, recBuffer.Length, out int dataSize, out error);
        switch (type)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                connectionIds.Add(connectionId);
                Player.Add(connectionId);
                Debug.Log(string.Format("User {0} has connected.", connectionId));
                console.Print("User " + connectionId + " has connected.");
                break;
            case NetworkEventType.DisconnectEvent:
                connectionIds.Remove(connectionId);
                Player.Remove(connectionId);
                Debug.Log(string.Format("User {0} has disconnected.", connectionId));
                console.Print("User " + connectionId + " has disconnected.");

                // Inform all other clients this client has disconnected.
                byte[] buffer = new byte[2];
                buffer[0] = 0;
                buffer[1] = (byte)connectionId;

                foreach (Player player in Player.GetPlayers().ToArray())
                    if (player.connectionId != connectionId)
                        SendData(player.connectionId, buffer);
                break;
            case NetworkEventType.DataEvent:
                HandleData(connectionId, recBuffer);
                break;
            default:
            case NetworkEventType.BroadcastEvent:
                Debug.Log("Unexpected network event type.");
                console.Print("Unexpected network event type.");
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

                Player.UpdatePosition(connectionId, new Vector3(x, y, z));

                /*byte[] buffer = new byte[BYTE_SIZE];
                buffer[0] = 1; // Of Type Position Data
                buffer[1] = (byte) connectionId;
                BitConverter.GetBytes(x).CopyTo(buffer, 2); // Position Data
                BitConverter.GetBytes(y).CopyTo(buffer, 6);
                BitConverter.GetBytes(z).CopyTo(buffer, 10);

                for (int i = 0; i < connectionIds.Count; i++)
                    if (connectionIds[i] != connectionId)
                        SendData(connectionIds[i], buffer);*/
                break;
            default:
                break;
        }
    }
    #endregion

    #region SendDataToClient
    private void SendData(int connectionId, byte[] buffer)
    {
        NetworkTransport.Send(hostId, connectionId, reliableChannel, buffer, buffer.Length, out error);
    }
    #endregion
}
