using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using UnityEngine.Serialization;

[ExecuteAlways]
public class Lead : MonoBehaviour
{
    Bounds bounds = new Bounds();
    private Vector4 splitSphere = new Vector4();
    
    private SkinnedMeshRenderer[] skinmeshes;
    private Matrix4x4 viewMatrix, projMatrix;
    Vector3[] farCorners = new Vector3[4];
    Vector3[] nearCorners = new Vector3[4];
    
    [Header("使用主角独立层级")]
    [Space(10)]
    [Header("当采用包含主角独立层级的级联阴影时请将最后一层级的比例置为0(最小值)")]
    [Header("注意！该脚本单场景中只应存在一个！并将主角的层级设置为Lead")]
    public bool useLeadCascade = true;
    [Header("主角的transform")]
    public Transform shadowCaster;
    [Header("主角包围球半径增加长度")][Range(0f,30f)]
    public float radiusAdd = 0.5f;
    [Header("主角层级远截面增加距离")][Range(0f,50f)]
    public float shadowFarClipDistance = 3;

    [Header("主角阴影Bias")] [Range(0f, 1.0f)] public float bias = 0;
    
    // Update is called once per frame
    void Update()
    {
        MainLightShadowCasterPass.useLeadCascade = useLeadCascade;
        if (KeyWordSetting())
        {
            CalculateSphere();
            CalculateMatrix();
        }
    }

    bool KeyWordSetting()
    {
        if (!useLeadCascade)
        {
            Shader.DisableKeyword("_USE_LEAD_CASCADE");
            return false;
        }
        UniversalRenderPipelineAsset urpa = (UniversalRenderPipelineAsset) GraphicsSettings.renderPipelineAsset;
        
        if (urpa.shadowCascadeOption == ShadowCascadesOption.NoCascades)
        {
            Shader.DisableKeyword("_USE_LEAD_CASCADE");
            useLeadCascade = false;
            MainLightShadowCasterPass.useLeadCascade = false;
            return false;
        } 
        
        if (urpa.shadowCascadeOption == ShadowCascadesOption.TwoCascades)
        {
            Shader.EnableKeyword("_LEAD_2_CASCADES");
        }
        else
        {
            Shader.DisableKeyword("_LEAD_2_CASCADES");
        }
        Shader.EnableKeyword("_USE_LEAD_CASCADE");
        return true;
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
        splitSphere.w = Vector3.Distance(bounds.extents, Vector3.zero)+radiusAdd;
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
        DrawFrustum(nearCorners,farCorners,Color.magenta);
    }

    void CalculateMatrix()
    {
        Light light = RenderSettings.sun;

        Vector3 o = (Vector3)splitSphere;
        float r = splitSphere.w;
        Vector3 pos = o ;

        farCorners[0] = pos + r * (light.transform.forward*(shadowFarClipDistance+1.0f) - light.transform.right - light.transform.up);
        farCorners[1] = pos + r * (light.transform.forward*(shadowFarClipDistance+1.0f) - light.transform.right + light.transform.up);
        farCorners[2] = pos + r * (light.transform.forward*(shadowFarClipDistance+1.0f) + light.transform.right + light.transform.up);
        farCorners[3] = pos + r * (light.transform.forward*(shadowFarClipDistance+1.0f) + light.transform.right - light.transform.up);
        nearCorners[0] = pos + r * (-light.transform.forward - light.transform.right - light.transform.up);
        nearCorners[1] = pos + r * (-light.transform.forward - light.transform.right + light.transform.up);
        nearCorners[2] = pos + r * (-light.transform.forward + light.transform.right + light.transform.up);
        nearCorners[3] = pos + r * (-light.transform.forward + light.transform.right - light.transform.up);
        
        
        
        Matrix4x4 toShadowViewInv = Matrix4x4.LookAt(pos, 
            pos + light.transform.forward, light.transform.up);
        
        Matrix4x4 toShadowView = toShadowViewInv.inverse;
        viewMatrix = toShadowView;//light.transform.worldToLocalMatrix;
        Vector3 min = new Vector3();
        Vector3 max = new Vector3();
        
        o = viewMatrix.MultiplyPoint(o);
        min = o - new Vector3(r,r,r-bias);
        max = o + new Vector3(r,r,r+shadowFarClipDistance);
        
        viewMatrix.m20 = -viewMatrix.m20;
        viewMatrix.m21 = -viewMatrix.m21;
        viewMatrix.m22 = -viewMatrix.m22;
        viewMatrix.m23 = -viewMatrix.m23;
        
        
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
        MainLightShadowCasterPass.farOffset = shadowFarClipDistance;
    }
}
