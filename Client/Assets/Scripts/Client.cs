#pragma warning disable 0618

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Client : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject otherClientPrefab;

    private GameObject player;
    private Transform playerTransform;

    private InputField inputField;

    private Dictionary<int, GameObject> clients = new Dictionary<int, GameObject>();
    private Dictionary<int, Vector3> newPositions = new Dictionary<int, Vector3>();

    private const int MAX_CLIENTS = 32;
    private const int PORT = 7777;
    private const int REC_BYTE_SIZE = 1024;
    private const float SEND_DELAY = 0.1f;

    private string serverIp;

    private bool isStarted;
    private bool loadingScene;

    private int hostId;
    private byte reliableChannel;
    private int connectionId;
    private byte error;
    
    private Vector3 prevPos = Vector3.zero;

    private IEnumerator playerPositionDataCoroutine;

    #region MonoBehaviour
    private void Awake()
    {
        inputField = GameObject.Find("InputField").GetComponent<InputField>();
    }
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 60;
        inputField.text = "127.0.0.1";
    }
    private void Update()
    {
        UpdateMessagePump();
    }
    private void FixedUpdate()
    {
        foreach (KeyValuePair<int, GameObject> client in clients)
        {
            Transform clientTransform = client.Value.transform;
            clientTransform.position = Vector3.Lerp(clientTransform.position, newPositions[client.Key], 0.06f);
        }
    }
    #endregion

    #region Network
    public void Connect()
    {
        StartCoroutine(LoadScene("Info"));
        StartCoroutine(InitializeNetwork());
    }

    public void Cancel()
    {
        Shutdown();
        StartCoroutine(LoadScene("Game Menu"));
        Destroy(gameObject);
    }

    private IEnumerator InitializeNetwork()
    {
        serverIp = inputField.text;

        while (loadingScene)
            yield return new WaitForSeconds(0.1f);

        Button button = GameObject.Find("Button").GetComponent<Button>();
        button.onClick.AddListener(delegate { Cancel(); });

        NetworkTransport.Init();

        ConnectionConfig config = new ConnectionConfig();
        reliableChannel = config.AddChannel(QosType.Unreliable);

        HostTopology toplogy = new HostTopology(config, MAX_CLIENTS);

        hostId = NetworkTransport.AddHost(toplogy, 0);

        connectionId = NetworkTransport.Connect(hostId, serverIp, PORT, 0, out error);

        Debug.Log(string.Format("Attempting to connect to {0} on port {1}.", serverIp, PORT));

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

        if ((NetworkError)error != NetworkError.Ok)
        {
            Debug.Log("There was a networking error: " + (NetworkError)error);
            return;
        }


        byte[] recBuffer = new byte[REC_BYTE_SIZE];

        NetworkEventType type = NetworkTransport.Receive(out int recHostId, out int connectionId, out int channelId, recBuffer, recBuffer.Length, out int dataSize, out error);
        switch (type)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("We have connected to the server.");
                StartCoroutine(LoadScene("Main"));
                // Scene is done loading.. create player!
                StartCoroutine(CreatePlayer());
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("We have disconnected from the server.");
                if (playerPositionDataCoroutine != null)
                    StopCoroutine(playerPositionDataCoroutine);
                Shutdown();
                StartCoroutine(LoadScene("Game Menu"));
                Destroy(gameObject);
                break;
            case NetworkEventType.DataEvent:
                HandleData(recBuffer);
                break;
            default:
            case NetworkEventType.BroadcastEvent:
                Debug.Log("Unexpected network event type.");
                break;
        }
    }
    private void HandleData(byte[] recBuffer)
    {
        if (recBuffer[0] == 0) // Server informing us a client has disconnected.
        {
            int otherClientId = recBuffer[1];

            if (otherClientId != connectionId)
            {
                Destroy(clients[otherClientId]);
                clients.Remove(otherClientId);
                newPositions.Remove(otherClientId);
            }
        }

        if (recBuffer[0] == 1) // Server sending positions of all other clients.
        {
            // Position data from our other clients.
            int playerCount = recBuffer[1];

            int i = 0;
            for (int n = 0; n < playerCount; n++)
            {
                int connectionId = recBuffer[i + 2];
                float x = BitConverter.ToSingle(recBuffer, i + 3);
                float y = BitConverter.ToSingle(recBuffer, i + 7);
                float z = BitConverter.ToSingle(recBuffer, i + 11);

                // Add new clients if they were not already added.
                if (!clients.ContainsKey(connectionId))
                {
                    GameObject otherClient = Instantiate(otherClientPrefab, Vector3.zero, Quaternion.identity);
                    clients.Add(connectionId, otherClient);
                    newPositions.Add(connectionId, new Vector3(x, y, z));
                }

                newPositions[connectionId] = new Vector3(x, y, z);

                i += 13;
            }
        }
    }
    #endregion

    private IEnumerator LoadScene(string scene)
    {
        loadingScene = true;
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene);

        // Wait until the asynchronous scene fully loads
        while (!asyncLoad.isDone)
        {
            yield return null;
            loadingScene = false;
        }
    }

    private IEnumerator CreatePlayer()
    {
        while (loadingScene)
            yield return new WaitForSeconds(0.1f);

        player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        playerTransform = player.transform;

        // Start sending player position data!
        playerPositionDataCoroutine = SendPlayerPosition();
        StartCoroutine(playerPositionDataCoroutine);
    }

    private IEnumerator SendPlayerPosition()
    {
        while (true)
        {
            PlayerPositionData();
            yield return new WaitForSeconds(SEND_DELAY);
        }
    }

    #region SendDataToServer
    public void PlayerPositionData()
    {
        Vector3 pos = playerTransform.position;

        if (Vector3.Distance(pos, prevPos) >= 0.1)
        {
            byte[] buffer = new byte[16];
            buffer[0] = 1; // Position
            BitConverter.GetBytes(pos.x).CopyTo(buffer, 1);
            BitConverter.GetBytes(pos.y).CopyTo(buffer, 5);
            BitConverter.GetBytes(pos.z).CopyTo(buffer, 9);
            SendData(buffer);
        }

        prevPos = pos;
    }

    private void SendData(byte[] buffer)
    {
        NetworkTransport.Send(hostId, connectionId, reliableChannel, buffer, buffer.Length, out error);
    }
    #endregion
}
