using Unity.VisualScripting;
using UnityEngine;

public class ObjectManipulator : MonoBehaviour
{
    public GameObject ARObject;
    [SerializeField] private Camera ARCamera;
    private bool isARObjectSelected;
    private string tagARObjects = "Block";
    private Vector2 initialTouchPos;

    [SerializeField] private float speedMovement = 4.0f;
    [SerializeField] private float speedRotation = 4.0f;
    [SerializeField] private float scaleFactor = 4.0f;

    private float screenFactor = 0.0001f;

    private float touchDis;
    private Vector2 touchPositionDiff;

    private float rotationTolerance = 1.5f;
    private float scaleTolerance = 25f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touchOne = Input.GetTouch(0);

            if (Input.touchCount == 1)
            {

                if (touchOne.phase == TouchPhase.Began)
                {
                    initialTouchPos = touchOne.position;
                    isARObjectSelected = CheckTouchOnARObject(initialTouchPos);
                }

                if (touchOne.phase == TouchPhase.Moved && isARObjectSelected)
                {
                    Vector2 diffPos = (touchOne.position - initialTouchPos) * screenFactor;

                    ARObject.transform.position = ARObject.transform.position + 
                        new Vector3(diffPos.x * speedMovement, diffPos.y * speedMovement, 0);

                    initialTouchPos = touchOne.position;
                }
            }

            if (Input.touchCount == 2)
            {
                Touch touchTwo = Input.GetTouch(1);

                if (touchOne.phase == TouchPhase.Began || touchTwo.phase == TouchPhase.Began)
                {
                    touchPositionDiff = touchTwo.position - touchOne.position;
                    touchDis = Vector2.Distance(touchTwo.position, touchOne.position);
                }

                if (touchOne.phase == TouchPhase.Moved || touchTwo.phase == TouchPhase.Moved)
                {
                    Vector2 currentTouchPosDiff = touchTwo.position - touchOne.position;
                    float currentTouchDis = Vector2.Distance(touchTwo.position, touchOne.position);

                    float diffDis = currentTouchDis - touchDis;

                    if (Mathf.Abs(diffDis) > scaleTolerance)
                    {

                        Vector3 newScale = ARObject.transform.localScale + Mathf.Sign(diffDis) * Vector3.one * scaleFactor;
                        ARObject.transform.localScale = Vector3.Lerp(ARObject.transform.localScale, newScale, 0.05f);
                    }

                    float angle = Vector2.SignedAngle(touchPositionDiff, currentTouchPosDiff);

                    if (Mathf.Abs(angle) > rotationTolerance)
                    {
                        ARObject.transform.rotation = Quaternion.Euler(0, ARObject.transform.rotation.eulerAngles.y - Mathf.Sign(angle) * speedRotation, 0);
                    }

                    touchDis = currentTouchDis;
                    touchPositionDiff = currentTouchPosDiff;
                }
            }
        }
    }

    private bool CheckTouchOnARObject(Vector2 touchPosition)
    {
        Ray ray = ARCamera.ScreenPointToRay(touchPosition);

        if (Physics.Raycast(ray, out RaycastHit hitARObject))
        {
            if (hitARObject.collider.CompareTag(tagARObjects))
            {
                ARObject = hitARObject.transform.gameObject;
                return true;
            }
        }

        return false;
    }
}
