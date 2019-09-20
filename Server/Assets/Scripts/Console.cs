using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Console : MonoBehaviour
{
    public GameObject UICanvas;
    public GameObject UITextPrefab;
    public GameObject UIInput;
    public GameObject ServerGo;

    private const int MAX_CONSOLE_LINES = 15;
    private List<GameObject> UITextLines = new List<GameObject>();
    private int lines = 0;

    private InputField UIInputField;
    private Server server;

    private void Awake()
    {
        UIInputField = UIInput.GetComponent<InputField>();
        server = ServerGo.GetComponent<Server>();
    }

    public void Print(string message)
    {
        Print(message, Color.white);
    }

    public void Print(string message, Color color)
    {
        if (UITextLines.Count >= MAX_CONSOLE_LINES)
        {
            Destroy(UITextLines[0]);
            UITextLines.RemoveAt(0);
            lines--;
        }

        GameObject textGo = Instantiate(UITextPrefab, UITextPrefab.transform.position, Quaternion.identity);
        Text text = textGo.GetComponent<Text>();
        text.text = message;
        text.color = color;

        const int VERTICAL_LINE_SPACING = 25;

        Transform textGoTransform = textGo.transform;
        textGoTransform.SetParent(UICanvas.transform);
        UITextLines.Add(textGo);

        for (int i = 0; i < lines; i++)
        {
            UITextLines[i].transform.Translate(new Vector3(0, VERTICAL_LINE_SPACING, 0));
        }

        lines++;
    }

    public void HandleCommands()
    {
        if (!Input.GetKeyDown(KeyCode.Return))
            return;

        string input = UIInputField.text;
        string[] args = input.Split();
        switch(args[0])
        {
            case "kick":
                if (args.Length <= 1)
                {
                    Print("Error: Command kick requires <user> to kick", Color.red);
                }
                else
                {
                    if (server.Kick(int.Parse(args[1])))
                    {
                        Print("Kicked User " + args[1]);
                    }
                    else
                    {
                        Print("User " + args[1] + " is not connected to the server.");
                    }
                }
                break;
            case "exit":
                Application.Quit();
                break;
            case "start":
                if (!server.isStarted)
                {
                    server.InitializeNetwork();
                }
                else
                {
                    Print("Server is already running!");
                }
                break;
            case "stop":
                if (server.isStarted)
                {
                    server.Shutdown();
                    Print("Stopped server.");
                }
                else
                {
                    Print("Server is not running!");
                }
                break;
            case "restart":
                if (server.isStarted)
                {
                    server.Shutdown();
                    Print("Stopped server.");
                    server.InitializeNetwork();
                }
                else
                {
                    Print("Server is not running!");
                }
                break;
            case "say":
                if (args.Length > 1)
                {
                    string msg = "";
                    for (int i = 1; i < args.Length; i++)
                    {
                        msg += args[i] + " ";
                    }
                    Print(msg);
                }
                else
                {
                    Print("Error: Command \"say\" requires a <message>", Color.red);
                }
                break;
            default:
                if (!args[0].Equals(""))
                    Print("Error: Unknown command \"" + args[0] + "\"", Color.red);
                break;
        }

        UIInputField.text = "";
        UIInputField.Select();
        UIInputField.ActivateInputField();
    }
}
