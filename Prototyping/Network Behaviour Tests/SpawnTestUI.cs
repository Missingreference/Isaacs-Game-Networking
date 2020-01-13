using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Isaac.Network;
using Isaac.Network.Exceptions;

public class SpawnTestUI : MonoBehaviour
{

    //A short hand for setting the target network behaviour's unique ID
    public string uniqueID = "My Unique ID";

    
    private NetworkManager m_NetworkManager;

    private NetworkUISquare leftNetworkSquare;
    private NetworkUISquare rightNetworkSquare;

    //UI References
    private RectTransform m_LeftWindow;
    private TextMeshProUGUI m_LeftTitleText;
    private Button m_LeftInstantiateButton;
    private Button m_LeftDestroyButton;
    private Button m_LeftSpawnButton;
    private TextMeshProUGUI m_LeftStatusText;
    private RectTransform m_LeftObjectPositioner;
    private Toggle m_LeftSpawnInitToggle;
    private Toggle m_LeftDestroyToggle;
    private Toggle m_LeftOwnerUnspawnToggle;


    private RectTransform m_RightObjectPositioner;


    void Awake()
    {
        m_LeftWindow = (RectTransform)transform.Find("Left Window");

        m_LeftTitleText = m_LeftWindow.Find("Title").GetComponent<TextMeshProUGUI>();
        m_LeftInstantiateButton = m_LeftWindow.Find("Instantiate Button").GetComponent<Button>();
        m_LeftInstantiateButton.onClick.AddListener(OnLeftInstantiateButtonPressed);
        m_LeftDestroyButton = m_LeftWindow.Find("Destroy Button").GetComponent<Button>();
        m_LeftDestroyButton.onClick.AddListener(OnLeftDestroyButtonPressed);
        m_LeftSpawnButton = m_LeftWindow.Find("Spawn Button").GetComponent<Button>();
        m_LeftSpawnButton.onClick.AddListener(OnLeftSpawnButtonPressed);
        m_LeftStatusText = m_LeftWindow.Find("Status Text").GetComponent<TextMeshProUGUI>();
        m_LeftObjectPositioner = (RectTransform)transform.Find("Left Object Position");
        m_LeftSpawnInitToggle = m_LeftWindow.Find("Spawn Network Init Toggle").GetComponent<Toggle>();
        m_LeftSpawnInitToggle.onValueChanged.AddListener(OnLeftSpawnInitToggleChanged);
        m_LeftDestroyToggle = m_LeftWindow.Find("Destroy Unspawn Toggle").GetComponent<Toggle>();
        m_LeftDestroyToggle.onValueChanged.AddListener(OnLeftDestroyUnspawnToggleChanged);
        m_LeftOwnerUnspawnToggle = m_LeftWindow.Find("Owner Unspawn Toggle").GetComponent<Toggle>();
        m_LeftOwnerUnspawnToggle.onValueChanged.AddListener(OnLeftOwnerUnspawnToggleChanged);

        m_RightObjectPositioner = (RectTransform)transform.Find("Right Object Position");
    }

    void Start()
    {
        m_NetworkManager = NetworkManager.Get();
        m_NetworkManager.transport.RegisterChannel("Rotate Channel", ChannelType.UnreliableSequenced);
    }

    void Update()
    {
        UpdateLeftWindow();
        UpdateRightWindow();
    }

