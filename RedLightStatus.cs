// Traffic Simulation
// https://github.com/mchrbn/unity-traffic-simulation

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrafficSimulation;

public class RedLightStatus : MonoBehaviour
{
    public int lightGroupId;
    public Intersection intersection;

    public float yellowDuration = 4f;

    private Light pointLight;

    private int lastGroup = -1;
    private bool isYellow = false;
    private float timer = 0f;

    void Start()
    {
        pointLight = this.transform.GetChild(0).GetComponent<Light>();
        SetTrafficLightColor();
    }

    void Update()
    {
        if (intersection == null) return;

        if (intersection.currentRedLightsGroup != lastGroup)
        {
            isYellow = true;
            timer = 0f;
            lastGroup = intersection.currentRedLightsGroup;
        }

        if (isYellow)
        {
            timer += Time.deltaTime;
            pointLight.color = Color.yellow;

            if (timer >= yellowDuration)
            {
                isYellow = false;
                SetTrafficLightColor();
            }
        }
    }

    void SetTrafficLightColor()
    {
        if (isYellow) return;

        if (lightGroupId == intersection.currentRedLightsGroup)
            pointLight.color = Color.red;
        else
            pointLight.color = Color.green;
    }
}