using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
     public WheelCollider frontRight; 
     public WheelCollider rearLeft;
     public WheelCollider rearRight;

    [Header("Wheel Meshes")]
     public Transform frontLeftMesh;
     public Transform frontRightMesh;
     public Transform rearLeftMesh;
     public Transform rearRightMesh;

    [Header("Wheel Visual Rotation Offset")]
    public Vector3 wheelRotationOffset = new Vector3(0f, 0f, 90f);

    [Header("Power")]
    public float motorTorque = 3600f;
    
    public float reverseTorque = 1600f;
    
    [Tooltip("Target top speed in km/h.")]
    public float maxSpeed = 400f;

    [Range(0.35f, 0.95f)]
    public float torqueTaperStart = 0.68f;

    [Range(0f, 0.75f)]
    public float launchSoftening = 0.3f;

    [Header("Braking")]
    public float brakeTorque = 9000f;

    [Range(0.5f, 0.8f)]
    public float brakeFrontBias = 0.64f;

    public float engineBraking = 450f;

    [Header("Transmission")]
    public float[] gearSpeedLimits = new float[] { 0f, 80f, 120f, 165f, 220f, 280f, 340f, 400f };
    public float[] gearTorqueMultipliers = new float[] { 0f, 1.75f, 1.45f, 1.2f, 1.05f, 0.9f, 0.8f, 0.7f };
    public float shiftUpRatio = 0.92f;
    public float shiftDownRatio = 0.42f;
    public float shiftDelay = 0.18f;
    public float reverseSpeedKph = 8f;
    public float handlingReferenceSpeed = 280f;
    [Header("Steering")]
    public float maxSteerAngle = 28f;
    public float steeringSmoothness = 9f;
    [Range(0.08f, 0.55f)]
    public float highSpeedSteerMultiplier = 0.23f;

    [Range(0f, 1f)]
    public float slipSteerReduction = 0.45f;
    [Header("Aerodynamics")]
    public float downforce = 700f;
    
    [Range(0.25f, 0.65f)]
    public float frontDownforceBias = 0.42f;

    public float maxDownforce = 12000f;

    [FormerlySerializedAs("dragCoeff")]
    public float aeroDrag = 0.08f;

    public bool enableDrs = true;
    public float drsActivationSpeed = 200f;
    public float drsDragReduction = 0.28f;
    public float drsDownforceReduction = 0.15f;

    [Header("Grip And Stability")]
    public float lateralStability = 2.7f;
    public float yawStability = 1.6f;
    public float yawDamping = 1.05f;
    public float pitchRollDamping = 2.2f;
    [Range(0f, 1f)]
    public float tractionControl = 0.65f;
    public float allowedDriveSlip = 0.32f;

    [Header("Anti-Roll")]
     public float frontAntiRoll = 3200f;
    public float rearAntiRoll = 3800f;
    [Header("Vehicle Physics")] 
    public float vehicleMass = 760f;

    public float centerOfMassYOffset = -0.6f;
    public float centerOfMassZOffset = 0.08f;
    public float linearDamping = 0.015f;
    public float angularDamping = 0.16f;
    public float maxAngularVelocity = 10f;

    [Header("Suspension")]
    public float suspensionSpring = 36000f;
    public float suspensionDamper = 7200f;
    public float suspensionDistance = 0.16f;
    public float forceAppPointDistance = 0.03f;
    public float wheelMass = 14f;
    public float wheelDampingRate = 2.2f;

    [Header("Tyres")]
    public float frontForwardGrip = 2.1f;
    public float rearForwardGrip = 2.25f;
    public float frontSideGrip = 2.65f;
    public float rearSideGrip = 2.85f;
    public float highSpeedGripBoost = 0.12f;

    private Rigidbody rb;
    private WheelCollider[] wheels;
    private Transform[] wheelMeshes;
    private float throttleInput;
    private float steerInput;
    private float brakeInput;
    private float currentSteerAngle;
    private float currentDownforce;
    private float lastGripBoost = -1f;
    private int groundedWheelCount;
    private int currentGear = 1;
    private float currentEngineRpm;
    private float lastShiftTime = -999f;
    private bool drsOpen;
    private bool ready;
    public float SpeedKph => rb == null ? 0f : rb.linearVelocity.magnitude * 3.6f;
    public float ForwardSpeedKph => rb == null ? 0f : Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
    public float DownforceNow => currentDownforce;
    public bool IsGrounded => groundedWheelCount > 0;
    public int CurrentGear => currentGear;
    public int MaxForwardGear => GetMaxForwardGear();
    public float EngineRpm => currentEngineRpm;
    public bool DrsOpen => drsOpen;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        maxSpeed = Mathf.Max(400f, maxSpeed);
        AutoAssignMissingReferences();
        CacheWheelArrays();
        EnsureTransmissionData();
        ready = ValidateSetup();
        if (!ready)
        {
            enabled = false;
            return;
        }

        ConfigureRigidbody();
        ConfigureWheels();
    }

    private void Update()
    {
        throttleInput = Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);
        steerInput = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
        brakeInput = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }
    private void FixedUpdate()
    {
        if (!ready) return;

        UpdateGrounding();
        UpdateDynamicGrip();
        ApplySteering();
        ApplyDriveAndBrakes();
        ApplyAerodynamics();
        ApplyAntiRoll(frontLeft, frontRight, frontAntiRoll);
        ApplyAntiRoll(rearLeft, rearRight, rearAntiRoll);
        ApplyStabilityAssists();
        SyncWheelMeshes();
    }

    private void ConfigureRigidbody()
    {
        rb.mass = vehicleMass;
        rb.centerOfMass = new Vector3(0f, centerOfMassYOffset, centerOfMassZOffset);
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.maxAngularVelocity = maxAngularVelocity;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.solverIterations = 16;
        rb.solverVelocityIterations = 12;
    }
    private void ConfigureWheels()
    {
        JointSpring spring = new JointSpring
        {
            spring = suspensionSpring,
            damper = suspensionDamper,
            targetPosition = 0.52f
        };
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            wheel.suspensionSpring = spring;
            wheel.suspensionDistance = suspensionDistance;
            wheel.forceAppPointDistance = forceAppPointDistance;
            wheel.mass = wheelMass;
            wheel.wheelDampingRate = wheelDampingRate;
            wheel.ConfigureVehicleSubsteps(8f, 12, 15);
        }

        UpdateDynamicGrip();
    }
    private void ApplySteering()
    {
        float speedRatio = Mathf.Clamp01(Mathf.Abs(ForwardSpeedKph) / handlingReferenceSpeed);
        float speedLimitedAngle = Mathf.Lerp(maxSteerAngle, maxSteerAngle * highSpeedSteerMultiplier, speedRatio * speedRatio);

        float slipAngle = Mathf.Abs(GetSlipAngle());
        float slipCut = Mathf.Lerp(1f, Mathf.Clamp01(1f - slipAngle / 40f), slipSteerReduction * speedRatio);
        float targetAngle = steerInput * speedLimitedAngle * slipCut;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetAngle,
            1f - Mathf.Exp(-steeringSmoothness * Time.fixedDeltaTime));
        ApplyAckermann(currentSteerAngle);
    }

    private void ApplyAckermann(float steerAngle)
    {
        if (Mathf.Abs(steerAngle) < 0.01f)
        {
            frontLeft.steerAngle = 0f;
            frontRight.steerAngle = 0f;
            return;
        }

        float wheelbase = Mathf.Max(0.1f, Mathf.Abs(frontLeft.transform.localPosition.z - rearLeft.transform.localPosition.z));
        float track = Mathf.Max(0.1f, Mathf.Abs(frontRight.transform.localPosition.x - frontLeft.transform.localPosition.x));
        float steerRad = Mathf.Abs(steerAngle) * Mathf.Deg2Rad;
        float turnRadius = wheelbase / Mathf.Tan(steerRad);
        float inner = Mathf.Atan(wheelbase / Mathf.Max(0.1f, turnRadius - track * 0.5f)) * Mathf.Rad2Deg;
        float outer = Mathf.Atan(wheelbase / (turnRadius + track * 0.5f)) * Mathf.Rad2Deg;

        if (steerAngle > 0f)
        {
            frontLeft.steerAngle = outer;
            frontRight.steerAngle = inner;
        }
        else
        {
            frontLeft.steerAngle = -inner;
            frontRight.steerAngle = -outer;
        }
    }

    private void ApplyDriveAndBrakes()
    {
        float forwardKph = ForwardSpeedKph;
        float throttle = throttleInput;
        float brake = brakeInput;

        if (throttle < -0.05f && forwardKph > 4f)
        {
            brake = Mathf.Max(brake, -throttle);
            throttle = 0f;
        }
        else if (throttle > 0.05f && forwardKph < -4f)
        {
            brake = Mathf.Max(brake, throttle);
            throttle = 0f;
        }

        UpdateAutomaticGear(forwardKph, throttle, brake > 0.01f);

        SetFrontMotorTorque(0f);
        SetRearMotorTorque(0f);

        if (brake > 0.01f)
        {
            drsOpen = false;
            ApplyBrake(brake);
            return;
        }

        SetBrakeTorque(0f);

        if (Mathf.Abs(throttle) < 0.05f)
        {
            drsOpen = false;
            ApplyCoastBraking();
            return;
        }

        if (throttle > 0f)
        {
            float gearLimit = GetCurrentGearSpeedLimit();
            if (gearLimit <= 0f || Mathf.Abs(forwardKph) >= maxSpeed) return;

            float torque = motorTorque * throttle * GetGearTorqueMultiplier(currentGear) * GetTorqueTaper(forwardKph) * GetLaunchLimiter(forwardKph);
            rearLeft.motorTorque = torque * GetTractionLimiter(rearLeft);
            rearRight.motorTorque = torque * GetTractionLimiter(rearRight);
            drsOpen = ShouldOpenDrs(throttle);
            return;
        }

        drsOpen = false;
        if (Mathf.Abs(forwardKph) < 14f)
        {
            float reverse = reverseTorque * throttle;
            rearLeft.motorTorque = reverse;
            rearRight.motorTorque = reverse;
        }
    }

    private float GetTorqueTaper(float forwardKph)
    {
        float gearLimit = GetCurrentGearSpeedLimit();
        if (gearLimit <= 0f) return 1f;

        float speedRatio = Mathf.Clamp01(Mathf.Abs(forwardKph) / gearLimit);
        if (speedRatio <= torqueTaperStart) return 1f;

        float taper = Mathf.InverseLerp(1f, torqueTaperStart, speedRatio);
        return Mathf.SmoothStep(0.2f, 1f, taper);
    }

    private float GetLaunchLimiter(float forwardKph)
    {
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardKph) / 90f);
        return Mathf.Lerp(1f - launchSoftening, 1f, speedFactor);
    }

    private float GetTractionLimiter(WheelCollider wheel)
    {
        if (tractionControl <= 0f) return 1f;
        if (!wheel.GetGroundHit(out WheelHit hit)) return 0.2f;

        float slip = Mathf.Abs(hit.forwardSlip);
        if (slip <= allowedDriveSlip) return 1f;

        float excessSlip = slip - allowedDriveSlip;
        return Mathf.Clamp01(1f - excessSlip * tractionControl * 2.2f);
    }

    private void ApplyBrake(float brake)
    {
        float total = brakeTorque * Mathf.Clamp01(brake);
        float front = total * brakeFrontBias;
        float rear = total * (1f - brakeFrontBias);

        frontLeft.brakeTorque = front;
        frontRight.brakeTorque = front;
        rearLeft.brakeTorque = rear;
        rearRight.brakeTorque = rear;
    }

    private void ApplyCoastBraking()
    {
        if (Mathf.Abs(ForwardSpeedKph) < 2f)
        {
            SetBrakeTorque(0f);
            return;
        }

        float coast = engineBraking;
        rearLeft.brakeTorque = coast;
        rearRight.brakeTorque = coast;
        frontLeft.brakeTorque = coast * 0.35f;
        frontRight.brakeTorque = coast * 0.35f;
    }

    private void ApplyAerodynamics()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        float speed100 = speed * 3.6f / 100f;
        bool drsActive = enableDrs && drsOpen && throttleInput > 0.35f && brakeInput <= 0f && Mathf.Abs(steerInput) < 0.15f;
        float downforceScale = drsActive ? 1f - drsDownforceReduction : 1f;
        float dragScale = drsActive ? 1f - drsDragReduction : 1f;
        currentDownforce = Mathf.Clamp(downforce * speed100 * speed100 * downforceScale, 0f, maxDownforce);

        if (currentDownforce > 0f)
        {
            Vector3 down = -transform.up;
            float wheelbaseHalf = GetWheelbase() * 0.5f;
            Vector3 frontPosition = transform.position + transform.forward * wheelbaseHalf;
            Vector3 rearPosition = transform.position - transform.forward * wheelbaseHalf;

            rb.AddForceAtPosition(down * currentDownforce * frontDownforceBias, frontPosition, ForceMode.Force);
            rb.AddForceAtPosition(down * currentDownforce * (1f - frontDownforceBias), rearPosition, ForceMode.Force);
        }

        if (speed > 0.1f && aeroDrag > 0f)
        {
            rb.AddForce(-velocity.normalized * aeroDrag * speed * speed * dragScale, ForceMode.Force);
        }
    }

    private void ApplyAntiRoll(WheelCollider left, WheelCollider right, float stiffness)
    {
        if (stiffness <= 0f) return;

        bool groundedLeft = left.GetGroundHit(out WheelHit hitLeft);
        bool groundedRight = right.GetGroundHit(out WheelHit hitRight);

        float travelLeft = groundedLeft ? GetSuspensionTravel(left, hitLeft) : 1f;
        float travelRight = groundedRight ? GetSuspensionTravel(right, hitRight) : 1f;
        float speedKph = Mathf.Abs(ForwardSpeedKph);
        float lowSpeedBlend = Mathf.InverseLerp(2f, 18f, speedKph);
        float antiRollForce = (travelLeft - travelRight) * stiffness * lowSpeedBlend;

        if (groundedLeft)
        {
            rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position, ForceMode.Force);
        }

        if (groundedRight)
        {
            rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position, ForceMode.Force);
        }
    }

    private float GetSuspensionTravel(WheelCollider wheel, WheelHit hit)
    {
        float distance = Mathf.Max(0.01f, wheel.suspensionDistance);
        return (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / distance;
    }

    private void ApplyStabilityAssists()
    {
        if (groundedWheelCount == 0) return;

        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.5f) return;

        float groundedRatio = groundedWheelCount / 4f;
        float speedAssist = Mathf.Clamp01(speed / 22f) * groundedRatio;
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        Vector3 lateralAccel = -transform.right * localVelocity.x * lateralStability * speedAssist;
        rb.AddForce(lateralAccel, ForceMode.Acceleration);

        Vector3 localAngular = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 pitchRollDamp = new Vector3(-localAngular.x * pitchRollDamping, 0f, -localAngular.z * pitchRollDamping);
        rb.AddTorque(transform.TransformDirection(pitchRollDamp), ForceMode.Acceleration);

        float yawDamp = -localAngular.y * yawDamping * speedAssist;
        rb.AddTorque(transform.up * yawDamp, ForceMode.Acceleration);

        float slipAngle = Mathf.Clamp(GetSlipAngle(), -18f, 18f);
        float steerAllowance = Mathf.Lerp(1f, 0.55f, Mathf.Abs(steerInput));
        rb.AddTorque(transform.up * slipAngle * yawStability * speedAssist * steerAllowance, ForceMode.Acceleration);
    }

    private void UpdateDynamicGrip()
    {
        if (wheels == null || wheels.Length == 0) return;

        float speedRatio = rb == null ? 0f : Mathf.Clamp01(Mathf.Abs(ForwardSpeedKph) / handlingReferenceSpeed);
        float boost = 1f + highSpeedGripBoost * speedRatio;
        if (Mathf.Abs(boost - lastGripBoost) < 0.02f) return;

        lastGripBoost = boost;

        SetWheelFriction(frontLeft, frontForwardGrip * boost, frontSideGrip * boost);
        SetWheelFriction(frontRight, frontForwardGrip * boost, frontSideGrip * boost);
        SetWheelFriction(rearLeft, rearForwardGrip * boost, rearSideGrip * boost);
        SetWheelFriction(rearRight, rearForwardGrip * boost, rearSideGrip * boost);
    }

    private void UpdateAutomaticGear(float forwardKph, float throttle, bool braking)
    {
        float speed = Mathf.Abs(forwardKph);
        if (Time.time - lastShiftTime < shiftDelay)
        {
            currentEngineRpm = GetEngineRpm(speed);
            return;
        }

        int maxGear = GetMaxForwardGear();

        if (throttle > 0.25f && currentGear < maxGear)
        {
            float gearLimit = GetCurrentGearSpeedLimit();
            if (gearLimit > 0f && speed >= gearLimit * shiftUpRatio)
            {
                ShiftGear(currentGear + 1);
            }
        }
        else if ((braking || throttle < 0.12f) && currentGear > 1)
        {
            float prevLimit = GetGearSpeedLimit(currentGear - 1);
            if (prevLimit > 0f && speed <= prevLimit * shiftDownRatio)
            {
                ShiftGear(currentGear - 1);
            }
        }

        currentEngineRpm = GetEngineRpm(speed);
    }

    private void ShiftGear(int targetGear)
    {
        currentGear = Mathf.Clamp(targetGear, 1, GetMaxForwardGear());
        lastShiftTime = Time.time;
    }

    private float GetEngineRpm(float speedKph)
    {
        float gearLimit = Mathf.Max(1f, GetCurrentGearSpeedLimit());
        float ratio = Mathf.Clamp01(speedKph / gearLimit);
        return Mathf.Lerp(1200f, 12000f, ratio);
    }

    private bool ShouldOpenDrs(float throttle)
    {
        if (!enableDrs) return false;
        if (brakeInput > 0f) return false;
        if (throttle < 0.5f) return false;
        if (Mathf.Abs(steerInput) > 0.15f) return false;
        if (ForwardSpeedKph < drsActivationSpeed) return false;
        return currentGear >= Mathf.Max(5, GetMaxForwardGear() - 1);
    }

    private int GetMaxForwardGear()
    {
        int speedCount = gearSpeedLimits == null ? 0 : gearSpeedLimits.Length;
        int torqueCount = gearTorqueMultipliers == null ? 0 : gearTorqueMultipliers.Length;
        int count = Mathf.Min(speedCount, torqueCount);
        return Mathf.Max(1, count - 1);
    }

    private float GetCurrentGearSpeedLimit()
    {
        return GetGearSpeedLimit(currentGear);
    }

    private float GetGearSpeedLimit(int gear)
    {
        if (gearSpeedLimits == null || gearSpeedLimits.Length == 0) return maxSpeed;
        int index = Mathf.Clamp(gear, 0, gearSpeedLimits.Length - 1);
        return gearSpeedLimits[index];
    }

    private float GetGearTorqueMultiplier(int gear)
    {
        if (gearTorqueMultipliers == null || gearTorqueMultipliers.Length == 0) return 1f;
        int index = Mathf.Clamp(gear, 0, gearTorqueMultipliers.Length - 1);
        return gearTorqueMultipliers[index];
    }

    private void EnsureTransmissionData()
    {
        if (gearSpeedLimits == null || gearSpeedLimits.Length < 2)
        {
            gearSpeedLimits = new float[] { 0f, 80f, 120f, 165f, 220f, 280f, 340f, 400f };
        }

        if (gearTorqueMultipliers == null || gearTorqueMultipliers.Length < 2)
        {
            gearTorqueMultipliers = new float[] { 0f, 1.75f, 1.45f, 1.2f, 1.05f, 0.9f, 0.8f, 0.7f };
        }

        currentGear = Mathf.Clamp(currentGear, 1, GetMaxForwardGear());
    }

    private void SetWheelFriction(WheelCollider wheel, float forwardStiffness, float sideStiffness)
    {
        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.extremumSlip = 0.25f;
        forward.extremumValue = 1f;
        forward.asymptoteSlip = 0.75f;
        forward.asymptoteValue = 0.72f;
        forward.stiffness = forwardStiffness;
        wheel.forwardFriction = forward;

        WheelFrictionCurve side = wheel.sidewaysFriction;
        side.extremumSlip = 0.18f;
        side.extremumValue = 1f;
        side.asymptoteSlip = 0.48f;
        side.asymptoteValue = 0.78f;
        side.stiffness = sideStiffness;
        wheel.sidewaysFriction = side;
    }

    private float GetSlipAngle()
    {
        Vector3 flatVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        if (flatVelocity.sqrMagnitude < 1f) return 0f;

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, transform.up).normalized;
        return Vector3.SignedAngle(flatForward, flatVelocity.normalized, transform.up);
    }

    private float GetWheelbase()
    {
        return Mathf.Max(0.1f, Mathf.Abs(frontLeft.transform.localPosition.z - rearLeft.transform.localPosition.z));
    }

    private void UpdateGrounding()
    {
        groundedWheelCount = 0;

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i].GetGroundHit(out _))
            {
                groundedWheelCount++;
            }
        }
    }

    private void SyncWheelMeshes()
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            Transform mesh = wheelMeshes[i];
            if (mesh == null) continue;

            wheels[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
            mesh.SetPositionAndRotation(position, rotation * Quaternion.Euler(wheelRotationOffset));
        }
    }

    private void SetFrontMotorTorque(float torque)
    {
        frontLeft.motorTorque = torque;
        frontRight.motorTorque = torque;
    }

    private void SetRearMotorTorque(float torque)
    {
        rearLeft.motorTorque = torque;
        rearRight.motorTorque = torque;
    }
    private void SetBrakeTorque(float torque)
    {
        frontLeft.brakeTorque = torque;
        frontRight.brakeTorque = torque;
        rearLeft.brakeTorque = torque;
        rearRight.brakeTorque = torque;
    }
    private void CacheWheelArrays()
    {
        wheels = new[] { frontLeft, frontRight, rearLeft, rearRight };
        wheelMeshes = new[] { frontLeftMesh, frontRightMesh, rearLeftMesh, rearRightMesh };
    }

    private bool ValidateSetup()
    {
        
        if (frontLeft != null && frontRight != null && rearLeft != null && rearRight != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(CarController)} on {name} needs four WheelCollider references.", this);
        return false;
    }

    private void AutoAssignMissingReferences()
    {
        if (frontLeft == null) frontLeft = FindWheelCollider("FL_Collider", "FrontLeft_Collider", "FrontLeft", "FL");
        if (frontRight == null) frontRight = FindWheelCollider("FR_Collider", "FrontRight_Collider", "FrontRight", "FR");
        if (rearLeft == null) rearLeft = FindWheelCollider("RL_Collider", "RearLeft_Collider", "RearLeft", "RL");
        if (rearRight == null) rearRight = FindWheelCollider("RR_Collider", "RearRight_Collider", "RearRight", "RR");

        if (frontLeftMesh == null) frontLeftMesh = FindChild("FL_Mesh", "FrontLeft_Mesh", "FrontLeftMesh", "FLMesh");
        if (frontRightMesh == null) frontRightMesh = FindChild("FR_Mesh", "FrontRight_Mesh", "FrontRightMesh", "FRMesh");
        if (rearLeftMesh == null) rearLeftMesh = FindChild("RL_Mesh", "RearLeft_Mesh", "RearLeftMesh", "RLMesh");
        if (rearRightMesh == null) rearRightMesh = FindChild("RR_Mesh", "RearRight_Mesh", "RearRightMesh", "RRMesh");
    }

    private WheelCollider FindWheelCollider(params string[] names)
    {
        Transform child = FindChild(names);
        if (child != null && child.TryGetComponent(out WheelCollider wheel))
        {
            return wheel;
        }

        return null;
    }

    private Transform FindChild(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindChildRecursive(transform, names[i]);
            if (found != null) return found;
        }

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null) return nested;
        }

        return null;
    }
    private void OnValidate()
    {
        motorTorque = Mathf.Max(0f, motorTorque);
        reverseTorque = Mathf.Max(0f, reverseTorque);
        maxSpeed = Mathf.Max(400f, maxSpeed);
        brakeTorque = Mathf.Max(0f, brakeTorque);
        engineBraking = Mathf.Max(0f, engineBraking);
        maxSteerAngle = Mathf.Clamp(maxSteerAngle, 1f, 45f);
        handlingReferenceSpeed = Mathf.Max(40f, handlingReferenceSpeed);
        downforce = Mathf.Max(0f, downforce);
        maxDownforce = Mathf.Max(0f, maxDownforce);
        aeroDrag = Mathf.Max(0f, aeroDrag);
        lateralStability = Mathf.Max(0f, lateralStability);
        yawStability = Mathf.Max(0f, yawStability);
        yawDamping = Mathf.Max(0f, yawDamping);
        pitchRollDamping = Mathf.Max(0f, pitchRollDamping);
        frontAntiRoll = Mathf.Max(0f, frontAntiRoll);
        rearAntiRoll = Mathf.Max(0f, rearAntiRoll);
        vehicleMass = Mathf.Max(1f, vehicleMass);
        suspensionSpring = Mathf.Max(1f, suspensionSpring);
        suspensionDamper = Mathf.Max(0f, suspensionDamper);
        suspensionDistance = Mathf.Max(0.01f, suspensionDistance);
        forceAppPointDistance = Mathf.Max(0f, forceAppPointDistance);
        wheelMass = Mathf.Max(1f, wheelMass);
        wheelDampingRate = Mathf.Max(0f, wheelDampingRate);
        allowedDriveSlip = Mathf.Max(0.01f, allowedDriveSlip);
        shiftUpRatio = Mathf.Clamp(shiftUpRatio, 0.5f, 0.98f);
        shiftDownRatio = Mathf.Clamp(shiftDownRatio, 0.1f, 0.8f);
        shiftDelay = Mathf.Max(0.01f, shiftDelay);
        reverseSpeedKph = Mathf.Max(0f, reverseSpeedKph);
        drsActivationSpeed = Mathf.Max(0f, drsActivationSpeed);
        drsDragReduction = Mathf.Clamp01(drsDragReduction);
        drsDownforceReduction = Mathf.Clamp01(drsDownforceReduction);
        EnsureTransmissionData(); 
    }
}

