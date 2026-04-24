// Traffic Simulation
// https://github.com/mchrbn/unity-traffic-simulation
// Based on the Vehicle Tools package from Unity

using UnityEngine;
using System;

namespace TrafficSimulation
{
    [Serializable]
    public enum DriveType
    {
        RearWheelDrive,
        FrontWheelDrive,
        AllWheelDrive
    }

    [Serializable]
    public enum UnitType
    {
        KMH,
        MPH
    }

    public class WheelDrive : MonoBehaviour
    {
        [Tooltip("Downforce applied to the vehicle")]
        public float downForce = 100f;
        public float maxAngle = 30f;
        public float steeringLerp = 5f;
        public float steeringSpeedMax = 20f;
        public float maxTorque = 300f;
        public float brakeTorque = 30000f;
        public UnitType unitType;
        public float minSpeed = 5;
        public float maxSpeed = 50;
        public GameObject leftWheelShape;
        public GameObject rightWheelShape;
        public bool animateWheels = true;
        public DriveType driveType;

        private WheelCollider[] wheels;
        private float currentSteering = 0f;

        void OnEnable()
        {
            wheels = GetComponentsInChildren<WheelCollider>();
            for (int i = 0; i < wheels.Length; ++i)
            {
                var wheel = wheels[i];
                if (leftWheelShape != null && wheel.transform.localPosition.x < 0)
                {
                    var ws = Instantiate(leftWheelShape);
                    ws.transform.parent = wheel.transform;
                }
                else if (rightWheelShape != null && wheel.transform.localPosition.x > 0)
                {
                    var ws = Instantiate(rightWheelShape);
                    ws.transform.parent = wheel.transform;
                }
                wheel.ConfigureVehicleSubsteps(10, 1, 1);
            }
        }

        public void Move(float _acceleration, float _steering, float _brake)
        {
            float nSteering = Mathf.Lerp(currentSteering, _steering, Time.deltaTime * steeringLerp);
            currentSteering = nSteering;
            Rigidbody rb = this.GetComponent<Rigidbody>();
            float angle = maxAngle * nSteering;
            float torque = maxTorque * _acceleration;
            float handBrake = _brake > 0 ? brakeTorque : 0;

            foreach (WheelCollider wheel in wheels)
            {
                if (wheel.transform.localPosition.z > 0) wheel.steerAngle = angle;
                if (wheel.transform.localPosition.z < 0) wheel.brakeTorque = handBrake;
                if (wheel.transform.localPosition.z < 0 && driveType != DriveType.FrontWheelDrive) wheel.motorTorque = torque;
                if (wheel.transform.localPosition.z >= 0 && driveType != DriveType.RearWheelDrive) wheel.motorTorque = torque;

                if (animateWheels)
                {
                    Quaternion q;
                    Vector3 p;
                    wheel.GetWorldPose(out p, out q);
                    Transform shapeTransform = wheel.transform.GetChild(0);
                    shapeTransform.position = p;
                    shapeTransform.rotation = q;
                }
            }

            float s = GetSpeedUnit(rb.linearVelocity.magnitude);
            if (s > maxSpeed) rb.linearVelocity = GetSpeedMS(maxSpeed) * rb.linearVelocity.normalized;
            rb.AddForce(-transform.up * downForce * rb.linearVelocity.magnitude);
        }

        public float GetSpeedMS(float _s)
        {
            return unitType == UnitType.KMH ? _s / 3.6f : _s / 2.237f;
        }

        public float GetSpeedUnit(float _s)
        {
            return unitType == UnitType.KMH ? _s * 3.6f : _s * 2.237f;
        }
    }
}