// Traffic Simulation
// https://github.com/mchrbn/unity-traffic-simulation

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSimulation
{

    public struct Target
    {
        public int segment;
        public int waypoint;
    }

    public enum Status
    {
        GO,
        STOP,
        SLOW_DOWN
    }

    public class VehicleAI : MonoBehaviour
    {
        [Header("Traffic System")]
        public TrafficSystem trafficSystem;
        public float waypointThresh = 6;

        [Header("Radar")]
        public Transform raycastAnchor;
        public float raycastLength = 7;
        public int raySpacing = 4;
        public int raysNumber = 6;
        public float emergencyBrakeThresh = 4f;
        public float slowDownThresh = 6f;

        [HideInInspector] public Status vehicleStatus = Status.GO;

        private WheelDrive wheelDrive;
        private float initMaxSpeed = 0;
        private int pastTargetSegment = -1;
        private Target currentTarget;
        private Target futureTarget;

        void Start()
        {
            wheelDrive = this.GetComponent<WheelDrive>();
            if (trafficSystem == null) return;
            initMaxSpeed = wheelDrive.maxSpeed;
            SetWaypointVehicleIsOn();
        }

        void Update()
        {
            if (trafficSystem == null) return;
            WaypointChecker();
            MoveVehicle();
        }

        void WaypointChecker()
        {
            GameObject waypoint = trafficSystem.segments[currentTarget.segment].waypoints[currentTarget.waypoint].gameObject;
            Vector3 wpDist = this.transform.InverseTransformPoint(new Vector3(waypoint.transform.position.x, this.transform.position.y, waypoint.transform.position.z));

            if (wpDist.magnitude < waypointThresh)
            {
                currentTarget.waypoint++;
                if (currentTarget.waypoint >= trafficSystem.segments[currentTarget.segment].waypoints.Count)
                {
                    pastTargetSegment = currentTarget.segment;
                    currentTarget.segment = futureTarget.segment;
                    currentTarget.waypoint = 0;
                }

                futureTarget.waypoint = currentTarget.waypoint + 1;
                if (futureTarget.waypoint >= trafficSystem.segments[currentTarget.segment].waypoints.Count)
                {
                    futureTarget.waypoint = 0;
                    futureTarget.segment = GetNextSegmentId();
                }
            }
        }

        void MoveVehicle()
        {
            float acc = 1;
            float brake = 0;
            float steering = 0;
            wheelDrive.maxSpeed = initMaxSpeed;

            Transform targetTransform = trafficSystem.segments[currentTarget.segment].waypoints[currentTarget.waypoint].transform;
            Transform futureTargetTransform = trafficSystem.segments[futureTarget.segment].waypoints[futureTarget.waypoint].transform;
            Vector3 futureVel = futureTargetTransform.position - targetTransform.position;
            float futureSteering = Mathf.Clamp(this.transform.InverseTransformDirection(futureVel.normalized).x, -1, 1);

            if (vehicleStatus == Status.STOP)
            {
                acc = 0;
                brake = 1;
                wheelDrive.maxSpeed = Mathf.Min(wheelDrive.maxSpeed / 2f, 5f);
            }
            else
            {
                if (vehicleStatus == Status.SLOW_DOWN)
                {
                    acc = .3f;
                    brake = 0f;
                }

                if (futureSteering > .3f || futureSteering < -.3f)
                {
                    wheelDrive.maxSpeed = Mathf.Min(wheelDrive.maxSpeed, wheelDrive.steeringSpeedMax);
                }

                float hitDist;
                GameObject obstacle = GetDetectedObstacles(out hitDist);

                if (obstacle != null)
                {
                    WheelDrive otherVehicle = null;
                    otherVehicle = obstacle.GetComponent<WheelDrive>();

                    if (otherVehicle != null)
                    {
                        float dotFront = Vector3.Dot(this.transform.forward, otherVehicle.transform.forward);

                        if (otherVehicle.maxSpeed < wheelDrive.maxSpeed && dotFront > .8f)
                        {
                            float ms = Mathf.Max(wheelDrive.GetSpeedMS(otherVehicle.maxSpeed) - .5f, .1f);
                            wheelDrive.maxSpeed = wheelDrive.GetSpeedUnit(ms);
                        }

                        if (hitDist < emergencyBrakeThresh && dotFront > .8f)
                        {
                            acc = 0;
                            brake = 1;
                            wheelDrive.maxSpeed = Mathf.Max(wheelDrive.maxSpeed / 2f, wheelDrive.minSpeed);
                        }
                        else if (hitDist < emergencyBrakeThresh && dotFront <= .8f)
                        {
                            acc = -.3f;
                            brake = 0f;
                            wheelDrive.maxSpeed = Mathf.Max(wheelDrive.maxSpeed / 2f, wheelDrive.minSpeed);

                            float dotRight = Vector3.Dot(this.transform.forward, otherVehicle.transform.right);
                            if (dotRight > 0.1f) steering = -.3f;
                            else if (dotRight < -0.1f) steering = .3f;
                            else steering = -.7f;
                        }
                        else if (hitDist < slowDownThresh)
                        {
                            acc = .5f;
                            brake = 0f;
                        }
                    }
                    else
                    {
                        if (hitDist < emergencyBrakeThresh)
                        {
                            acc = 0;
                            brake = 1;
                            wheelDrive.maxSpeed = Mathf.Max(wheelDrive.maxSpeed / 2f, wheelDrive.minSpeed);
                        }
                        else if (hitDist < slowDownThresh)
                        {
                            acc = .5f;
                            brake = 0f;
                        }
                    }
                }

                if (acc > 0f)
                {
                    Vector3 desiredVel = trafficSystem.segments[currentTarget.segment].waypoints[currentTarget.waypoint].transform.position - this.transform.position;
                    steering = Mathf.Clamp(this.transform.InverseTransformDirection(desiredVel.normalized).x, -1f, 1f);
                }
            }

            wheelDrive.Move(acc, steering, brake);
        }

        GameObject GetDetectedObstacles(out float _hitDist)
        {
            GameObject detectedObstacle = null;
            float minDist = 1400f;
            float hitDist = -2f;

            float initRay = (raysNumber / 2f) * raySpacing;
            for (float a = -initRay; a <= initRay; a += raySpacing)
            {
                CastRay(raycastAnchor.transform.position, a, this.transform.forward, raycastLength, out detectedObstacle, out hitDist);
                if (detectedObstacle == null) continue;
                float dist = Vector3.Distance(this.transform.position, detectedObstacle.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    break;
                }
            }

            _hitDist = hitDist;
            return detectedObstacle;
        }

        void CastRay(Vector3 _anchor, float _angle, Vector3 _dir, float _length, out GameObject _outObstacle, out float _outHitDistance)
        {
            _outObstacle = null;
            _outHitDistance = -2f;

            Debug.DrawRay(_anchor, Quaternion.Euler(0, _angle, 0) * _dir * _length, new Color(1, 0, 0, 0.5f));

            int layer = 1 << LayerMask.NameToLayer("AutonomousVehicle");
            int finalMask = layer;

            foreach (string layerName in trafficSystem.collisionLayers)
            {
                int id = 1 << LayerMask.NameToLayer(layerName);
                finalMask = finalMask | id;
            }

            RaycastHit hit;
            if (Physics.Raycast(_anchor, Quaternion.Euler(0, _angle, 0) * _dir, out hit, _length, finalMask))
            {
                _outObstacle = hit.collider.gameObject;
                _outHitDistance = hit.distance;
            }
        }

        int GetNextSegmentId()
        {
            if (trafficSystem.segments[currentTarget.segment].nextSegments.Count == 0)
                return 0;
            int c = Random.Range(0, trafficSystem.segments[currentTarget.segment].nextSegments.Count);
            return trafficSystem.segments[currentTarget.segment].nextSegments[c].id;
        }

        void SetWaypointVehicleIsOn()
        {
            foreach (Segment segment in trafficSystem.segments)
            {
                if (segment.IsOnSegment(this.transform.position))
                {
                    currentTarget.segment = segment.id;

                    float minDist = float.MaxValue;
                    for (int j = 0; j < trafficSystem.segments[currentTarget.segment].waypoints.Count; j++)
                    {
                        float d = Vector3.Distance(this.transform.position, trafficSystem.segments[currentTarget.segment].waypoints[j].transform.position);
                        Vector3 lSpace = this.transform.InverseTransformPoint(trafficSystem.segments[currentTarget.segment].waypoints[j].transform.position);
                        if (d < minDist && lSpace.z > 0)
                        {
                            minDist = d;
                            currentTarget.waypoint = j;
                        }
                    }
                    break;
                }
            }

            futureTarget.waypoint = currentTarget.waypoint + 1;
            futureTarget.segment = currentTarget.segment;

            if (futureTarget.waypoint >= trafficSystem.segments[currentTarget.segment].waypoints.Count)
            {
                futureTarget.waypoint = 0;
                futureTarget.segment = GetNextSegmentId();
            }
        }

        public int GetSegmentVehicleIsIn()
        {
            int vehicleSegment = currentTarget.segment;
            bool isOnSegment = trafficSystem.segments[vehicleSegment].IsOnSegment(this.transform.position);
            if (!isOnSegment)
            {
                bool isOnPSegement = trafficSystem.segments[pastTargetSegment].IsOnSegment(this.transform.position);
                if (isOnPSegement)
                    vehicleSegment = pastTargetSegment;
            }
            return vehicleSegment;
        }
    }

}