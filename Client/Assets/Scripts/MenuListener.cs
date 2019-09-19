using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuListener : MonoBehaviour
{
    public GameObject menu;
    private bool visibility;

    private void Start()
    {
        if (menu.activeInHierarchy)
            menu.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            visibility = !visibility;
            menu.SetActive(visibility);
        }
    }

    public void Cancel()
    {
        GameObject.Find("Client").GetComponent<Client>().Cancel();
    }
}
