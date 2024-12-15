using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneGenerator : MonoBehaviour
{
    private Mesh myMesh;
    private MeshFilter meshFilter;
    private MeshRenderer _meshRenderer;
    [SerializeField] private Vector2 planeSize = Vector2.one;
    [SerializeField] private int planeResolution = 1;

    private List<Vector3> vertices;
    private List<int> triangles;
    private List<Vector2> uvs;
    private List<Vector3> normals;
    
    
    public void InstantiateMesh(Vector3 pos, int pSize, int pResolution)
    {
        this.transform.position = pos;
        Setup();
        GeneratePlane(new Vector2(pSize,pSize),pResolution);
        AssignMesh();
    }
    
    void Setup()
    {
        myMesh = new Mesh();
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = myMesh;
    }
    
    void GeneratePlane(Vector2 size, int resolution)
    {
        vertices = new List<Vector3>();
        uvs = new List<Vector2>();
        normals = new List<Vector3>();
        float xPerStep = size.x / resolution;
        float yPerStep = size.y / resolution;

        for (int y = 0; y < resolution + 1; y++)
        {
            for (int x = 0; x < resolution + 1; x++)
            {
                
                var vert = new Vector3(x * xPerStep, 0, y * yPerStep);
                //Debug.Log("VERT: " + vert + "|| UV: " + uv);
                vertices.Add(vert);
                //uvs.Add(uv);
                normals.Add(Vector3.up);
            }
        }
        
        for (int v = 0; v < resolution + 1; v++)
        {
            for (int u = 0; u < resolution + 1; u++)
            {
                uvs.Add(new Vector2((float)u / (resolution - 1), (float)v / (resolution - 1)));
            }
        }

        triangles = new List<int>();
        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                int i = (row * resolution) + row + column;
                //first triangle
                triangles.Add(i);
                triangles.Add(i+resolution+1);
                triangles.Add(i+resolution+2);
                //second
                triangles.Add(i);
                triangles.Add(i+resolution+2);
                triangles.Add(i+1);
            }
        }
        
        
    }

    void AssignMesh()
    {
        myMesh.Clear();
        myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        myMesh.vertices = vertices.ToArray();
        myMesh.normals = normals.ToArray();
        myMesh.triangles = triangles.ToArray();
        myMesh.uv = uvs.ToArray();
        
        //myMesh.RecalculateTangents();
        //myMesh.Optimize();
        //myMesh.UploadMeshData(true);
        
        //myMesh.RecalculateUVDistributionMetrics();
    }
    
}
