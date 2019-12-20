using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SceneTesterUI : MonoBehaviour
{
    void Awake()
    {

    }

    void Start()
    {
        transform.Find("Current Scene Text").GetComponent<TextMeshProUGUI>().text = SceneManager.GetActiveScene().name;
        transform.Find("Load Scene 1").GetComponent<Button>().onClick.AddListener(Button1Pressed);
        transform.Find("Load Scene 2").GetComponent<Button>().onClick.AddListener(Button2Pressed);
        transform.Find("Load Scene 3").GetComponent<Button>().onClick.AddListener(Button3Pressed);
    }

    void Button1Pressed()
    {
        SceneManager.LoadScene("Scene Test 1");
    }

    void Button2Pressed()
    {
        SceneManager.LoadScene("Scene Test 2");
    }

    void Button3Pressed()
    {
        SceneManager.LoadScene("Scene Test 3");
    }
}
