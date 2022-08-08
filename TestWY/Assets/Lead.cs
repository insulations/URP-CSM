using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

[ExecuteAlways]
public class Lead : MonoBehaviour
{
    //public GameObject go;
    // Start is called before the first frame update
    Bounds bounds = new Bounds();
    public Vector4 splitSphere = new Vector4();
    private Transform shadowCaster;
    private SkinnedMeshRenderer[] skinmeshes;
    public float shadowNearClipDistance = 10;
    public float shadowFarClipDistance = 10;
    
    private Matrix4x4 viewMatrix, projMatrix;
    
    private UniversalAdditionalCameraData cameraData;

    Vector3[] farCorners = new Vector3[4];
    Vector3[] nearCorners = new Vector3[4];
    void Start()
    {
        shadowCaster = transform;
        cameraData = Camera.main.gameObject.GetComponent<UniversalAdditionalCameraData>();
    }

    // Update is called once per frame
    void Update()
    {
        CalculateSphere();
        CalculateMatrix();
        DrawFrustum(nearCorners,farCorners,Color.magenta);
    }
    void CalculateAABB(int boundsCount, SkinnedMeshRenderer skinmeshRender)
    {
        if(boundsCount != 0)
        {

            bounds.Encapsulate(skinmeshRender.bounds);
                
            
        }
        else
        {
            bounds = skinmeshRender.bounds;
               
        }
    }

    void CalculateAABB()
    {
        skinmeshes = shadowCaster.GetComponentsInChildren<SkinnedMeshRenderer>();
        int boundsCount = 0;

        for(int i = 0;i <skinmeshes.Length;i++)
        {
            CalculateAABB(boundsCount, skinmeshes[i]);
            boundsCount += 1;
        }
        
    }

    void CalculateSphere()
    {
        CalculateAABB();
        splitSphere.x = bounds.center.x;
        splitSphere.y = bounds.center.y;
        splitSphere.z = bounds.center.z;
        splitSphere.w = Vector3.Distance(bounds.extents, Vector3.zero)+0.2f;
        MainLightShadowCasterPass.s_LeadSplitDistances = splitSphere;
        
        
    }

    void DrawFrustum(Vector3[] nearCorners, Vector3[] farCorners, Color color)
    {
        for (int i = 0; i < 4; i++)
            Debug.DrawLine(nearCorners[i], farCorners[i], color);

        Debug.DrawLine(farCorners[0], farCorners[1], color);
        Debug.DrawLine(farCorners[0], farCorners[3], color);
        Debug.DrawLine(farCorners[2], farCorners[1], color);
        Debug.DrawLine(farCorners[2], farCorners[3], color);
        Debug.DrawLine(nearCorners[0], nearCorners[1], color);
        Debug.DrawLine(nearCorners[0], nearCorners[3], color);
        Debug.DrawLine(nearCorners[2], nearCorners[1], color);
        Debug.DrawLine(nearCorners[2], nearCorners[3], color);
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(splitSphere,splitSphere.w);
        
    }

    void CalculateMatrix()
    {
        Light light = RenderSettings.sun;
        //Debug.Log(Vector3.Dot(light.transform.forward,go.transform.forward));
        //light.layerShadowCullDistances[8] = 0.01f;
        Vector3 o = (Vector3)splitSphere;
        float r = splitSphere.w;
        Vector3 pos = o ;

        farCorners[0] = pos + r * (light.transform.forward*shadowFarClipDistance - light.transform.right - light.transform.up);
        farCorners[1] = pos + r * (light.transform.forward*shadowFarClipDistance - light.transform.right + light.transform.up);
        farCorners[2] = pos + r * (light.transform.forward*shadowFarClipDistance + light.transform.right + light.transform.up);
        farCorners[3] = pos + r * (light.transform.forward*shadowFarClipDistance + light.transform.right - light.transform.up);
        nearCorners[0] = pos + r * (-light.transform.forward*shadowNearClipDistance - light.transform.right - light.transform.up);
        nearCorners[1] = pos + r * (-light.transform.forward*shadowNearClipDistance - light.transform.right + light.transform.up);
        nearCorners[2] = pos + r * (-light.transform.forward*shadowNearClipDistance + light.transform.right + light.transform.up);
        nearCorners[3] = pos + r * (-light.transform.forward*shadowNearClipDistance + light.transform.right - light.transform.up);
        
        
        
        Matrix4x4 toShadowViewInv = Matrix4x4.LookAt(pos, 
            pos + light.transform.forward, light.transform.up);
        
        Matrix4x4 toShadowView = toShadowViewInv.inverse;
        viewMatrix = toShadowView;//light.transform.worldToLocalMatrix;
        Vector3 min = new Vector3();
        Vector3 max = new Vector3();
        
        o = viewMatrix.MultiplyPoint(o);
        min = o - new Vector3(r,r,r+shadowNearClipDistance);
        max = o + new Vector3(r,r,r+shadowFarClipDistance);
        
        if (SystemInfo.usesReversedZBuffer)
        {
            viewMatrix.m20 = -viewMatrix.m20;
            viewMatrix.m21 = -viewMatrix.m21;
            viewMatrix.m22 = -viewMatrix.m22;
            viewMatrix.m23 = -viewMatrix.m23;
        }
        
        Vector4 row0 = new Vector4(2/(max.x - min.x),0, 0,0);
        Vector4 row1 = new Vector4(0, 2 / (max.y - min.y), 0, 0);
        Vector4 row2 = new Vector4(0, 0, -2 / (max.z - min.z), -(max.z + min.z) / (max.z - min.z));
        Vector4 row3 = new Vector4(0, 0, 0, 1);
        
        projMatrix.SetRow(0, row0);
        projMatrix.SetRow(1, row1);
        projMatrix.SetRow(2, row2);
        projMatrix.SetRow(3, row3);
        

        MainLightShadowCasterPass.s_LeadViewMatrix = viewMatrix;
        MainLightShadowCasterPass.s_LeadProjectionMatrix = projMatrix;
        MainLightShadowCasterPass.othLength = max.y - min.y;
    }
}
