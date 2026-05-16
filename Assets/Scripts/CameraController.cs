using UnityEngine;

public class CameraController : MonoBehaviour
{
    [System.Serializable]
    public class CameraView
    {
        public string name;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
    }
    [SerializeField] private Transform carTransform;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float cameraSmoothSpeed = 5f;
    [SerializeField] private CameraView[] cameraViews = new CameraView[]
    {
        new CameraView { name = "Chase", positionOffset = new Vector3(0, 2, -8), rotationOffset = new Vector3(10, 0, 0) },
        new CameraView { name = "FirstPerson", positionOffset = new Vector3(0, 1.5f, 0.5f), rotationOffset = new Vector3(0, 0, 0) },
        new CameraView { name = "TopDown", positionOffset = new Vector3(0, 15, 0), rotationOffset = new Vector3(90, 0, 0) },
        new CameraView { name = "Side", positionOffset = new Vector3(-10, 3, 0), rotationOffset = new Vector3(0, 90, 0) }
    };

    private int currentViewIndex = 0;
    private Vector3 targetPosition;
    private Vector3 targetRotation;
    void Start()
    {
        if (carTransform == null)
            carTransform = GetComponent<CarController>().transform;
        if (mainCamera == null)
            mainCamera = Camera.main;

        UpdateCameraTarget();
    }
    void Update()
    {
        
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentViewIndex = (currentViewIndex + 1) % cameraViews.Length;
            UpdateCameraTarget();
            Debug.Log("Camera: " + cameraViews[currentViewIndex].name);
        }
        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            targetPosition,
            Time.deltaTime * cameraSmoothSpeed
        );

        Quaternion targetQuat = Quaternion.Euler(targetRotation);
        mainCamera.transform.rotation = Quaternion.Lerp(
            mainCamera.transform.rotation,
            targetQuat,
            Time.deltaTime * cameraSmoothSpeed
        );
    }

    void LateUpdate()
    {
        
        CameraView view = cameraViews[currentViewIndex];
        targetPosition = carTransform.position + carTransform.TransformDirection(view.positionOffset);
        if (currentViewIndex == 1) 
        {
            mainCamera.transform.LookAt(carTransform.position + carTransform.forward * 5);
        }
        else
        {
            targetRotation = view.rotationOffset;
            if (currentViewIndex == 0 || currentViewIndex == 3) 
            {
                mainCamera.transform.LookAt(carTransform.position + carTransform.forward * 3 + Vector3.up);
            }
        }
    }
    void UpdateCameraTarget()
    {
        CameraView view = cameraViews[currentViewIndex];
        targetPosition = carTransform.position + carTransform.TransformDirection(view.positionOffset);
        targetRotation = view.rotationOffset;
    }
}

