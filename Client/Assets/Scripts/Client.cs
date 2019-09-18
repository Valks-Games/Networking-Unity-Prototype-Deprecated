#pragma warning disable 0618

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class Client : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject otherClientPrefab;

    private GameObject player;
    private Transform playerTransform;

    private Dictionary<int, GameObject> clients = new Dictionary<int, GameObject>();

    private const int MAX_CLIENTS = 32;
    private const int PORT = 7777;
    private const string SERVER_IP = "127.0.0.1";
    private const int BYTE_SIZE = 1024;

    private bool isStarted;

    private int hostId;
    private byte reliableChannel;
    private int connectionId;
    private byte error;

    private int playerConnectionId; // The unique connection ID of this particular client separating it from all the other clients.

    #region MonoBehaviour
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 60;
    }
    private void Update()
    {
        UpdateMessagePump();
    }
    #endregion

    #region Network
    public void InitializeNetwork()
    {
        NetworkTransport.Init();

        ConnectionConfig config = new ConnectionConfig();
        reliableChannel = config.AddChannel(QosType.Unreliable);

        HostTopology toplogy = new HostTopology(config, MAX_CLIENTS);

        hostId = NetworkTransport.AddHost(toplogy, 0);

        connectionId = NetworkTransport.Connect(hostId, SERVER_IP, PORT, 0, out error);

        Debug.Log(string.Format("Attempting to connect to {0}.", SERVER_IP));

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
                playerConnectionId = connectionId;
                Debug.Log("We have connected to the server.");
                StartCoroutine(LoadMainScene());
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("We have disconnected from the server.");
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
            case 1: // Server sending positions of all other clients.
                // Position data from our other clients.
                float x = BitConverter.ToSingle(recBuffer, 1);
                float y = BitConverter.ToSingle(recBuffer, 5);
                float z = BitConverter.ToSingle(recBuffer, 9);

                // Add new clients if they were not already added.
                /*if (!clients.ContainsKey(connectionId) && connectionId != playerConnectionId)
                {
                    GameObject otherClient = Instantiate(otherClientPrefab, Vector3.zero, Quaternion.identity);
                    clients.Add(connectionId, otherClient);
                }*/

                // Update the position of all the other clients except for ourself.
                /*foreach (KeyValuePair<int, GameObject> entry in clients)
                {
                    entry.Value.transform.position = new Vector3(x, y, z);
                }*/
                break;
            default:
                break;
        }
    }
    #endregion

    private IEnumerator LoadMainScene()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Main");

        // Wait until the asynchronous scene fully loads
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Scene is done loading.. create player!
        player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        playerTransform = player.transform;

        StartCoroutine(SendPlayerPosition());
    }

    private IEnumerator SendPlayerPosition()
    {
        while (true)
        {
            PlayerPositionData();
            yield return new WaitForSeconds(1);
        }
    }

    #region SendDataToServer
    public void PlayerPositionData()
    {
        Vector3 pos = playerTransform.position;

        byte[] buffer = new byte[BYTE_SIZE];
        buffer[0] = 1; // Position
        BitConverter.GetBytes(pos.x).CopyTo(buffer, 1);
        BitConverter.GetBytes(pos.y).CopyTo(buffer, 5);
        BitConverter.GetBytes(pos.z).CopyTo(buffer, 9);
        SendData(buffer);
    }

    private void SendData(byte[] buffer)
    {
        NetworkTransport.Send(hostId, connectionId, reliableChannel, buffer, BYTE_SIZE, out error);
    }
    #endregion
}
