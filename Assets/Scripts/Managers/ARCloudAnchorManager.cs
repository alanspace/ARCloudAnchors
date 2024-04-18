using DilmerGames.Core.Singletons;
using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using Unity.Services.CloudSave;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Collections.Generic;
using static UnityEngine.Networking.UnityWebRequest;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.SceneManagement;


public class UnityEventResolver : UnityEvent<Transform>{}

public class ARCloudAnchorManager : Singleton<ARCloudAnchorManager>
{
    [SerializeField]
    private Camera arCamera = null;

    [SerializeField]
    private float resolveAnchorPassedTimeout = 10.0f;
    [SerializeField]
    private GameObject placedPrefab = null;

    private ARAnchorManager arAnchorManager = null;

    private ARAnchor pendingHostAnchor = null;

    private ARCloudAnchor cloudAnchor = null;

    private ARCloudAnchor outputCloudAnchor = null;

    private string anchorToResolve;

    private bool anchorUpdateInProgress = false;

    private bool anchorResolveInProgress = false;
    
    private float safeToResolvePassed = 0;

    private UnityEventResolver resolver = null;

    private HostCloudAnchorPromise HostCloudAnchorPromise = null;   

    private HostCloudAnchorResult hostCloudAnchorResult = null;

    private ResolveCloudAnchorPromise resolveCloudAnchorPromise = null; 

    private ResolveCloudAnchorResult resolveCloudAnchorResult = null;

    private string cloudAnchorId = null;

    private List<ARAnchor> aRAnchors = new List<ARAnchor>();

    private int anchorHostedCount = 0;

    private bool isListHosted = true;

    public bool isNewSceneResolved = false;

    private async void Awake() 
    {
        resolver = new UnityEventResolver();   
        resolver.AddListener((t) => ARPlacementManager.Instance.ReCreatePlacement(t));
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

    }

    public async void SaveData(string anchorId)
    {
        var playerData = new Dictionary<string, object>{
          {"firstKeyName", "a text value"},
          {"secondKeyName", anchorId}
        };
        await CloudSaveService.Instance.Data.ForceSaveAsync(playerData);
        Debug.Log($"Saved data {string.Join(',', playerData)}");
        ARDebugManager.Instance.LogInfo($"Saved data {string.Join(',', playerData)}");
    }

    private async Task<string> GetAnchorIDCloud()
    {
        var resultdata = await CloudSaveService.Instance.Data.LoadAllAsync();
        Debug.Log($"Saved data {string.Join(',', resultdata.Values)}");
        string anchorId = resultdata.TryGetValue("secondKeyName", out string result) ? result : "";
        return anchorId;
    }


    public void SaveARDrawAnchor()
    {
        arAnchorManager = GetComponent<ARAnchorManager>();
        LoadData();
    }

    public async void NewSceneResolve()
    {
        ARDebugManager.Instance.LogInfo($"Next scene");
        var anchorId = await GetAnchorIDCloud();
        ARDebugManager.Instance.LogInfo($"saved Cloud Anchor ID {anchorId}");

        //newscene
        //SceneManager.LoadScene("ARCloudAnchor");
        arAnchorManager = GetComponent<ARAnchorManager>();
        resolveCloudAnchorPromise = arAnchorManager.ResolveCloudAnchorAsync(anchorId);
        ARDebugManager.Instance.LogInfo($"Next scene can get  Cloud Anchor ID {resolveCloudAnchorPromise}");

        StartCoroutine(ResolvePromise(resolveCloudAnchorPromise));
        ARDebugManager.Instance.LogInfo("Resolved");

        isNewSceneResolved = true;

    }

    public async void LoadData()
    {
        ARDebugManager.Instance.LogInfo($"Resolve start ");

        var anchorId = await GetAnchorIDCloud();

        ARDebugManager.Instance.LogInfo($"Saved data {string.Join(',', anchorId)} ");
        if (anchorId != "")
        {
            ARDebugManager.Instance.LogInfo($"Can Get the AnchorID ");
            resolveCloudAnchorPromise = arAnchorManager.ResolveCloudAnchorAsync(anchorId);
            StartCoroutine(ResolvePromise(resolveCloudAnchorPromise));
            ARDebugManager.Instance.LogInfo("Resolved");



        }

    }


