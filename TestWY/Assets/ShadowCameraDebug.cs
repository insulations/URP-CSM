﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

// 这个脚本挂相机上测试
[ExecuteAlways]
public class ShadowCameraDebug : MonoBehaviour
{
    MyMainLightShadowCasterPass.CSM csm;

    private UniversalAdditionalCameraData _cameraData;

    private ForwardRenderer renderer;
    // Start is called before the first frame update
    void Start()
    {
        _cameraData = GetComponent<UniversalAdditionalCameraData>();
        renderer = (ForwardRenderer)_cameraData.scriptableRenderer;
    }

    void DrawUnitySphere()
    {
        _cameraData = GetComponent<UniversalAdditionalCameraData>();
        renderer = (ForwardRenderer)_cameraData.scriptableRenderer;
        Vector3[] pos = new Vector3[] {
            new Vector3(renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[0].x,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[0].y,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[0].z),
            new Vector3(renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[1].x,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[1].y,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[1].z),
            new Vector3(renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[2].x,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[2].y,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[2].z),
            new Vector3(renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[3].x,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[3].y,
                renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[3].z)
        };
        float[] r = new []
        {
            renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[0].w,
            renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[1].w,
            renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[2].w,
            renderer.m_MainLightShadowCasterPass.m_CascadeSplitDistances[3].w
        } ;
 
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pos[3],r[3]);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos[2],r[2]);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos[1],r[1]);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(pos[0],r[0]);
        
        Debug.Log("Cascated1 unity r = "+r[0]);
    }
    void OnDrawGizmosSelected()
    {
        Camera mainCam = Camera.main;
        //DrawUnitySphere();
        Light light = RenderSettings.sun;
        Vector3 lightDir = light.transform.rotation * Vector3.forward;
        if(csm==null) csm = new MyMainLightShadowCasterPass.CSM();

        Gizmos.color = Color.cyan;
        
        csm.Update(mainCam,lightDir);
        //Gizmos.DrawWireSphere(csm.splitSpheres[1], csm.splitSpheres[1].w);
        //csm.DrawSplitSphere();
        DrawUnitySphere();
        //Debug.Log("Cascated1 my r = "+ csm.splitSpheres[0].w);
    }
}
