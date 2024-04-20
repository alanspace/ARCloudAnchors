using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuTest : MonoBehaviour
{

    [SerializeField]
    private GameObject placedPrefab = null;
    // Start is called before the first frame update
    void Start()
    {
        Instantiate(placedPrefab, new Vector3(0, 0, 0), new Quaternion());
        Instantiate(placedPrefab, new Vector3(1, 1, 1), new Quaternion());

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ChangeSceneTest()
    {

        //SceneManager.LoadScene("NewScene");
        SceneManager.LoadScene("ARCloudAnchor");

    }

}