    private Pose GetCameraPose()
    {
        return new Pose(arCamera.transform.position,
            arCamera.transform.rotation);
    }

#region Anchor Cycle

    public void QueueAnchor(ARAnchor arAnchor)
    {
        pendingHostAnchor = arAnchor;
    }


    public void QueueAnchorList(List<ARAnchor> arAnchorList)
    {
        aRAnchors = arAnchorList;
    }


    private IEnumerator CheckHostCloudAnchorPromise(HostCloudAnchorPromise promise)
    {
       
        yield return promise;
        if (promise.State == PromiseState.Cancelled) yield break;
        hostCloudAnchorResult = promise.Result;
        /// Use the result of your promise here.

        cloudAnchorId = hostCloudAnchorResult.CloudAnchorId;
        ARDebugManager.Instance.LogInfo($"Cloud Anchor ID new {cloudAnchorId}");
        anchorUpdateInProgress = true;

    }

    private IEnumerator CheckListHostCloudAnchorPromise(HostCloudAnchorPromise promise)
    {

        yield return promise;
        if (promise.State == PromiseState.Cancelled) yield break;
        hostCloudAnchorResult = promise.Result;
        /// Use the result of your promise here.

        cloudAnchorId = hostCloudAnchorResult.CloudAnchorId;
        ARDebugManager.Instance.LogInfo($"Cloud Anchor ID new {cloudAnchorId}");
        isListHosted = true;
        anchorHostedCount++;
    }


