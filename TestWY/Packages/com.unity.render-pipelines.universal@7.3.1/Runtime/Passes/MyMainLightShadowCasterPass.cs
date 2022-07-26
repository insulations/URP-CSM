using System;

namespace UnityEngine.Rendering.Universal.Internal
{
    public class MyMainLightShadowCasterPass : ScriptableRenderPass
    {
        private static class MainLightShadowConstantBuffer
        {
            public static int _WorldToShadow;
            public static int _ShadowParams;
            public static int _CascadeShadowSplitSpheres0;
            public static int _CascadeShadowSplitSpheres1;
            public static int _CascadeShadowSplitSpheres2;
            public static int _CascadeShadowSplitSpheres3;//四级剔除球
            public static int _CascadeShadowSplitSphereRadii;
            public static int _ShadowOffset0;
            public static int _ShadowOffset1;
            public static int _ShadowOffset2;
            public static int _ShadowOffset3;
            public static int _ShadowmapSize;
        }
        const int k_MaxCascades = 4;
        const int k_ShadowmapBufferBits = 16;
        int m_ShadowmapWidth;
        int m_ShadowmapHeight;
        int m_ShadowCasterCascadesCount;
        bool m_SupportsBoxFilterForShadows;

        RenderTargetHandle m_MainLightShadowmap;
        RenderTexture m_MainLightShadowmapTexture;
        public bool Setup(ref RenderingData renderingData)
        {
            
            return true;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            throw new NotImplementedException();
        }

        public class CSM
        {
            public float[] splits = {0.067f, 0.133f, 0.267f, 0.533f};
            public Vector3[] box0,box1,box2,box3;
            public Vector4[] splitSpheres = new Vector4[4];
            
            // 主相机视锥体
            Vector3[] farCorners = new Vector3[4];
            Vector3[] nearCorners = new Vector3[4];

            // 主相机划分四个视锥体
            Vector3[] f0_near = new Vector3[4], f0_far = new Vector3[4];
            Vector3[] f1_near = new Vector3[4], f1_far = new Vector3[4];
            Vector3[] f2_near = new Vector3[4], f2_far = new Vector3[4];
            Vector3[] f3_near = new Vector3[4], f3_far = new Vector3[4];
            
            //四个视锥体的绘制范围
            float[] far = new float[4], near = new float[4];

            // 齐次坐标矩阵乘法变换
            Vector3 matTransform(Matrix4x4 m, Vector3 v, float w)
            {
                Vector4 v4 = new Vector4(v.x, v.y, v.z, w);
                v4 = m * v4;
                return new Vector3(v4.x, v4.y, v4.z);
            }

            public Vector4 LightSpaceSplitSphere(Vector3[] nearCorners, Vector3[] farCorners, float near, float far,Camera cam)
            {
                //计算包围球
                
                float a = Vector3.Distance(cam.transform.position + nearCorners[0],
                    cam.transform.position + nearCorners[2]);
                float b = Vector3.Distance(cam.transform.position + farCorners[0],
                    cam.transform.position + farCorners[2]);
                float l = far - near;
                float x = l * 0.5f - (a*a - b*b) / (8.0f * l);

                float PO = x + near;
                Vector3 pos = cam.transform.forward * PO + cam.transform.position;
                float r = Mathf.Sqrt(a * a * 0.25f + x * x);
                return new Vector4(pos.x,pos.y,pos.z,r);
            }
            // 计算光源方向包围盒的世界坐标
            Vector3[] LightSpaceAABB(Vector3[] nearCorners, Vector3[] farCorners, Vector3 lightDir)
            {
                Matrix4x4 toShadowViewInv = Matrix4x4.LookAt(Vector3.zero, lightDir, Vector3.up);
                Matrix4x4 toShadowView = toShadowViewInv.inverse;

                // 视锥体顶点转光源方向
                for(int i=0; i<4; i++)
                {
                    farCorners[i] = matTransform(toShadowView, farCorners[i], 1.0f);
                    nearCorners[i] = matTransform(toShadowView, nearCorners[i], 1.0f);
                }

                // 计算 AABB 包围盒
                float[] x = new float[8];
                float[] y = new float[8];
                float[] z = new float[8];
                for(int i=0; i<4; i++)
                {
                    x[i] = nearCorners[i].x; x[i+4] = farCorners[i].x;
                    y[i] = nearCorners[i].y; y[i+4] = farCorners[i].y;
                    z[i] = nearCorners[i].z; z[i+4] = farCorners[i].z;
                }
                float xmin=Mathf.Min(x), xmax=Mathf.Max(x);
                float ymin=Mathf.Min(y), ymax=Mathf.Max(y);
                float zmin=Mathf.Min(z), zmax=Mathf.Max(z);

                // 包围盒顶点转世界坐标
                Vector3[] points = {
                    new Vector3(xmin, ymin, zmin), new Vector3(xmin, ymin, zmax), new Vector3(xmin, ymax, zmin), new Vector3(xmin, ymax, zmax),
                    new Vector3(xmax, ymin, zmin), new Vector3(xmax, ymin, zmax), new Vector3(xmax, ymax, zmin), new Vector3(xmax, ymax, zmax)
                };
                for(int i=0; i<8; i++)
                    points[i] = matTransform(toShadowViewInv, points[i], 1.0f);

                // 视锥体顶还原
                for(int i=0; i<4; i++)
                {
                    farCorners[i] = matTransform(toShadowViewInv, farCorners[i], 1.0f);
                    nearCorners[i] = matTransform(toShadowViewInv, nearCorners[i], 1.0f);
                }

                return points;
            }

