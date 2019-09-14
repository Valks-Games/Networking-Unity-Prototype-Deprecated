#pragma warning disable 0618

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Server : MonoBehaviour
{
    private const int MAX_CLIENTS = 32;
    private const int PORT = 7777;

    private void Start()
    {
        Application.targetFrameRate = 60;
        InitializeNetwork();
    }

    private void InitializeNetwork()
    {
        NetworkTransport.Init();

        ConnectionConfig config = new ConnectionConfig();
        config.AddChannel(QosType.Unreliable);

        HostTopology toplogy = new HostTopology(config, MAX_CLIENTS);

        NetworkTransport.AddHost(toplogy, PORT, null);

        Debug.Log(string.Format("Opening connection on {0}.", PORT));
    }
}
