using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Debug = UnityEngine.Debug;
using Application = UnityEngine.Application;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using System.Diagnostics;
using System;
using System.Linq;
using System.Net;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.MixedReality.Toolkit.UI.BoundsControlTypes;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using UnityEngine.UIElements;
using UnityEditor;
using static NewBehaviourScript1;
using Unity.VisualScripting;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;
using System.Reflection.Emit;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine.UI;



[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class NewBehaviourScript2 : MonoBehaviour
{
    // 这一段是脚本最上面的状态变量，类似 Python 里的全局变量/类属性。
    // 这些变量在整个脚本里共享，所有方法都能访问。
    // static 表示“静态成员”：整个项目共享同一份数据，而不是每个物体各有一份。
    public static bool iscoloring = false;         // 是否正在进行点云染色
    public static bool tip = true;                 // 是否显示提示
    public static int[] Tag;                       // 每个点对应的标签编号，决定颜色类别
    public int tagmode = 1;                       // 当前标记工具模式：球/立方体/圆柱
    public int vessel = 1;                        // 0=SOM 方式，1=普通颜色/标记方式
    public static int colornum = 1;               // 当前选中的颜色编号
    public static int taggingmode = 1;            // 当前绘制/标记方式
    public static Color[] acolor = new Color[100]; // 颜色表，给每个标签对应一种颜色
    public static bool b2_Sign = false;           // 分解/恢复状态标志

    // C# 的 int 是整数类型；这里表示 SOM 点的最大数量。
    int SOM_NUM = 1000;
    private int[] SOM_List = new int[1000]; // 保存可用于 SOM 的点索引
    int local = 100; // 缩放系数，控制点云坐标大小

    private ObjectManipulator objectManipulator; // Unity 交互组件：拖拽/旋转/缩放

    // 结构体类似 Python 里的字典/数据类：把一组相关数据放在一个对象里。
    public struct pcdData
    {
        public float[] m_fX;       // 所有点的 X 坐标
        public float[] m_fY;       // 所有点的 Y 坐标
        public float[] m_fZ;       // 所有点的 Z 坐标
        public float[] m_fNorX;    // 法向量 X 分量
        public float[] m_fNorY;    // 法向量 Y 分量
        public float[] m_fNorZ;    // 法向量 Z 分量
        public float[] m_fCurvature; // 点的曲率
        public float[] m_label;    // 每个点的标签值
    }

    // PCD 文件头结构：保存点云文件开头的版本、字段、点数等信息。
    public struct PcdInfo
    {
        public string m_strVerInfo;  // PCD 文件版本，例如 VERSION .7
        public string m_strFileds;   // 字段定义，如 x y z label
        public string m_strSize;     // 每个字段的大小
        public string m_strType;     // 数据类型：F / I / U 等
        public string m_strCount;    // 字段个数
        public string m_strWidth;    // 点云宽度
        public string m_strHeight;   // 点云高度
        public string m_strViewPoint; // 视角信息
        public int m_nPoints;        // 点云总点数
        public string m_points;      // 点数字段字符串
        public string m_strData;     // DATA 关键字
        public pcdData m_pcdData;    // 实际存放点坐标和标签的数据
    }



    // 这是 PCD 文件解析器的核心头信息对象。
    // m_pcdHeader 里存放所有点云头信息，后面从文件里读取数据时会填进去。
    public PcdInfo m_pcdHeader;

    // 枚举类型：列出字段名字，方便访问和判断。类似 Python 里常量列表。
    public enum FiledNames { X, Y, Z, NorX, NorY, NorZ, Curvature };

    // 这是一个空方法，可能是之前写到一半的解析器框架。
    public void PcdFileParser()
    {
    }



    // 这个方法的功能是：读取某个 PCD 文件，然后把点云数据存到 m_pcdHeader 中。
    // 逻辑上等同于 Python 里：打开文件 -> 读取每行 -> 解析成数组 -> 存入字典/结构体。
    // public 表示可以被其他脚本调用，返回 bool 表示“成功/失败”。
    public bool LoadFile_Sphere(string strFile)
    {
        // File.ReadAllLines：一次性把文件按行读出来，返回一个 string[] 数组。
        string[] strs = File.ReadAllLines(strFile);

        // PCD 文件头通常前几行是固定格式：VERSION、FIELDS、SIZE、TYPE、COUNT、WIDTH、HEIGHT、VIEWPOINT...
        m_pcdHeader.m_strVerInfo = strs[0];
        m_pcdHeader.m_strFileds = strs[1];
        m_pcdHeader.m_strSize = strs[2];
        m_pcdHeader.m_strType = strs[3];
        m_pcdHeader.m_strCount = strs[4];
        m_pcdHeader.m_strWidth = strs[5];
        m_pcdHeader.m_strHeight = strs[6];
        m_pcdHeader.m_strViewPoint = strs[8];
        m_pcdHeader.m_points = strs[7];

        // 这里把点数写死成 10000-10，实际应该从 PCD 头中读取，比较容易出错。
        m_pcdHeader.m_nPoints = 10000-10;
        m_pcdHeader.m_strData = strs[9];

        // 给 pcdData 结构体中的所有数组分配长度。
        m_pcdHeader.m_pcdData = new pcdData();
        m_pcdHeader.m_pcdData.m_fX = new float[m_pcdHeader.m_nPoints];
        m_pcdHeader.m_pcdData.m_fY = new float[m_pcdHeader.m_nPoints];
        m_pcdHeader.m_pcdData.m_fZ = new float[m_pcdHeader.m_nPoints];
        m_pcdHeader.m_pcdData.m_label = new float[m_pcdHeader.m_nPoints];

        m_pcdHeader.m_pcdData.m_fNorX = new float[m_pcdHeader.m_nPoints];
        m_pcdHeader.m_pcdData.m_fNorY = new float[m_pcdHeader.m_nPoints];
        m_pcdHeader.m_pcdData.m_fNorZ = new float[m_pcdHeader.m_nPoints];
        m_pcdHeader.m_pcdData.m_fCurvature = new float[m_pcdHeader.m_nPoints];

        // 这里开始从第 11 行读取真实点数据。
        int f = 0;
        for (int i = 11; i < m_pcdHeader.m_nPoints; i++)
        {
            // 一行数据大概是：x y z label ...
            string[] data = strs[i+10].Split(' ');

            // float.Parse 把字符串转成 float，类似 Python 的 float()
            m_pcdHeader.m_pcdData.m_fX[i] = float.Parse(data[0]);
            m_pcdHeader.m_pcdData.m_fY[i] = float.Parse(data[1]);
            m_pcdHeader.m_pcdData.m_fZ[i] = float.Parse(data[2]);
            m_pcdHeader.m_pcdData.m_label[i] = float.Parse(data[3]);

            // 如果这行数据有第 5 个数，并且它大于 1，则认为它是 SOM 有效点。
            if (data.Length >= 5)
            {
                if (float.Parse(data[4]) >= 1 )
                {
                    SOM_List[f] = i; // 记录这个点的索引
                    f++;            // 索引计数+1
                }
            }
        }

        // 初始化标签数组 Tag，长度等于点数。
        int num = m_pcdHeader.m_nPoints;
        Tag = new int[num];

        return true;
    }



    // -------------------------------------------------------------------
    // 把标签数组转换成 Unity 里的点云网格对象。
    // 也就是：label -> 颜色类别 -> 生成多个小点 -> 组合成一个 mesh
    // 这一步相当于 Python 里把一堆点“分组后画图”。
    // -------------------------------------------------------------------
    // 这个方法的作用是：根据标签数组 labels 创建一个完整的 3D 点云对象。
    // 它会新建一个 GameObject，然后根据每个点的 label 分别绘制不同颜色。
    private void mesh_pointcloud(float[] labels)
    {
        // 1) 创建一个父对象，用于承载这批点云。
        GameObject pointObj = new GameObject();

        // 2) 如果还没着色成功，就命名为 "new"，否则命名为 "new1"。
        //    这里相当于 Python 里 if/else 判断并赋值。
        if (TcpText.coloringsuccess == false) pointObj.name = "new"; else pointObj.name = "new1";

        // 3) 设置这个父对象的初始位置。
        pointObj.transform.position = new Vector3(0, 1.8f, 0.5f);

        // 4) 通过 GameObject.Find 找到真正的父节点。
        GameObject father ;
        if (TcpText.coloringsuccess == false)
            father = GameObject.Find("new");
        else
            father = GameObject.Find("new1");

        // 5) 真正构建点云的逻辑，放在 mesh_creat 里。
        mesh_creat(labels, father, pointObj);

        // 6) 给生成出来的对象添加 BoxCollider、交互组件、边界控制。
        pointObj.AddComponent<BoxCollider>();
        pointObj.AddComponent<ObjectManipulator>();
        pointObj.AddComponent<BoundsControl>();

        // 7) 获取 BoxCollider，用来确定物体的中心和尺寸。
        BoxCollider boxCollider = pointObj.GetComponent<BoxCollider>();
        Vector3 center = boxCollider.bounds.center;
        Vector3 size = boxCollider.bounds.extents;

        // 8) 生成一个空对象，用作参考点或挂载点。
        GameObject newEmptyObject = new GameObject("EmptyObject");
        newEmptyObject.transform.position = new Vector3(0, 1.8f, 0.5f);
        newEmptyObject.transform.SetParent(father.transform);
    }

    // 这个函数的核心思路是：
    // 1. 统计每个 label 有多少个点；
    // 2. 根据 label 分组；
    // 3. 每一组用同一颜色渲染；
    // 4. 把同一组的点合并成一个 Mesh，减少 DrawCall。
    private void mesh_creat(float[] labels, GameObject father, GameObject pointObj)
    {
        // Dictionary 用来统计每个 label 出现了多少次，并记录哪些点属于它。
        Dictionary<float, int> labelCount = new Dictionary<float, int>();
        Dictionary<float, List<int>> labelIndices = new Dictionary<float, List<int>>();

        // 遍历所有标签，将点按 label 分类。
        for (int i = 0; i < labels.Length; i++)
        {
            if (!labelCount.ContainsKey(labels[i]))
            {
                labelCount[labels[i]] = 0;
                labelIndices[labels[i]] = new List<int>();
            }
            labelCount[labels[i]]++;
            labelIndices[labels[i]].Add(i);
        }

        // labelList 其实没有被真正用到，更多是说明一种统计过程。
        List<KeyValuePair<float, int>> labelList = new List<KeyValuePair<float, int>>(labelCount);
        int a = 0;

        // 找到一个基础材质对象，用来复用渲染材质。
        GameObject ma;
        ma = GameObject.Find("11111");
        MeshRenderer mr = ma.GetComponent<MeshRenderer>();

        // foreach 会按顺序逐个遍历每个 label 组。
        foreach (KeyValuePair<float, int> label in labelCount)
        {
            int g = (int)label.Key;  // 把标签值转换成 int，作为颜色索引
            Color newColor = acolor[g];

            // 资源名是 "点"，在 Assets/Resources 目录下 exists，因此可以加载。
            // GameObject importedPrefab1 = Resources.Load("点") as GameObject;
            List<int> indices = labelIndices[label.Key];

            // 当前 label 对应的点构成一个合并数组。
            CombineInstance[] combineInstances = new CombineInstance[label.Value];
            for (int i = 0; i < label.Value; ++i)
            {
                GameObject prefab = Resources.Load("点") as GameObject;
                MeshFilter prefabMesh = prefab.GetComponent<MeshFilter>();
                Vector3 xyz = new Vector3(
                    m_pcdHeader.m_pcdData.m_fX[indices[i]], 
                    m_pcdHeader.m_pcdData.m_fY[indices[i]], 
                    m_pcdHeader.m_pcdData.m_fZ[indices[i]]
                    ) /local;
                combineInstances[i].mesh = prefabMesh.sharedMesh;
                combineInstances[i].transform = Matrix4x4.TRS(
                    xyz, 
                    Quaternion.identity, 
                    new Vector3(0.0012f, 0.0012f, 0.0012f));
            }

            // 合成一个新的 Mesh，把同一组点统一渲染。
            Mesh newMesh = new Mesh();
            newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            newMesh.CombineMeshes(combineInstances);

            // 新建一个 GameObject 表示该类别的点云层。
            GameObject combinedObject = new GameObject("label_" + label.Key.ToString());
            combinedObject.transform.position = father.transform.position;
            combinedObject.transform.SetParent(father.transform);
            combinedObject.AddComponent<MeshFilter>().mesh = newMesh;
            combinedObject.AddComponent<MeshRenderer>().material = mr.material;
            combinedObject.GetComponent<MeshRenderer>().material.color = newColor;

            a += label.Value;
        }
    }

    // -------------------------------------------------------------------
    // 这个版本用于“修改颜色后重新生成点云”，即先删掉旧对象，再画新对象。
    // Python 里等价于：先 pop，再重新 append。
    // -------------------------------------------------------------------
    // 此函数用于“颜色更新后重建点云”。
    // 它和 mesh_creat 很类似，但会先删掉旧模型，再重新生成当前标签分组的网格。
    private void mesh_pointcloud_change(float[] labels)
    {
        Dictionary<float, int> labelCount = new Dictionary<float, int>();
        Dictionary<float, List<int>> labelIndices = new Dictionary<float, List<int>>();
        GameObject father;
        if (TcpText.coloringsuccess == false)
            father = GameObject.Find("new");
        else
            father = GameObject.Find("new1");

        // 先对 labels 做一次分组统计。
        for (int i = 0; i < labels.Length; i++)
        {
            if (!labelCount.ContainsKey(labels[i]))
            {
                labelCount[labels[i]] = 0;
                labelIndices[labels[i]] = new List<int>();
            }
            labelCount[labels[i]]++;
            labelIndices[labels[i]].Add(i);
        }

        GameObject ma = GameObject.Find("11111");
        MeshRenderer mr = ma.GetComponent<MeshRenderer>();
        Vector3 fatherscale = father.transform.localScale;

        // 遍历每个标签，生成新 mesh，并删除旧 mesh。
        foreach (KeyValuePair<float, int> label in labelCount)
        {
            GameObject oldObject = GameObject.Find("label_" + label.Key.ToString());
            if (oldObject != null)
            {
                MeshFilter oldMeshFilter = oldObject.GetComponent<MeshFilter>();
                if (oldMeshFilter != null && oldMeshFilter.mesh != null)
                {
                    DestroyImmediate(oldMeshFilter.mesh); // 立即销毁旧的 Mesh
                }
                DestroyImmediate(oldObject); // 立即销毁旧的 GameObject
            }

            int g = (int)label.Key;
            Color newColor = acolor[g];
            List<int> indices = labelIndices[label.Key];

            CombineInstance[] combineInstances = new CombineInstance[label.Value];
            for (int i = 0; i < label.Value; ++i)
            {
                GameObject prefab = Resources.Load("点") as GameObject;
                MeshFilter prefabMesh = prefab.GetComponent<MeshFilter>();
                Vector3 xyz = new Vector3(m_pcdHeader.m_pcdData.m_fX[indices[i]] * fatherscale.x, m_pcdHeader.m_pcdData.m_fY[indices[i]] * fatherscale.y, m_pcdHeader.m_pcdData.m_fZ[indices[i]] * fatherscale.z) /local;
                combineInstances[i].mesh = prefabMesh.sharedMesh;
                combineInstances[i].transform = Matrix4x4.TRS(xyz, Quaternion.identity, new Vector3(0.0012f * fatherscale.x, 0.0012f * fatherscale.y, 0.0012f * fatherscale.z));
            }

            Mesh newMesh = new Mesh();
            newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            newMesh.CombineMeshes(combineInstances);

            GameObject combinedObject = new GameObject("label_" + label.Key.ToString());
            combinedObject.transform.position = father.transform.position;
            combinedObject.transform.rotation = father.transform.rotation;
            combinedObject.transform.SetParent(father.transform);
            combinedObject.AddComponent<MeshFilter>().mesh = newMesh;
            combinedObject.AddComponent<MeshRenderer>().material = mr.material;
            combinedObject.GetComponent<MeshRenderer>().material.color = newColor;
        }

        // 强制清理垃圾，释放不再使用的资源。
        System.GC.Collect();
        Resources.UnloadUnusedAssets();
    }






    string filepath11;

    void Start()
    {
        // 定义 20 种固定颜色。
        Color[] fixedColors = new Color[]
        {
        Color.red,
        Color.green,
        Color.blue,
        new Color(1f, 0.5f, 0f), // 橙色
        new Color(0.5f, 0f, 0.5f), // 紫色
        Color.yellow,
        new Color(0f, 0.5f, 1f), // 天蓝色
        new Color(0.5f, 1f, 0f), // 浅绿色
        new Color(0.5f, 0.5f, 0f), // 橄榄色
        new Color(1f, 0f, 0.5f), // 粉色
        new Color(0f, 0.5f, 0.5f), // 青色
        new Color(0.5f, 0f, 1f), // 深紫色
        new Color(1f, 1f, 0f), // 明黄
        new Color(0.75f, 0.75f, 0.75f), // 灰色
        new Color(0.25f, 0.25f, 0.25f), // 深灰
        new Color(1f, 0.5f, 0.25f), // 橙红色
        new Color(0.5f, 0.25f, 0f), // 棕色
        new Color(0.25f, 1f, 0.5f), // 淡绿色
        new Color(0.5f, 0.5f, 1f), // 淡蓝色
        new Color(1f, 0.75f, 0f) // 金色
        };

        // 将固定颜色循环填充到 acolor 数组。
        for (int a = 0; a < acolor.Length; a++)
        {
            acolor[a] = fixedColors[a % fixedColors.Length];
        }

        filepath11 = Application.persistentDataPath;

    }

    private bool only1 = false;
    private bool only2 = false;
    void Update()
    {
        if (TcpText.ConnectedCompleted && dialog.hasResponded && !only1 && dialog.canload && TcpText.fileAccept)
        {
            iscoloring = true;
            LoadFile_Sphere(filepath11 + "/flowerWithoutLabel.pcd");
            mesh_pointcloud(m_pcdHeader.m_pcdData.m_label);

            GameObject.Find("new").GetComponent<BoundsControl>().BoundsControlActivation = BoundsControlActivationType.ActivateByPointer;
            only1 = true;
        }
        if (TcpText.ConnectedCompleted && dialog.hasResponded && !only2 && dialog.canload && TcpText.fileAccept && TcpText.coloringsuccess)
        {

            for (int j = 0; j < m_pcdHeader.m_nPoints; j++)
            {
                m_pcdHeader.m_pcdData.m_fX[j] = 0;
                m_pcdHeader.m_pcdData.m_fY[j] = 0;
                m_pcdHeader.m_pcdData.m_fZ[j] = 0;
                m_pcdHeader.m_pcdData.m_label[j] = 0;
            }
            Destroy(GameObject.Find("process1"));
            LoadFile_Sphere(filepath11 + "/flowerWithLabel.pcd");
            mesh_pointcloud(m_pcdHeader.m_pcdData.m_label);

            GameObject.Find("new1").GetComponent<BoundsControl>().BoundsControlActivation = BoundsControlActivationType.ActivateByPointer;
            only2 = true;
        }
    }



    private BoxCollider boxCollider;
    private BoundsControl boundscontrol;
    public void Frozen()
    {

        boxCollider = GameObject.Find("new").GetComponent<BoxCollider>();
        boundscontrol= GameObject.Find("new").GetComponent<BoundsControl>();
        if (boxCollider != null)
        {
            // 关闭 BoxCollider 的碰撞检测。
            boxCollider.enabled = !boxCollider.enabled;
            boundscontrol.enabled= !boundscontrol.enabled;
        }

    }


    public void B2() 
    {
        Dictionary<float, int> labelCount = new Dictionary<float, int>();
        float[] labels = m_pcdHeader.m_pcdData.m_label;
        GameObject father = GameObject.Find("new1");
        // Iterate over the labels.
        for (int i = 0; i < labels.Length; i++)
        {
            // If the label is not in the dictionary, add it.
            if (!labelCount.ContainsKey(labels[i]))
            {
                labelCount[labels[i]] = 0;

            }
            // Increase the count of the current label.
            labelCount[labels[i]]++;
        }
        if (!b2_Sign) 
        {
            
            foreach (KeyValuePair<float, int> label in labelCount)
            {
                GameObject test = GameObject.Find("label_" + label.Key.ToString());
                test.AddComponent<BoxCollider>();
                test.AddComponent<ObjectManipulator>();
                test.AddComponent<BoundsControl>().BoundsControlActivation = BoundsControlActivationType.ActivateByPointer;
                test.transform.SetParent(null, true);
                //Destroy(father.GetComponent<BoundsControl>());
            }
            Destroy(father);

        }
        if (b2_Sign)
        {

            foreach (KeyValuePair<float, int> label in labelCount)
            {
                GameObject test = GameObject.Find("label_"+label.Key.ToString());
                Destroy(test);
            }

            LoadFile_Sphere(filepath11 + "/flowerWithLabel.pcd");
            mesh_pointcloud(m_pcdHeader.m_pcdData.m_label);

            GameObject.Find("new1").GetComponent<BoundsControl>().BoundsControlActivation = BoundsControlActivationType.ActivateByPointer;

        }
        b2_Sign = !b2_Sign;
    }



 

    private void colorChange_mesh(int[] a) 
    {
        // int[] 是整数数组；这里转换成 float[]，以复用点云重建方法。
        float[] floatArray = new float[a.Length];

        for (int i = 0; i < a.Length; i++)
        {
            floatArray[i] = (float)a[i];
        }

        mesh_pointcloud_change(floatArray);

    }


    public void Tagging(Vector3 xyz_l,float r_l,Vector3 qiu_size_l)
    {
        // 参数由调用者传入：点的位置、半径，以及立方体/圆柱体的尺寸。
        float distance;
        Vector3 xyz_R = xyz_l;

        float z = GameObject.Find("new").GetComponent<Transform>().localScale.x;
        float r = r_l / 2;
        Vector3 qiu_size = qiu_size_l;
        Vector3 MXsize = xyz_R + 0.5f * qiu_size;
        Vector3 MNsize = xyz_R - 0.5f * qiu_size;


        for (int i = 0; i < m_pcdHeader.m_nPoints; ++i)
        {
            Vector3 xyz = new Vector3(m_pcdHeader.m_pcdData.m_fX[i] * z, m_pcdHeader.m_pcdData.m_fZ[i] * z, m_pcdHeader.m_pcdData.m_fY[i] * z) /local;
            distance = Vector3.Distance(xyz, xyz_R);
            // == 用于比较；= 用于赋值，这是 C# 和 Python 都很重要的区别。
            if (taggingmode == 1) if (distance < r) Tag[i] = colornum;
            if (taggingmode == 0)
            {
                if (xyz.x < MXsize.x & xyz.y < MXsize.y & xyz.z < MXsize.z)
                {
                    if (xyz.x > MNsize.x & xyz.y > MNsize.y & xyz.z > MNsize.z) Tag[i] = colornum;
                }
            }
            if (taggingmode == 2)
            {
                float cylinderHeight = 1f * qiu_size.x;
                float cylinderRadius = 0.5f * qiu_size.x;
                Vector3 pointXZ = new Vector3(xyz.x, 0, xyz.y);
                Vector3 cylinderBaseCenterXZ = new Vector3(xyz_R.x, 0, xyz_R.y);
                if (xyz.z > xyz_R.z - cylinderHeight & xyz.z < xyz_R.z + cylinderHeight)
                {
                    if (Vector3.Distance(pointXZ, cylinderBaseCenterXZ) < cylinderRadius) Tag[i] = colornum;
                }
            }

        }

        colorChange_mesh(Tag);
    }

    public void Tagging1()
    {
        GameObject sphere = GameObject.Find("Sphere");
        Vector3 xyz_l = Printlocation();
        float r_l = sphere.GetComponent<Transform>().localScale.x;
        Vector3 qiu_size_l = sphere.GetComponent<Transform>().localScale;

        // 调用本脚本中的 Tagging 方法。
       Tagging(xyz_l, r_l, qiu_size_l);
    }


    GameObject FindChildByName(GameObject parent, string name)
    {
        foreach (Transform child in parent.transform)
        {
            if (child.name == name)
            {
                return child.gameObject;
            }
            GameObject result = FindChildByName(child.gameObject, name);
            if (result != null)
            {
                return result;
            }
        }
        return null; // 没有找到时返回 null；null 表示“没有对象”。
    }

    public void colornumchange()
    {
        colornum += 1;
        /*
        for (int i = 0; i < 100; i++)
        {
            GameObject.Find(i.ToString()).GetComponent<MeshRenderer>().material.color = acolor[colornum];
        }
        */
        GameObject parent1 = GameObject.Find("changecolor"); 
        //GameObject child = FindChildByName(parent1, "Cylinder"); // 可以按实际子对象名称修改。
        GameObject child = parent1.transform.GetChild(0).GetChild(1).gameObject;
        child.GetComponent<Renderer>().material.color = acolor[colornum];
        if(GameObject.Find("Sphere"))
        {
            GameObject.Find("Sphere").GetComponent<Renderer>().material.color = acolor[colornum];
        }
        Debug.Log(colornum);
        Debug.Log(acolor[colornum]);
    }

    public void LocalChange(SliderEventData eventData)
    {
        if(GameObject.Find("KON"))
        {
            Vector3 o = new Vector3(0.01f, 0.01f, 0.01f);
            float value1 = GameObject.Find("Pinch").GetComponent<PinchSlider>().SliderValue*12;
            for(int i = 0; i <SOM_NUM; i++)
            {
                GameObject.Find(i.ToString()).GetComponent<Transform>().localScale = o * value1;
            }

        }
        if(GameObject.Find("Sphere"))
        {
            Vector3 o = new Vector3(0.05f, 0.05f, 0.05f);
            float value1 = GameObject.Find("Pinch").GetComponent<PinchSlider>().SliderValue * 2;
            GameObject.Find("Sphere").GetComponent<Transform>().localScale = o * value1;
        }
        
    }

    public void Change2Cube()
    {
        if (GameObject.Find("Sphere"))
        {
            Destroy(GameObject.Find("Sphere"));
            var objCube = GameObject.CreatePrimitive(PrimitiveType.Cube);// 创建立方体。
            objCube.name = "Sphere";
            objCube.transform.position = new Vector3(0, 1.8f, 0.5f);
            objCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            objCube.AddComponent<BoxCollider>();
            objCube.AddComponent<ObjectManipulator>();
            objCube.AddComponent<ConstraintManager>();
            objCube.GetComponent<MeshRenderer>().material.color = acolor[colornum];
            taggingmode = 0;
        }
    }
    public void Change2Sphere()
    {
        if (GameObject.Find("Sphere"))
        {
            Destroy(GameObject.Find("Sphere"));
            var objCube = GameObject.CreatePrimitive(PrimitiveType.Sphere);// 创建球体。
            objCube.name = "Sphere";
            objCube.transform.position = new Vector3(0, 1.8f, 0.5f);
            objCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            objCube.AddComponent<BoxCollider>();
            objCube.AddComponent<ObjectManipulator>();
            objCube.AddComponent<ConstraintManager>();
            objCube.GetComponent<MeshRenderer>().material.color = acolor[colornum];
            taggingmode = 1;
        }
    }
    public void Change2Cylinder()
    {
        if (GameObject.Find("Sphere"))
        {
            Destroy(GameObject.Find("Sphere"));
            var objCube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);// 创建圆柱体。
            objCube.name = "Sphere";
            objCube.transform.position = new Vector3(0, 1.8f, 0.5f);
            objCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            objCube.AddComponent<BoxCollider>();
            objCube.AddComponent<ObjectManipulator>();
            objCube.AddComponent<ConstraintManager>();
            objCube.GetComponent<MeshRenderer>().material.color = acolor[colornum];
            taggingmode = 2;
        }
    }

    public void Tagmode()
    {
        
        if (GameObject.Find("KON"))
        {
            Destroy(GameObject.Find("KON"));
            Destroy(GameObject.Find("Pinch"));
            Destroy(GameObject.Find("tagging"));
            Destroy(GameObject.Find("changecolor"));
            Destroy(GameObject.Find("menu"));
        }
        else if (GameObject.Find("new"))
        {
            BoxCollider boxCollider = GameObject.Find("new").GetComponent<BoxCollider>();
            Vector3 center = boxCollider.bounds.center;
            Vector3 size = boxCollider.bounds.extents;

            Vector3 location = center + size + new Vector3(0.1f, 0.1f, 0.1f);

            GameObject importedPrefab1 = Resources.Load("changecolor") as GameObject;
            importedPrefab1 = Instantiate(importedPrefab1);
            importedPrefab1.name = "changecolor";
            importedPrefab1.GetComponent<Transform>().position = center - new Vector3(0, size.y, size.z) + new Vector3(-0.242f, -0.06f, -0.02f);
            importedPrefab1.transform.GetChild(0).GetComponent<Interactable>().OnClick.AddListener(colornumchange);
            importedPrefab1.AddComponent<SolverHandler>();
            importedPrefab1.transform.SetParent(GameObject.Find("new").transform);



            GameObject importedPrefab2 = Resources.Load("tagging") as GameObject;
            importedPrefab2 = Instantiate(importedPrefab2);
            importedPrefab2.name = "tagging";
            importedPrefab2.GetComponent<Transform>().position = center - new Vector3(0, size.y, size.z) + new Vector3(0.242f, -0.06f, -0.02f);
            importedPrefab2.transform.GetChild(0).GetComponent<Interactable>().OnClick.AddListener(Tagging1);
            importedPrefab2.AddComponent<SolverHandler>();
            importedPrefab2.transform.SetParent(GameObject.Find("new").transform);


            GameObject importedPrefab3 = Resources.Load("Pinch") as GameObject;
            importedPrefab3 = Instantiate(importedPrefab3);
            importedPrefab3.name = "Pinch";
            importedPrefab3.GetComponent<Transform>().position = center - new Vector3(0, size.y, size.z) + new Vector3(0, -0.06f, -0.02f);
            importedPrefab3.GetComponent<PinchSlider>().OnValueUpdated.AddListener(LocalChange);
            importedPrefab3.AddComponent<SolverHandler>();
            importedPrefab3.transform.SetParent(GameObject.Find("new").transform);

            GameObject Cube_father = new GameObject();
            Cube_father.GetComponent<Transform>().position = GameObject.Find("new").GetComponent<Transform>().position;
            Cube_father .transform.SetParent(GameObject.Find ("new").transform);
            Cube_father.name = "KON";
            
            for (int i = 0; i < SOM_NUM; i++)
            {
                //Debug.Log(SOM_List[i]);
                Vector3 P_List = new Vector3(m_pcdHeader.m_pcdData.m_fX[SOM_List[i]], m_pcdHeader.m_pcdData.m_fY[SOM_List[i]], m_pcdHeader.m_pcdData.m_fZ[SOM_List[i]]) / local;
                GameObject importedPrefab = Resources.Load("球") as GameObject;
                importedPrefab = Instantiate(importedPrefab);
                importedPrefab.name=i.ToSafeString();
                importedPrefab.GetComponent<Transform>().position = P_List+ Cube_father.GetComponent<Transform>().position;
                //importedPrefab.GetComponent<Transform>().localScale = new Vector3(0.05f, 0.05f, 0.05f);
                importedPrefab.transform.SetParent(Cube_father.transform);

            }
            
        }
    }



    // 把当前标签写回 PCD 文件。void 表示这个方法不返回结果。
    public void SaveToPcd()// 保存 PCD
    {

        string strDir = Application.persistentDataPath;

        if (m_pcdHeader.m_nPoints > 0)
        {

            string strFile = string.Format("{0}\\{1}.pcd", strDir, "savepcdaaa");
            using (FileStream fs = new FileStream(strFile, FileMode.Append, FileAccess.Write))
            {
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(m_pcdHeader.m_strVerInfo);
                sw.WriteLine(m_pcdHeader.m_strFileds);
                sw.WriteLine(m_pcdHeader.m_strSize);
                sw.WriteLine(m_pcdHeader.m_strType);
                sw.WriteLine(m_pcdHeader.m_strCount);
                sw.WriteLine(m_pcdHeader.m_strWidth);
                sw.WriteLine(m_pcdHeader.m_strHeight);
                sw.WriteLine(m_pcdHeader.m_points);
                sw.WriteLine(m_pcdHeader.m_strViewPoint);
                sw.WriteLine(m_pcdHeader.m_strData);
                for (int j = 0; j < m_pcdHeader.m_nPoints; j++)
                {

                    sw.WriteLine(m_pcdHeader.m_pcdData.m_fX[j].ToString() + " " + m_pcdHeader.m_pcdData.m_fY[j].ToString() + " " + m_pcdHeader.m_pcdData.m_fZ[j].ToString() + " " + Tag[j].ToString() + " " + "-1");

                }
                sw.Flush();
                fs.Close();
            }

        }

    }


    public void modeChange()
    {
        if (GameObject.Find("Sphere"))
        {
            Destroy(GameObject.Find("Sphere"));
            Destroy(GameObject.Find("menu"));

            GameObject Cube_father = new GameObject();
            Cube_father.GetComponent<Transform>().position = GameObject.Find("new").GetComponent<Transform>().position;
            Cube_father.transform.SetParent(GameObject.Find("new").transform);
            Cube_father.name = "KON";

            for (int i = 0; i < SOM_NUM; i++)
            {
                //Debug.Log(SOM_List[i]);
                Vector3 P_List = new Vector3(m_pcdHeader.m_pcdData.m_fX[SOM_List[i]], m_pcdHeader.m_pcdData.m_fY[SOM_List[i]], m_pcdHeader.m_pcdData.m_fZ[SOM_List[i]]) / local;
                GameObject importedPrefab = Resources.Load("球") as GameObject;
                importedPrefab = Instantiate(importedPrefab);
                importedPrefab.name = i.ToSafeString();
                importedPrefab.GetComponent<Transform>().position = P_List + Cube_father.GetComponent<Transform>().position;
                //importedPrefab.GetComponent<Transform>().localScale = new Vector3(0.05f, 0.05f, 0.05f);
                importedPrefab.transform.SetParent(Cube_father.transform);

            }
            vessel = 0;
        }
        else if (GameObject.Find("new"))
        {
            BoxCollider boxCollider = GameObject.Find("new").GetComponent<BoxCollider>();
            Vector3 center = boxCollider.bounds.center;
            Vector3 size = boxCollider.bounds.extents;

            Destroy(GameObject.Find("KON"));

            GameObject importedPrefab4 = Resources.Load("menu") as GameObject;
            importedPrefab4 = Instantiate(importedPrefab4);
            importedPrefab4.name = "menu";
            importedPrefab4.GetComponent<Transform>().position = center - new Vector3(size.x, size.y, size.z) + new Vector3(-0.1f, size.y, size.z);
            importedPrefab4.AddComponent<SolverHandler>();
            importedPrefab4.transform.SetParent(GameObject.Find("new").transform);

            GameObject child1 = importedPrefab4.transform.GetChild(1).GetChild(0).gameObject;
            child1.AddComponent<Interactable>().OnClick.AddListener(Change2Cube);
            GameObject child2 = importedPrefab4.transform.GetChild(1).GetChild(1).gameObject;
            child2.AddComponent<Interactable>().OnClick.AddListener(Change2Sphere);
            GameObject child3 = importedPrefab4.transform.GetChild(1).GetChild(2).gameObject;
            child3.AddComponent<Interactable>().OnClick.AddListener(Change2Cylinder);

            GameObject importedPerfab=Resources.Load("Sphere") as GameObject;
            importedPerfab = Instantiate(importedPerfab);
            importedPerfab.name = "Sphere";
            importedPerfab.transform.position = center;
            importedPerfab.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            vessel = 1;
            importedPerfab.AddComponent<BoxCollider>();
            importedPerfab.AddComponent<ObjectManipulator>();
            importedPerfab.AddComponent<ConstraintManager>();
        }
    }

    private Vector3 Printlocation()
    {

        Transform targetTransform = GameObject.Find("EmptyObject").GetComponent<Transform>();
        Transform referenceTransform = GameObject.Find("Sphere").GetComponent<Transform>();
        //Vector3 relativePosition = referenceTransform.InverseTransformPoint(targetTransform.position);
        Vector3 distance = referenceTransform.transform.position - targetTransform.transform.position;
        Vector3 relativePosition = Vector3.zero;
        relativePosition.x = Vector3.Dot(distance, targetTransform.transform.right.normalized);
        relativePosition.z = Vector3.Dot(distance, targetTransform.transform.up.normalized);
        relativePosition.y = Vector3.Dot(distance, targetTransform.transform.forward.normalized);
        

        return relativePosition;
    }


}
