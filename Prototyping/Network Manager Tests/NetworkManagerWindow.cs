using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Isaac.Network.Transports;
using Isaac.Network;
using Isaac.Network.Development;

public class NetworkManagerWindow : MonoBehaviour
{
    public bool isShowing { get; private set; }

    //UI References
    Button visibleToggleButton;
    Image bigWindow;
    Image smallWindow;
    Button startHostButton;
    Button startClientButton;
    Button stopButton;
    NetworkManager networkManager;
    TextMeshProUGUI clientsText;
    TextMeshProUGUI statusText;
    TextMeshProUGUI bottomLeftText;
    TextMeshProUGUI bottomRightText;
    TextMeshProUGUI infoText;

    NetworkLogModule networkLogModule;

    void Awake()
    {
        bigWindow = transform.Find("Big Window").GetComponent<Image>();
        smallWindow = transform.Find("Small Window").GetComponent<Image>();

        visibleToggleButton = smallWindow.transform.Find("Visible Toggle Button").GetComponent<Button>();
        visibleToggleButton.onClick.AddListener(OnVisibleToggleButtonPressed);
        clientsText = smallWindow.transform.Find("Clients Text").GetComponent<TextMeshProUGUI>();
        statusText = smallWindow.transform.Find("Status Text").GetComponent<TextMeshProUGUI>();

        startHostButton = bigWindow.transform.Find("Start Host Button").GetComponent<Button>();
        startHostButton.onClick.AddListener(OnStartHostButtonPressed);
        startClientButton = bigWindow.transform.Find("Start Client Button").GetComponent<Button>();
        startClientButton.onClick.AddListener(OnStartClientButtonPressed);
        bottomLeftText = bigWindow.transform.Find("Bottom Left Text").GetComponent<TextMeshProUGUI>();
        bottomRightText = bigWindow.transform.Find("Bottom Right Text").GetComponent<TextMeshProUGUI>();
        infoText = bigWindow.transform.Find("Info Text").GetComponent<TextMeshProUGUI>();
        stopButton = bigWindow.transform.Find("Stop Button").GetComponent<Button>();
        stopButton.onClick.AddListener(OnStopButtonPressed);
        networkManager = NetworkManager.Get();
        if(networkManager == null)
        {
            networkManager = new GameObject("Network Manager").AddComponent<NetworkManager>();
        }

        Hide();
    }

    void Start()
    {
        SetupNetworkManager();
    }

    private void SetupNetworkManager()
    {
        //Choose Transport
        //networkManager.transport = new UnetTransport();
        networkManager.transport = new EnetTransport();

        //Input address
        networkManager.transport.address = "127.0.0.1";
        networkManager.transport.port = 7525;

        //Setup specific transport
        if(networkManager.transport is UnetTransport)
        {
            UnetTransport transport = (UnetTransport)networkManager.transport;
            transport.ServerListenPort = 7525;
        }
        else if(networkManager.transport is EnetTransport)
        {
            EnetTransport transport = (EnetTransport)networkManager.transport;
        }

        //Create config with default values
        networkManager.config = new NetworkConfig() { };

        //Subscribe events
        networkManager.onInitialize += OnNetworkInit;
        networkManager.onShutdown += OnNetworkShutdown;
        networkManager.onClientConnect += OnClientConnect;
    }

    void Update()
    {
        if(networkManager.isRunning)
        {
            startHostButton.gameObject.SetActive(false);
            startClientButton.gameObject.SetActive(false);
            stopButton.gameObject.SetActive(true);
            statusText.text = "Running";
            if(networkManager.isHost)
            {
                clientsText.text = "[Host] ID: " + networkManager.clientID + " Connected Clients: " + networkManager.connectedClients.Count;
                statusText.text += " [Listening]";
            }
            else if(networkManager.isClient)
            {
                clientsText.text = "[Client] ID: " + networkManager.clientID;
                if(networkManager.isConnected)
                    statusText.text += " [Connected]";
                else
                    statusText.text += " [Connecting...]";
            }
            infoText.text = "Network Time: " + networkManager.networkTime.ToString("F5");
            infoText.text += "\n" + "Server ID: " + networkManager.serverID;
        }
        else
        {
            statusText.text = "Not running";
            clientsText.text = "";
            infoText.text = "";
            bottomLeftText.text = "";
            bottomRightText.text = "";
            startHostButton.gameObject.SetActive(true);
            startClientButton.gameObject.SetActive(true);
            stopButton.gameObject.SetActive(false);
        }
    }

    public void Show()
    {
        isShowing = true;
        visibleToggleButton.GetComponentInChildren<TextMeshProUGUI>().text = "Hide";
        smallWindow.enabled = false;
        bigWindow.enabled = true;
        bigWindow.gameObject.SetActive(true);
    }

    public void Hide()
    {
        isShowing = false;
        visibleToggleButton.GetComponentInChildren<TextMeshProUGUI>().text = "Show";
        smallWindow.enabled = true;
        bigWindow.gameObject.SetActive(false);
    }

    void OnStartHostButtonPressed()
    {
        networkManager.StartHost();
    }

    void OnStartClientButtonPressed()
    {
        networkManager.StartClient();
    }

    void OnStopButtonPressed()
    {
        if(networkManager.isHost)
        {
            networkManager.StopHost();
        }
        else if(networkManager.isClient)
        {
            networkManager.StopClient();
        }
    }

    void OnNetworkInit()
    {
        if(networkManager.isServer)
            Debug.Log("Server Started");

        networkLogModule = networkManager.GetModule<NetworkLogModule>();
        //networkLogModule.onReceivedNetworkLog += OnNetworkLog;
        bottomRightText.text = "";
    }

    void OnNetworkShutdown()
    {
        networkLogModule.onReceivedNetworkLog -= OnNetworkLog;
        networkLogModule = null;
    }

    void OnNetworkLog(ulong clientID, string condition, string stackTrace, LogType logType)
    {
        bottomRightText.text += "[";
        switch(logType)
        {
            case LogType.Error:
                bottomRightText.text += "<color=#FF0000>";
                break;
            case LogType.Assert:
                bottomRightText.text += "<color=#FF0000>";
                break;
            case LogType.Warning:
                bottomRightText.text += "<color=#FFEB04>";
                break;
            case LogType.Log:
                bottomRightText.text += "<color=#FFFFFF>";
                break;
            case LogType.Exception:
                bottomRightText.text += "<color=#FF0000>";
                break;
            default:
                break;
        }

        bottomRightText.text += logType.ToString();
        bottomRightText.text += "</color>";
        bottomRightText.text += "] ";

        if(networkManager.isServer)
        {
            bottomRightText.text += "Client " + clientID + ": ";
        }
        else
        {
            bottomRightText.text += "Server: ";
        }
        bottomRightText.text += condition + "\n";
    }

    void OnClientConnect(ulong clientID)
    {
        if(networkManager.isServer)
            networkLogModule.ToggleRemoteTarget(clientID, true, false);
        else
            networkLogModule.ToggleRemoteTarget(networkManager.serverID, true, false);
    }

    private void OnVisibleToggleButtonPressed()
    {
        if(isShowing)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }
}
