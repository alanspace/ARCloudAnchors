using System.Collections.Generic;
using DilmerGames.Core.Singletons;
using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


[RequireComponent(typeof(ARRaycastManager))]
public class ARPlacementManager : Singleton<ARPlacementManager>
{ 
    [SerializeField]
    private Camera arCamera;

    [SerializeField]
    private GameObject placedPrefab = null;

    private GameObject placedGameObject = null;

    private ARRaycastManager arRaycastManager = null;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private ARAnchorManager arAnchorManager = null;

    void Awake() 
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        arAnchorManager = GetComponent<ARAnchorManager>();
    }

    bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        if(Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);

            if(touch.phase == TouchPhase.Began)
            {
                touchPosition = touch.position;

                bool isOverUI = touchPosition.IsPointOverUIObject();

                return isOverUI ? false : true;
            }
        }

        touchPosition = default;

        return false;
    }

    public void RemovePlacements()
    {
        Destroy(placedGameObject);
        placedGameObject = null;
    }

    void Update()
    {
        if(!TryGetTouchPosition(out Vector2 touchPosition))
            return;

        if(placedGameObject != null)
            return;

        if(arRaycastManager.Raycast(touchPosition, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon))
        {
            var hitPose = hits[0].pose;
            ARDebugManager.Instance.LogInfo($"Hit Pose {hitPose}");
            placedGameObject = Instantiate(placedPrefab, hitPose.position, hitPose.rotation);
            ARDebugManager.Instance.LogInfo($"HostAnchor executing");
            var anchor = arAnchorManager.AddAnchor(new Pose(hitPose.position, hitPose.rotation));
            placedGameObject.transform.parent = anchor.transform;
            ARDebugManager.Instance.LogInfo($"anchor Pose {anchor.transform.position}");

            // this won't host the anchor just add a reference to be later host it
            ARCloudAnchorManager.Instance.QueueAnchor(anchor);
        }
    }

    public void ReCreatePlacement(Transform transform)
    {
        placedGameObject = Instantiate(placedPrefab, transform.position, transform.rotation);
        placedGameObject.transform.parent = transform;
    }

    public void ResetAnchor(ARCloudAnchor anchorCloudObject)
    {
        Pose pose = anchorCloudObject.pose;
        ARDebugManager.Instance.LogInfo($"Get Back Position {pose.position}");
        Instantiate(placedPrefab, pose.position + new Vector3(0, 10, 50), pose.rotation);
    }
}