            // 用主相机和光源方向更新 CSM 划分
            public void Update(Camera mainCam, Vector3 lightDir)
            {
                // 获取主相机视锥体
                mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), mainCam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farCorners);
                mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), mainCam.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearCorners);

                // 视锥体向量转世界坐标
                for (int i = 0; i < 4; i++)
                {
                    farCorners[i] = mainCam.transform.TransformVector(farCorners[i]);
                    nearCorners[i] = mainCam.transform.TransformVector(nearCorners[i]);
                }

                // 按照比例划分相机视锥体
                for(int i=0; i<4; i++)
                {
                    Vector3 dir = farCorners[i] - nearCorners[i];

                    f0_near[i] = nearCorners[i];
                    f0_far[i] = f0_near[i] + dir * splits[0];

                    f1_near[i] = f0_far[i];
                    f1_far[i] = f1_near[i] + dir * splits[1];

                    f2_near[i] = f1_far[i];
                    f2_far[i] = f2_near[i] + dir * splits[2];

                    f3_near[i] = f2_far[i];
                    f3_far[i] = f3_near[i] + dir * splits[3];
                }
                
                //计算绘制距离
                float dis = mainCam.farClipPlane - mainCam.nearClipPlane;
                near[0] = mainCam.nearClipPlane;
                far[0] = mainCam.nearClipPlane + splits[0] * dis;
                near[1] = far[0];
                far[1] = near[1] + splits[1] * dis;
                near[2] = far[1];
                far[2] = near[2] + splits[2] * dis;
                near[3] = far[2];
                far[3] = near[3] + splits[3] * dis;

                // 计算包围球
                splitSpheres[0] = LightSpaceSplitSphere(f0_near, f0_far, near[0], far[0], mainCam);
                splitSpheres[1] = LightSpaceSplitSphere(f1_near, f1_far, near[1], far[1], mainCam);
                splitSpheres[2] = LightSpaceSplitSphere(f2_near, f2_far, near[2], far[2], mainCam);
                splitSpheres[3] = LightSpaceSplitSphere(f3_near, f3_far, near[3], far[3], mainCam);
            }

            // 画相机视锥体
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

            // 画光源方向的 AABB 包围盒
            void DrawAABB(Vector3[] points, Color color)
            {
                // 画线
                Debug.DrawLine(points[0], points[1], color);
                Debug.DrawLine(points[0], points[2], color);
                Debug.DrawLine(points[0], points[4], color);

                Debug.DrawLine(points[6], points[2], color);
                Debug.DrawLine(points[6], points[7], color);
                Debug.DrawLine(points[6], points[4], color);

                Debug.DrawLine(points[5], points[1], color);
                Debug.DrawLine(points[5], points[7], color);
                Debug.DrawLine(points[5], points[4], color);

                Debug.DrawLine(points[3], points[1], color);
                Debug.DrawLine(points[3], points[2], color);
                Debug.DrawLine(points[3], points[7], color);
            }

            public void DrawSplitSphere()
            {
                //DrawFrustum(nearCorners, farCorners, Color.white);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(splitSpheres[0], splitSpheres[0].w);
                Gizmos.DrawWireSphere(splitSpheres[1], splitSpheres[1].w);
                Gizmos.DrawWireSphere(splitSpheres[2], splitSpheres[2].w);
                Gizmos.DrawWireSphere(splitSpheres[3], splitSpheres[3].w);
            }

            public void DebugDraw()
            {
                DrawFrustum(nearCorners, farCorners, Color.white);
                DrawAABB(box0, Color.yellow);  
                DrawAABB(box1, Color.magenta);
                DrawAABB(box2, Color.green);
                DrawAABB(box3, Color.cyan);
            }
            
// 将相机配置为第 level 级阴影贴图的绘制模式
            public void ConfigCameraToShadowSpace(ref Camera camera, Vector3 lightDir, int level, float distance)
            {
                // 选择第 level 级视锥划分
                var box = new Vector3[8];
                if(level==0) box=box0; if(level==1) box=box1; 
                if(level==2) box=box2; if(level==3) box=box3;

                // 计算 Box 中点, 宽高比
                Vector3 center = (box[3] + box[4]) / 2; 
                float w = Vector3.Magnitude(box[0] - box[4]);
                float h = Vector3.Magnitude(box[0] - box[2]);

                // 配置相机
                camera.transform.rotation = Quaternion.LookRotation(lightDir);
                camera.transform.position = center; 
                camera.nearClipPlane = -distance;
                camera.farClipPlane = distance;
                camera.aspect = w / h;
                camera.orthographicSize = h * 0.5f;
                
            }
        }
    }
}