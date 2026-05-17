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
    [SerializeField] private float smoothTime    = 0.05f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float lookAhead     = 4.5f;
    [SerializeField] private float lookHeight    = 1.1f;
    [SerializeField] private float baseFOV       = 60f;
    [SerializeField] private float fovSpeedBoost = 8f;

    [SerializeField] private CameraView[] views =
    {
        new CameraView { name = "Chase",       positionOffset = new Vector3(0f, 1.8f, -5.5f) },
        new CameraView { name = "FirstPerson", positionOffset = new Vector3(0f, 1.5f,  0.5f) },
        new CameraView { name = "Side",        positionOffset = new Vector3(-10f, 3f,  0f),  rotationOffset = new Vector3(0f, 90f, 0f) },
        new CameraView { name = "TopDown",     positionOffset = new Vector3(0f, 15f,   0f),  rotationOffset = new Vector3(90f, 0f, 0f) },
    };

    private CarController car;
    private int viewIndex;
    private Vector3 camVelocity;
    private float fovVelocity;

    private void Awake()
    {
        if (!mainCamera) mainCamera = Camera.main;

        car = carTransform
            ? carTransform.GetComponentInParent<CarController>()
            : FindFirstObjectByType<CarController>();

        if (car) carTransform = car.transform;
        if (mainCamera) baseFOV = mainCamera.fieldOfView;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            viewIndex = (viewIndex + 1) % views.Length;
            Debug.Log("Camera: " + views[viewIndex].name);
        }
    }

    private void LateUpdate()
    {
        if (!mainCamera || !carTransform || views.Length == 0) return;

        CameraView view   = views[viewIndex];
        Transform  camTfm = mainCamera.transform;
        bool       isFirst = view.name.ToLower().Contains("first");

        Vector3    targetPos = carTransform.TransformPoint(view.positionOffset);
        Quaternion targetRot = GetTargetRotation(view, targetPos);

        camTfm.position = Vector3.SmoothDamp(
            camTfm.position, targetPos, ref camVelocity,
            isFirst ? smoothTime * 0.5f : smoothTime);

        camTfm.rotation = Quaternion.Slerp(
            camTfm.rotation, targetRot,
            1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));

        float speedRatio = car ? Mathf.Clamp01(car.SpeedKph / car.maxSpeed) : 0f;
        mainCamera.fieldOfView = Mathf.SmoothDamp(
            mainCamera.fieldOfView, baseFOV + fovSpeedBoost * speedRatio,
            ref fovVelocity, 0.18f);
    }
    private Quaternion GetTargetRotation(CameraView view, Vector3 camPos)
    {
        if (view.name.ToLower().Contains("first"))
            return carTransform.rotation * Quaternion.Euler(view.rotationOffset);

        bool    isTop  = view.name.ToLower().Contains("top");
        Vector3 target = isTop
            ? carTransform.position
            : carTransform.position + carTransform.forward * lookAhead + Vector3.up * lookHeight;

        Vector3 dir = target - camPos;
        if (dir.sqrMagnitude < 0.01f) dir = carTransform.forward;

        return Quaternion.LookRotation(dir.normalized, isTop ? carTransform.forward : Vector3.up);
    }
    private void OnValidate()
    {
        smoothTime    = Mathf.Max(0.001f, smoothTime);
        rotationSpeed = Mathf.Max(0.1f,   rotationSpeed);
        lookAhead     = Mathf.Max(0f,     lookAhead);
        fovSpeedBoost = Mathf.Max(0f,     fovSpeedBoost);
    }
}