using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider FL;
    public WheelCollider FR;
    public WheelCollider RL;
    public WheelCollider RR;

    [Header("Wheel Meshes")]
    public Transform FLMesh;
    public Transform FRMesh;
    public Transform RLMesh;
    public Transform RRMesh;

    [Header("Power")]
    public float motorTorque = 5000000f;
    public float brakeTorque = 900000f;
    public float maxSpeedKmh = 2500f;
    public float frontWheelDriveSplit = 0.25f;

    [Header("Handling")]
    public float maxSteerAngle = 16f;
    public float steerSmoothness = 10f;
    public float steerSpeedReduction = 2.5f;
    public float reverseEnableThresholdKmh = 8f;

    [Header("Grip and Stability")]
    public float downforce = 12f;
    public float stabilityStrength = 0.12f;
    public float tractionControl = 0.1f;

    [Header("Vehicle Physics")]
    public float vehicleMass = 600f;
    private float centerOfMassY = -1.0f;
    private float linearDamping;
    private float angularDamping = 0.01f;
    private float maxAngularVelocity = 20f;

    private Rigidbody rb;
    private float currentSteerAngle;
    private float maxSpeedMs;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        maxSpeedMs = maxSpeedKmh / 3.6f;

        rb.mass = vehicleMass;
        rb.centerOfMass = new Vector3(0f, centerOfMassY, 0.1f);
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.maxAngularVelocity = maxAngularVelocity;
        SetupWheelFriction();
    }

    void SetupWheelFriction()
    {
        WheelFrictionCurve forwardFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.35f,
            extremumValue = 1.0f,
            asymptoteSlip = 0.9f,
            asymptoteValue = 0.55f,
            stiffness = 2.3f
        };

        WheelFrictionCurve sidewaysFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.2f,
            extremumValue = 1.0f,
            asymptoteSlip = 0.55f,
            asymptoteValue = 0.7f,
            stiffness = 2.8f
        };

        foreach (WheelCollider wheel in new[] { FL, FR, RL, RR })
        {
            wheel.forwardFriction = forwardFriction;
            wheel.sidewaysFriction = sidewaysFriction;
        }
    }

    void FixedUpdate()
    {
        float throttle = Input.GetAxis("Vertical");
        float steerInput = Input.GetAxis("Horizontal");
        bool braking = Input.GetKey(KeyCode.Space);

        HandleSteering(steerInput);
        HandleMotor(throttle, braking);
        HandleBraking(braking);
        ApplyDownforce();
        ApplyStabilityControl();
        UpdateAllWheelMeshes();
    }
    void HandleSteering(float steerInput)
    {
        float forwardSpeedKmh = Mathf.Abs(GetForwardSpeedKmh());
        float speedFactor = Mathf.Clamp01(forwardSpeedKmh / maxSpeedKmh);
        float steerLimit = Mathf.Lerp(maxSteerAngle, maxSteerAngle / steerSpeedReduction, speedFactor);

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            steerLimit * steerInput,
            Time.fixedDeltaTime * steerSmoothness
        );

        FL.steerAngle = currentSteerAngle;
        FR.steerAngle = currentSteerAngle;
    }

    void HandleMotor(float throttle, bool braking)
    {
        if (braking || throttle == 0f)
        {
            SetMotorTorque(0f);
            return;
        }

        float forwardSpeedKmh = GetForwardSpeedKmh();
        float forwardSpeedMs = Mathf.Abs(forwardSpeedKmh) / 3.6f;

        if (throttle < 0f)
        {
            // Only allow reverse when nearly stopped.
            if (forwardSpeedKmh > reverseEnableThresholdKmh)
            {
                SetMotorTorque(0f);
                return;
            }

            FL.motorTorque = 0f;
            FR.motorTorque = 0f;
            RL.motorTorque = motorTorque * throttle * 0.35f;
            RR.motorTorque = motorTorque * throttle * 0.35f;
            return;
        }

        if (forwardSpeedMs >= maxSpeedMs)
        {
            SetMotorTorque(0f);
            return;
        }

        float tcFactor = GetTractionControlFactor();
        float driveTorque = motorTorque * throttle * tcFactor;
        float frontTorque = driveTorque * frontWheelDriveSplit;
        float rearTorque = driveTorque * (1f - frontWheelDriveSplit);

        FL.motorTorque = frontTorque;
        FR.motorTorque = frontTorque;
        RL.motorTorque = rearTorque;
        RR.motorTorque = rearTorque;
    }

    void HandleBraking(bool braking)
    {
        if (braking)
        {
            FL.brakeTorque = brakeTorque * 0.7f;
            FR.brakeTorque = brakeTorque * 0.7f;
            RL.brakeTorque = brakeTorque * 0.3f;
            RR.brakeTorque = brakeTorque * 0.3f;
        }
        else
        {
            SetBrakeTorque(0f);
        }
    }

    float GetTractionControlFactor()
    {
        if (tractionControl <= 0f) return 1f;

        float wheelSpeedRl = RL.rpm * RL.radius * 2f * Mathf.PI / 60f;
        float wheelSpeedRr = RR.rpm * RR.radius * 2f * Mathf.PI / 60f;
        float avgWheelSpeed = (Mathf.Abs(wheelSpeedRl) + Mathf.Abs(wheelSpeedRr)) * 0.5f;
        float carSpeed = Mathf.Abs(GetForwardSpeedMs());
        float slip = avgWheelSpeed - carSpeed;

        if (slip > 1f)
        {
            float slipFactor = Mathf.Clamp01(1f - slip * tractionControl * 0.08f);
            return Mathf.Lerp(1f, slipFactor, tractionControl);
        }

        return 1f;
    }

    void ApplyDownforce()
    {
        float speed = Mathf.Abs(GetForwardSpeedMs());
        float downforceAmount = Mathf.Clamp(downforce * speed * speed, 0f, 250000f);
        rb.AddForce(-transform.up * downforceAmount);
    }

    float GetForwardSpeedKmh()
    {
        return Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
    }

    float GetForwardSpeedMs()
    {
        return Vector3.Dot(rb.linearVelocity, transform.forward);
    }

    void ApplyStabilityControl()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float sidewaysSpeed = localVelocity.x;
        Vector3 lateralCorrection = -transform.right * (sidewaysSpeed * stabilityStrength * rb.mass);
        rb.AddForce(lateralCorrection, ForceMode.Force);

        float yawDamping = -rb.angularVelocity.y * stabilityStrength * 0.5f;
        rb.AddTorque(transform.up * yawDamping, ForceMode.Force);
    }

    void UpdateAllWheelMeshes()
    {
        UpdateWheelMesh(FL, FLMesh);
        UpdateWheelMesh(FR, FRMesh);
        UpdateWheelMesh(RL, RLMesh);
        UpdateWheelMesh(RR, RRMesh);
    }

    void UpdateWheelMesh(WheelCollider wheelCollider, Transform mesh)
    {
        wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot * Quaternion.Euler(0f, 0f, 90f);
    }

    void SetMotorTorque(float torque)
    {
        FL.motorTorque = torque;
        FR.motorTorque = torque;
        RL.motorTorque = torque;
        RR.motorTorque = torque;
    }

    void SetBrakeTorque(float torque)
    {
        FL.brakeTorque = torque;
        FR.brakeTorque = torque;
        RL.brakeTorque = torque;
        RR.brakeTorque = torque;
    }
}