    private void UpdateLeftWindow()
    {
        if(leftNetworkSquare == null)
        {
            m_LeftTitleText.text = "With Unique ID";
            m_LeftInstantiateButton.interactable = true;
            m_LeftDestroyButton.interactable = false;
            m_LeftSpawnButton.interactable = false;
            m_LeftSpawnButton.GetComponentInChildren<TextMeshProUGUI>().text = "Spawn On Network";
            m_LeftStatusText.text = "Awaiting Instantiate";
        }
        else
        {
            m_LeftTitleText.text = "With Unique ID : '" + (!string.IsNullOrWhiteSpace(leftNetworkSquare.uniqueID) ? leftNetworkSquare.uniqueID : "<BLANK UNIQUE ID>") + "'";
            m_LeftInstantiateButton.interactable = false;
            m_LeftDestroyButton.interactable = true;
            m_LeftSpawnButton.interactable = true;

            if(leftNetworkSquare.isNetworkSpawned)
            {
                m_LeftSpawnButton.GetComponentInChildren<TextMeshProUGUI>().text = "Unspawn On Network";
                if(leftNetworkSquare.isNetworkReady)
                {
                    m_LeftStatusText.text = "Connected across network | Network ID: " + leftNetworkSquare.networkID;
                }
                else
                {
                    m_LeftStatusText.text = "Spawned but not connected";
                    if(m_NetworkManager.isServer)
                    {
                        m_LeftStatusText.text += " | Network ID: " + leftNetworkSquare.networkID;
                    }
                }
            }
            else
            {
                m_LeftSpawnButton.GetComponentInChildren<TextMeshProUGUI>().text = "Spawn On Network";
                m_LeftStatusText.text = "Not spawned";
            }

            //Update position and size
            leftNetworkSquare.transform.localPosition = m_LeftObjectPositioner.rect.center;
            float targetSize = Mathf.Min(m_LeftObjectPositioner.rect.width, m_LeftObjectPositioner.rect.height);
            //Pad
            targetSize = 0.8f * targetSize;
            //Make it an odd shape for visual reference and apply
            ((RectTransform)leftNetworkSquare.transform).sizeDelta = new Vector2(targetSize, targetSize * 0.7f);

            //Update Unique ID
            if(!leftNetworkSquare.isNetworkSpawned)
                leftNetworkSquare.uniqueID = uniqueID;
        }
    }

    private void UpdateRightWindow()
    {

    }

    private void OnLeftInstantiateButtonPressed()
    {
        //Create left network behaviour
        leftNetworkSquare = new GameObject("Network Square With Unique ID").AddComponent<NetworkUISquare>();
        leftNetworkSquare.uniqueID = uniqueID;
        leftNetworkSquare.transform.SetParent(m_LeftObjectPositioner);
        leftNetworkSquare.transform.localPosition = m_LeftObjectPositioner.rect.center;

        leftNetworkSquare.spawnOnNetworkInit = m_LeftSpawnInitToggle.isOn;

        if(m_NetworkManager.isServer || leftNetworkSquare.isOwner)
        {
            leftNetworkSquare.destroyOnUnspawn = m_LeftDestroyToggle.isOn;
        }
        else
        {
            m_LeftDestroyToggle.isOn = leftNetworkSquare.destroyOnUnspawn;
        }
        if(!m_NetworkManager.isRunning || m_NetworkManager.isServer)
        {
            leftNetworkSquare.ownerCanUnspawn = m_LeftOwnerUnspawnToggle.isOn;
        }
        else
        {
            m_LeftOwnerUnspawnToggle.SetIsOnWithoutNotify(leftNetworkSquare.ownerCanUnspawn);
        }
    }

    private void OnLeftDestroyButtonPressed()
    {
        Destroy(leftNetworkSquare.gameObject);
    }

    private void OnLeftSpawnButtonPressed()
    {
        if(leftNetworkSquare.isNetworkSpawned)
        {
            leftNetworkSquare.UnspawnOnNetwork();
        }
        else
        {
            leftNetworkSquare.SpawnOnNetwork();
        }
    }

    private void OnLeftSpawnInitToggleChanged(bool value)
    {
        if(leftNetworkSquare != null)
        {
            leftNetworkSquare.spawnOnNetworkInit = value;
        }
    }

    private void OnLeftDestroyUnspawnToggleChanged(bool value)
    {
        if(leftNetworkSquare != null)
        {
            leftNetworkSquare.destroyOnUnspawn = value;
        }
    }

    private void OnLeftOwnerUnspawnToggleChanged(bool value)
    {
        if(leftNetworkSquare != null)
        {
            try
            {
                leftNetworkSquare.ownerCanUnspawn = value;
            }
            catch(NotServerException ex)
            {
                m_LeftOwnerUnspawnToggle.SetIsOnWithoutNotify(leftNetworkSquare.ownerCanUnspawn);
                throw;
            }
        }
    }
}
