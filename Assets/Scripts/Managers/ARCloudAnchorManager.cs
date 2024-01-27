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


public class UnityEventResolver : UnityEvent<Transform>{}

public class ARCloudAnchorManager : Singleton<ARCloudAnchorManager>
{
    [SerializeField]
    private Camera arCamera = null;

    [SerializeField]
    private float resolveAnchorPassedTimeout = 10.0f;

    private ARAnchorManager arAnchorManager = null;

    private ARAnchor pendingHostAnchor = null;

    private ARCloudAnchor cloudAnchor = null;

    private string anchorToResolve;

    private bool anchorUpdateInProgress = false;

    private bool anchorResolveInProgress = false;
    
    private float safeToResolvePassed = 0;

    private UnityEventResolver resolver = null;

    private HostCloudAnchorPromise HostCloudAnchorPromise = null;   

    private string cloudAnchorId = null;

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

    public async void LoadData()
    {

        var resultdata = await CloudSaveService.Instance.Data.LoadAllAsync();
        Debug.Log($"Saved data {string.Join(',', resultdata.Values)}");
        string anchorId = resultdata.TryGetValue("secondKeyName", out string result) ? result : "";

        ARDebugManager.Instance.LogInfo($"Saved data {string.Join(',', anchorId)} ");
        if (anchorId != "")
        {
            ARDebugManager.Instance.LogInfo($"Can Get the AnchorID ");
            ARCloudAnchor resultAnchor = arAnchorManager.ResolveCloudAnchorId(anchorId);
            ARPlacementManager.Instance.RemovePlacements();
            ARPlacementManager.Instance.ResetAnchor(resultAnchor);
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

    private IEnumerator CheckRooftopPromise(HostCloudAnchorPromise promise)
    {
        yield return promise;
        if (promise.State == PromiseState.Cancelled) yield break;
        var result = promise.Result;
        /// Use the result of your promise here.

        cloudAnchorId = result.CloudAnchorId;
        ARDebugManager.Instance.LogInfo($"Cloud Anchor ID new {cloudAnchorId}");
    }



    public void HostAnchor()
    {

        ARDebugManager.Instance.LogInfo($"HostAnchor executing");
        ARDebugManager.Instance.LogInfo($"Camera Pose {GetCameraPose()}");
        FeatureMapQuality quality =
            arAnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());
        HostCloudAnchorPromise =  arAnchorManager.HostCloudAnchorAsync(pendingHostAnchor, 1);
        StartCoroutine(CheckRooftopPromise(HostCloudAnchorPromise));


        cloudAnchorId = HostCloudAnchorPromise.Result.CloudAnchorId;
        ARDebugManager.Instance.LogInfo($"Cloud Anchor ID {cloudAnchorId}");

        cloudAnchor = arAnchorManager.HostCloudAnchor(pendingHostAnchor, 1);
        ARDebugManager.Instance.LogInfo($"cloud anchor pose {cloudAnchor.pose}");
        if (cloudAnchor == null)
        {
            ARDebugManager.Instance.LogError("Unable to host cloud anchor");
        }
        else
        {
            ARDebugManager.Instance.LogError($"open the anchor {cloudAnchor.gameObject.transform.position}");
            anchorUpdateInProgress = true;
        }
    }


    private IEnumerator ResolvePromise(ResolveCloudAnchorPromise promise)
    {
        yield return promise;
        if (promise.State == PromiseState.Cancelled) yield break;
        var result = promise.Result;
        /// Use the result of your promise here.

        var resultAnchor = result.Anchor;
        ARDebugManager.Instance.LogInfo($"resultAnchor new {resultAnchor.transform.position}");
    }

    public async void Resolve()
    {
        ARDebugManager.Instance.LogInfo("Resolve executing");

        //cloudAnchor = arAnchorManager.ResolveCloudAnchorId(anchorToResolve);

        //var result = arAnchorManager.ResolveCloudAnchorAsync(cloudAnchorId);
        var resultAnchorPromise = arAnchorManager.ResolveCloudAnchorAsync(cloudAnchorId);
        StartCoroutine(ResolvePromise(resultAnchorPromise));
        ARDebugManager.Instance.LogInfo("result");


        //var ttt = result.Result.Anchor.transform.position;
        //ARDebugManager.Instance.LogInfo($"ResolveCloudAnchor  TTdfsdf {ttt}");
        //ARDebugManager.Instance.LogInfo($"ResolveCloudAnchor  TT STATE {result.State}");
        //ARDebugManager.Instance.LogInfo($"here I am");
        //if (result.State == PromiseState.Done)
        //{
        //    ARDebugManager.Instance.LogInfo($"now is done");

        //    var ff = result.Result.Anchor;
        //    ARDebugManager.Instance.LogInfo($"ResolveCloudAnchorAsync dfdf {ff.transform.position}");
        //}
        //ARDebugManager.Instance.LogInfo($"moving on");




        SaveData(anchorToResolve);

        if (cloudAnchor == null)
        {
            ARDebugManager.Instance.LogError($"Failed to resolve cloud achor id {cloudAnchor.cloudAnchorId}");
        }
        else
        {
            ARDebugManager.Instance.LogError($"Success cloudAnchor {cloudAnchor.gameObject}");
            cloudAnchor.gameObject.SetActive(true);
            anchorResolveInProgress = true;
            ARDebugManager.Instance.LogError($"resolve open the cloudanchor {cloudAnchor.gameObject.transform.position}");
        }
    }

    private void CheckHostingProgress()
    {
        CloudAnchorState cloudAnchorState = cloudAnchor.cloudAnchorState;
        if (cloudAnchorState == CloudAnchorState.Success)
        {
            ARDebugManager.Instance.LogError("Anchor successfully hosted");
            
            anchorUpdateInProgress = false;

            // keep track of cloud anchors added
            anchorToResolve = cloudAnchor.cloudAnchorId;
        }
        else if(cloudAnchorState != CloudAnchorState.TaskInProgress)
        {
            ARDebugManager.Instance.LogError($"Fail to host anchor with state: {cloudAnchorState}");
            anchorUpdateInProgress = false;
        }
    }

    private void CheckResolveProgress()
    {
        CloudAnchorState cloudAnchorState = cloudAnchor.cloudAnchorState;
        
        ARDebugManager.Instance.LogInfo($"ResolveCloudAnchor state {cloudAnchorState}");

        if (cloudAnchorState == CloudAnchorState.Success)
        {
            ARDebugManager.Instance.LogInfo($"CloudAnchorId: {cloudAnchor.cloudAnchorId} resolved");

            resolver.Invoke(cloudAnchor.transform);

            anchorResolveInProgress = false;
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
        if(anchorUpdateInProgress)
        {
            CheckHostingProgress();
            return;
        }

        if(anchorResolveInProgress && safeToResolvePassed <= 0)
        {
            // check evey (resolveAnchorPassedTimeout)
            safeToResolvePassed = resolveAnchorPassedTimeout;

            if(!string.IsNullOrEmpty(anchorToResolve))
            {
                ARDebugManager.Instance.LogInfo($"Resolving AnchorId: {anchorToResolve}");
                CheckResolveProgress();


            }
        }
        else
        {
            safeToResolvePassed -= Time.deltaTime * 1.0f;
        }
        

    }
}
