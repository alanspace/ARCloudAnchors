using System.Collections.Generic;
using DilmerGames.Core.Singletons;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARAnchorManager))]
public class ARDrawManager : Singleton<ARDrawManager>
{
    [SerializeField]
    private LineSettings lineSettings = null;
    
    [SerializeField]
    private UnityEvent OnDraw = null;

    [SerializeField]
    private ARAnchorManager anchorManager = null;

    [SerializeField] 
    private Camera arCamera = null;

    private List<ARAnchor> anchors = new List<ARAnchor>();

    private Dictionary<int, ARLine> Lines = new Dictionary<int, ARLine>();

    private List<Vector3> savePositions = new List<Vector3>();

    private List<int> fingerIdList = new List<int>();

    private bool CanDraw { get; set; }

    void Update ()
    {
        // Pose pose = new Pose(PlanetObject.transform.position, Quaternion.identity);
        // ARAnchor newAnchor = anchorManager.AddAnchor(pose);
        // var cloudAnchor = anchorManager.HostCloudAnchorAsync(newAnchor,10);

        CanDraw = true;
        #if !UNITY_EDITOR
        DrawOnTouch();
#else
        DrawOnMouse();
        #endif
	}

    public void AllowDraw(bool isAllow)
    {
        CanDraw = isAllow;
    }


    public void TestDraw()
    {
        //foreach (KeyValuePair<ARAnchor, Vector3> item in saveInfo) {
        //    ARLine line = new ARLine(lineSettings);
        //    line.AddNewLineRenderer(transform, item.Key, item.Value);
        //    ARDebugManager.Instance.LogInfo($"Draw");
        //}
        ARAnchor anchor = anchorManager.AddAnchor(new Pose(savePositions[0], Quaternion.identity));
        ARLine line = new ARLine(lineSettings);
        line.AddNewLineRenderer(transform, anchor, savePositions[0]);
        for (int i = 0; i < savePositions.Count; i++)
        {
            line.AddPoint(savePositions[i]);

        }   

    }


    void DrawOnTouch()
    {
        if(!CanDraw) return;

        int tapCount = Input.touchCount > 1 && lineSettings.allowMultiTouch ? Input.touchCount : 1;
        for (int i = 0; i < tapCount; i++)
        {

            Touch touch = Input.GetTouch(i);
            Vector3 touchPosition = arCamera.ScreenToWorldPoint(new Vector3(Input.GetTouch(i).position.x, Input.GetTouch(i).position.y, lineSettings.distanceFromCamera));
            savePositions.Add(touchPosition);
            if (touch.phase == TouchPhase.Began)
            {
                OnDraw?.Invoke();
                ARAnchor anchor = anchorManager.AddAnchor(new Pose(touchPosition, Quaternion.identity));
                
                if (anchor == null) 
                    Debug.LogError("Error creating reference point");
                else 
                {
                    anchors.Add(anchor);
                    ARDebugManager.Instance.LogInfo($"Anchor created & total of {anchors.Count} anchor(s)");
                }

                fingerIdList.Add(touch.fingerId);

                //ARLine line = new ARLine(lineSettings);
                //Lines.Add(touch.fingerId, line);
                //line.AddNewLineRenderer(transform, anchor, touchPosition);
                //ARDebugManager.Instance.LogInfo($"Draw");

            }
            else if(touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                Lines[touch.fingerId].AddPoint(touchPosition);
            }
            else if(touch.phase == TouchPhase.Ended)
            {
                Lines.Remove(touch.fingerId);
            }
        }
        //queue to the ARAnchor List after the drawing is done
        ARCloudAnchorManager.Instance.QueueAnchorList(anchors);


    }

    void DrawOnMouse()
    {
        if(!CanDraw) return;

        Vector3 mousePosition = arCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, lineSettings.distanceFromCamera));

        if(Input.GetMouseButton(0))
        {
            OnDraw?.Invoke();

            if(Lines.Keys.Count == 0)
            {
                ARLine line = new ARLine(lineSettings);
                Lines.Add(0, line);
                line.AddNewLineRenderer(transform, null, mousePosition);
            }
            else 
            {
                Lines[0].AddPoint(mousePosition);
            }
        }
        else if(Input.GetMouseButtonUp(0))
        {
            Lines.Remove(0);   
        }
    }

    GameObject[] GetAllLinesInScene()
    {
        return GameObject.FindGameObjectsWithTag("Line");
    }

    public void ClearLines()
    {
        GameObject[] lines = GetAllLinesInScene();
        foreach (GameObject currentLine in lines)
        {
            LineRenderer line = currentLine.GetComponent<LineRenderer>();
            Destroy(currentLine);
        }
    }
}