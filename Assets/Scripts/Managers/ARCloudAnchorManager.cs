using DilmerGames.Core.Singletons;
using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using Unity.Services.CloudSave;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Collections.Generic;

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
        ARDebugManager.Instance.LogInfo($"Saved data {string.Join(',', resultdata)}");
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

    public async void HostAnchor()
    {

        ARDebugManager.Instance.LogInfo($"HostAnchor executing");
        ARDebugManager.Instance.LogInfo($"cloud anchor {GetCameraPose()}");
        FeatureMapQuality quality =
            arAnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());

        
        cloudAnchor = arAnchorManager.HostCloudAnchor(pendingHostAnchor, 1);
    
        if(cloudAnchor == null)
        {
            ARDebugManager.Instance.LogError("Unable to host cloud anchor");
        }
        else
        {
            ARDebugManager.Instance.LogError($"open the anchor {cloudAnchor}");
            anchorUpdateInProgress = true;
        }
    }
    
    public void Resolve()
    {
        ARDebugManager.Instance.LogInfo("Resolve executing");

        cloudAnchor = arAnchorManager.ResolveCloudAnchorId(anchorToResolve);

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