    public void HostAnchor()
    {

        ARDebugManager.Instance.LogInfo($"HostAnchor executing");
        ARDebugManager.Instance.LogInfo($"Camera Pose {GetCameraPose()}");
        ARDebugManager.Instance.LogInfo($"Anchor transform position {pendingHostAnchor.transform.position}");

        FeatureMapQuality quality =
            arAnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());
        HostCloudAnchorPromise =  arAnchorManager.HostCloudAnchorAsync(pendingHostAnchor, 1);
        StartCoroutine(CheckHostCloudAnchorPromise(HostCloudAnchorPromise));


    }

    public void HostAnchorList()
    {
        ARDebugManager.Instance.LogInfo($"host a list of Anchors");
        foreach(ARAnchor anchor in aRAnchors)
        {
            if (isListHosted)
            {
                var promise = arAnchorManager.HostCloudAnchorAsync(anchor, 1);
                isListHosted = false;
                StartCoroutine(CheckListHostCloudAnchorPromise(promise));
            }

        }
        ARDebugManager.Instance.LogInfo($"number of hosted anchor {anchorHostedCount}");
        
    }


    private IEnumerator ResolvePromise(ResolveCloudAnchorPromise promise)
    {
        ARDebugManager.Instance.LogInfo($"new state {promise.State}");

        yield return promise;
        if (promise.State == PromiseState.Cancelled) yield break;
        resolveCloudAnchorResult = promise.Result;
        /// Use the result of your promise here.

        var resultAnchor = resolveCloudAnchorResult.Anchor;
        anchorResolveInProgress = true;
        ARDebugManager.Instance.LogInfo($"resultAnchor new {resultAnchor.transform.position}");
    }

    public async void Resolve()
    {
        ARDebugManager.Instance.LogInfo("save Cloud Anchor ID");

        ARDebugManager.Instance.LogInfo($"saved Cloud Anchor ID {cloudAnchorId}");



        SaveData(cloudAnchorId);

    }

    private void CheckHostingProgress()
    {
        CloudAnchorState cloudAnchorState = hostCloudAnchorResult.CloudAnchorState;
        if (cloudAnchorState == CloudAnchorState.Success)
        {
            ARDebugManager.Instance.LogError("Anchor successfully hosted");
            
            anchorUpdateInProgress = false;

            // keep track of cloud anchors added
            anchorToResolve = hostCloudAnchorResult.CloudAnchorId;
            ARDebugManager.Instance.LogError($"get the host cloud Anchor Result CloudAnchorID : {hostCloudAnchorResult.CloudAnchorId}");
        }
        else if(cloudAnchorState != CloudAnchorState.TaskInProgress)
        {
            ARDebugManager.Instance.LogError($"Fail to host anchor with state: {cloudAnchorState}");
            anchorUpdateInProgress = false;
        }
    }

    public void ChangeScene()
    {
        SceneManager.LoadScene("NewScene");

    }
    public void RemoveObject()
    {
        ARPlacementManager.Instance.RemovePlacements();
    }

    private void CheckResolveProgress()
    {
        CloudAnchorState cloudAnchorState = resolveCloudAnchorResult.CloudAnchorState;
        
        ARDebugManager.Instance.LogInfo($"ResolveCloudAnchor state {cloudAnchorState}");

        if (cloudAnchorState == CloudAnchorState.Success)
        {
            ARDebugManager.Instance.LogInfo($"CloudAnchorId: {resolveCloudAnchorResult.Anchor.transform.position} resolved");

            resolver.Invoke(resolveCloudAnchorResult.Anchor.transform);

            //ARPlacementManager.Instance.RemovePlacements();
            ARPlacementManager.Instance.ResetAnchor(resolveCloudAnchorResult.Anchor);

            anchorResolveInProgress = false;

            ARDebugManager.Instance.LogInfo($"CloudAnchorId: {resolveCloudAnchorResult.Anchor.transform.position} resolved");
        }
        else if (cloudAnchorState != CloudAnchorState.TaskInProgress)
        {
            ARDebugManager.Instance.LogError($"Fail to resolve Cloud Anchor with state: {cloudAnchorState}");

            anchorResolveInProgress = false;
        }
    }

    private void CheckResolveProgressAnother()
    {
        CloudAnchorState cloudAnchorState = resolveCloudAnchorResult.CloudAnchorState;

        ARDebugManager.Instance.LogInfo($"ResolveCloudAnchor state {cloudAnchorState}");

        if (cloudAnchorState == CloudAnchorState.Success)
        {
            ARDebugManager.Instance.LogInfo($"New Scene CloudAnchorId: {resolveCloudAnchorResult.Anchor.transform.position} resolved");
            anchorResolveInProgress = false;
            //resolver.Invoke(resolveCloudAnchorResult.Anchor.transform);
            ARDebugManager.Instance.LogInfo($"New Scene before reset anchor");

            //ARPlacementManager.Instance.ResetAnchor(resolveCloudAnchorResult.Anchor);
            Instantiate(placedPrefab, resolveCloudAnchorResult.Anchor.pose.position, resolveCloudAnchorResult.Anchor.pose.rotation);
            Instantiate(placedPrefab, new Vector3(0,0,0), resolveCloudAnchorResult.Anchor.transform.rotation);
            


            ARDebugManager.Instance.LogInfo($"New Scene Total resolved");
        }
        else if (cloudAnchorState != CloudAnchorState.TaskInProgress)
        {
            ARDebugManager.Instance.LogError($"Fail to resolve Cloud Anchor with state: {cloudAnchorState}");

            anchorResolveInProgress = false;
        }
    }

    #endregion

    void Update()
    {

        // check progress of new anchors created
        if (anchorUpdateInProgress)
        {
            CheckHostingProgress();
            return;
        }

        if(anchorResolveInProgress && safeToResolvePassed <= 0)
        {
            // check evey (resolveAnchorPassedTimeout)
            safeToResolvePassed = resolveAnchorPassedTimeout;

            if (!string.IsNullOrEmpty(anchorToResolve))
            {
                ARDebugManager.Instance.LogInfo($"Resolving AnchorId: {anchorToResolve}");
                CheckResolveProgress();


            }

        }
        else
        {
            safeToResolvePassed -= Time.deltaTime * 1.0f;
        }

        if (isNewSceneResolved && anchorResolveInProgress)
        {
            ARDebugManager.Instance.LogInfo($"new Scene update:");
            CheckResolveProgressAnother();
            isNewSceneResolved = false;
        }



    }


}
